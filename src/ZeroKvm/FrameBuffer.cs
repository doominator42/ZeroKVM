using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ZeroKvm;

internal unsafe class FrameBuffer : IDisposable
{
    public FrameBuffer(int maxPixels, int initialWidth, int initialHeight)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxPixels, 1);

        _updateWaiter = new(TaskCreationOptions.RunContinuationsAsynchronously);
        MaxPixels = maxPixels;
        Resize(initialWidth, initialHeight);

        _fbPtr = (uint*)NativeMemory.AlignedAlloc((nuint)(maxPixels * sizeof(uint)), 32);
    }

    private readonly uint* _fbPtr;
    private int _disposed;

    public Span<uint> Pixels
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed != 0, this);
            return new(_fbPtr, Width * Height);
        }
    }

    public Span<uint> AllPixels
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed != 0, this);
            return new(_fbPtr, MaxPixels);
        }
    }

    public int MaxPixels { get; }

    public int Width { get; private set; }

    public int Height { get; private set; }

    private uint _updateCount;

    public uint UpdateCount => _updateCount;

    private readonly List<Client> _clients = new();
    private readonly Lock _clientsLock = new();
    private readonly SemaphoreSlim _lock = new(1, 1);
    private TaskCompletionSource _updateWaiter;

    public Task UpdateWaiter => _updateWaiter.Task;

    public bool TryLock(out uint updateCount)
    {
        if (_lock.Wait(0))
        {
            updateCount = _updateCount;
            return true;
        }
        else
        {
            updateCount = 0;
            return false;
        }
    }

    public Task LockAsync(CancellationToken cancellationToken = default)
    {
        return _lock.WaitAsync(cancellationToken);
    }

    public void ReleaseLock()
    {
        _lock.Release();
    }

    public void ReleaseLockAfterUpdate(uint lastUpdateCount)
    {
        TaskCompletionSource? waiter = null;
        if (lastUpdateCount != _updateCount)
        {
            waiter = _updateWaiter;
            _updateWaiter = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        _lock.Release();

        waiter?.TrySetResult();
    }

    public void Update(int x1, int y1, int x2, int y2)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)x1, (uint)Width);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)y1, (uint)Height);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual((uint)x2, (uint)x1);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual((uint)y2, (uint)y1);

        lock (_clientsLock)
        {
            foreach (Client client in _clients)
            {
                client.Update(x1, y1, x2, y2);
            }

            _updateCount++;
        }
    }

    public Client CreateClient()
    {
        Client client = new(this);
        lock (_clientsLock)
        {
            _clients.Add(client);
        }

        return client;
    }

    public void Resize(int width, int height)
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(width * height, MaxPixels);

        lock (_clientsLock)
        {
            Width = width;
            Height = height;

            foreach (Client client in _clients)
            {
                client.Reset(width, height);
            }
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _lock.Dispose();
            NativeMemory.AlignedFree(_fbPtr);
        }
    }

    public class Client : IDisposable
    {
        public Client(FrameBuffer owner)
        {
            _owner = owner;
            AtomicRect.Write(ref _updateRect, new(0, 0, (ushort)owner.Width, (ushort)owner.Height));
        }

        private readonly FrameBuffer _owner;
        private AtomicRect _updateRect;

        public void Update(int x1, int y1, int x2, int y2)
        {
            AtomicRect.UnionWrite(ref _updateRect, new((ushort)x1, (ushort)y1, (ushort)x2, (ushort)y2));
        }

        public void Reset(int width, int height)
        {
            AtomicRect.Write(ref _updateRect, new(0, 0, (ushort)width, (ushort)height));
        }

        public ReadOnlySpan<uint> ConsumeUpdate(out int x, out int y, out int width, out int height, out int lineStride)
        {
            AtomicRect rect = AtomicRect.Write(ref _updateRect, AtomicRect.None);
            if (rect.IsNone)
            {
                x = 0;
                y = 0;
                width = 0;
                height = 0;
                lineStride = 0;
                return default;
            }

            FrameBuffer owner = _owner;
            int fbWidth = owner.Width;
            int fbHeight = owner.Height;
            rect.Grow(2, fbWidth, fbHeight);
            x = rect.X1;
            y = rect.Y1;
            width = rect.Width;
            height = rect.Height;
            lineStride = fbWidth;
            int offset = (y * lineStride) + x;
            int length = (height * lineStride) - x;
            if (offset + length > owner.MaxPixels)
            {
                ThrowInvalidRect();
            }

            return new(owner._fbPtr + offset, length);

            [DoesNotReturn]
            static void ThrowInvalidRect()
            {
                throw new InvalidOperationException("Invalid rectangle bounds");
            }
        }

        public void Dispose()
        {
            FrameBuffer owner = _owner;
            lock (owner._clientsLock)
            {
                owner._clients.Remove(this);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private readonly struct AtomicRect : IEquatable<AtomicRect>
    {
        public static AtomicRect None => new(
            x1: ushort.MaxValue,
            y1: ushort.MaxValue,
            x2: 0,
            y2: 0);

        public static AtomicRect Write(ref AtomicRect location, AtomicRect value)
        {
            return Unsafe.BitCast<ulong, AtomicRect>(Interlocked.Exchange(
                ref Unsafe.As<AtomicRect, ulong>(ref location),
                Unsafe.BitCast<AtomicRect, ulong>(value)));
        }

        public static AtomicRect CompareWrite(ref AtomicRect location, AtomicRect value, AtomicRect comparand)
        {
            return Unsafe.BitCast<ulong, AtomicRect>(Interlocked.CompareExchange(
                ref Unsafe.As<AtomicRect, ulong>(ref location),
                Unsafe.BitCast<AtomicRect, ulong>(value),
                Unsafe.BitCast<AtomicRect, ulong>(comparand)));
        }

        public static AtomicRect Read(ref AtomicRect location)
        {
            return Unsafe.BitCast<ulong, AtomicRect>(Interlocked.Read(ref Unsafe.As<AtomicRect, ulong>(ref location)));
        }

        public static void UnionWrite(ref AtomicRect location, AtomicRect other)
        {
            AtomicRect value;
            do
            {
                value = Read(ref location);
            }
            while (CompareWrite(ref location, value.Union(other), value) != value);
        }

        public AtomicRect(ushort x1, ushort y1, ushort x2, ushort y2)
        {
            X1 = x1;
            Y1 = y1;
            X2 = x2;
            Y2 = y2;
        }

        public readonly ushort X1;
        public readonly ushort Y1;
        public readonly ushort X2;
        public readonly ushort Y2;

        public bool IsNone => X2 <= X1;

        public int Width => X2 - X1;

        public int Height => Y2 - Y1;

        public AtomicRect Union(AtomicRect other)
        {
            return new(
                x1: Math.Min(X1, other.X1),
                y1: Math.Min(Y1, other.Y1),
                x2: Math.Max(X2, other.X2),
                y2: Math.Max(Y2, other.Y2));
        }

        public AtomicRect Grow(int amount, int maxWidth, int maxHeight)
        {
            return new(
                x1: (ushort)Math.Max(X1 - amount, 0),
                y1: (ushort)Math.Max(Y1 - amount, 0),
                x2: (ushort)Math.Min(X2 + amount, maxWidth),
                y2: (ushort)Math.Min(Y2 + amount, maxHeight));
        }

        public override int GetHashCode()
        {
            return Unsafe.BitCast<AtomicRect, ulong>(this).GetHashCode();
        }

        public override bool Equals([NotNullWhen(true)] object? obj)
        {
            return obj is AtomicRect rectObj && Equals(rectObj);
        }

        public bool Equals(AtomicRect other)
        {
            return this == other;
        }

        public static bool operator ==(AtomicRect left, AtomicRect right)
        {
            return Unsafe.BitCast<AtomicRect, ulong>(left) == Unsafe.BitCast<AtomicRect, ulong>(right);
        }

        public static bool operator !=(AtomicRect left, AtomicRect right)
        {
            return Unsafe.BitCast<AtomicRect, ulong>(left) != Unsafe.BitCast<AtomicRect, ulong>(right);
        }
    }
}
