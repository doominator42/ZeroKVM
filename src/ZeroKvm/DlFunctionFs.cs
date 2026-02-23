using System.Buffers.Binary;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using UsbFfs;

namespace ZeroKvm;

internal class DlFunctionFs : FunctionFs
{
    public DlFunctionFs(string mountPath, int maxWidth, int maxHeight, int maxPixels)
        : base(mountPath)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxWidth);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maxWidth, short.MaxValue);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxHeight);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maxHeight, short.MaxValue);
        HandleConfig0Setup = true;
        HandleAllControlRequests = true;
        MaxWidth = maxWidth;
        MaxHeight = maxHeight;
        MaxPixels = maxPixels;

        _vendorDescriptor = new((ushort)maxWidth, (ushort)maxHeight, (uint)maxPixels);
        _edid = new byte[Unsafe.SizeOf<Edid>()];
        _edidLock = new();
        _memory = new();
        _unprocessedBuffer = GC.AllocateUninitializedArray<byte>(UnprocessedBufferSize, true);
    }

    public int MaxWidth { get; }

    public int MaxHeight { get; }

    public int MaxPixels { get; }

    public bool SupportsVendorDescriptor { get; private set; }

    private readonly DlVendorUsbDescriptor _vendorDescriptor;

    private readonly byte[] _edid;

    private readonly Lock _edidLock;

    public ExclusiveEdid AcquireEdid()
    {
        Lock @lock = _edidLock;
        @lock.Enter();
        return new(_edid, @lock);
    }

    private UsbOutEp? _ep1;
    private AioContext? _aio;

    public void Initialize()
    {
        const int EINVAL = 22;

        UsbDescriptor interfaceDesc = new UsbInterfaceDescriptor()
        {
            DescriptorType = 0x04,
            InterfaceNumber = 0,
            AlternateSetting = 0,
            NumEndpoints = 1,
            InterfaceClass = 0xff,
            InterfaceStringIndex = 1,
        };
        UsbDescriptor vendorDesc = _vendorDescriptor;
        UsbDescriptor ep1Desc = new UsbEndpointDescriptor()
        {
            DescriptorType = 0x05,
            EndpointAddress = 1,
            Attributes = 0x02,
            MaxPacketSize = 512,
        };

        try
        {
            ReadOnlySpan<UsbDescriptor> descs = [interfaceDesc, vendorDesc, ep1Desc];
            WriteDescriptors(descs, descs);
            SupportsVendorDescriptor = true;
        }
        catch (ErrnoIOException ex) when (ex.Errno == EINVAL)
        {
            SupportsVendorDescriptor = false;
            ReadOnlySpan<UsbDescriptor> descs = [interfaceDesc, ep1Desc];
            WriteDescriptors(descs, descs);
        }

        WriteStrings(0x409, [
            "ZeroKVM", // TODO: configurable
            "ZeroKVM",
            "123456",
        ]);

        Logger.LogDebug(static @this => $"USB descriptors and strings configured, {nameof(SupportsVendorDescriptor)}={@this.SupportsVendorDescriptor}", this);

        _ep1 = OpenOutEpForAio(1, 8, UnprocessedBufferSize + ReceiveBufferSize, UnprocessedBufferSize);
        _aio = InitializeAio([_ep1]);
    }

    public void BeginReceiver()
    {
        AioContext? aio = _aio;
        if (aio is null)
        {
            ThrowNotInitialized();
        }

        aio.Submit();
    }

    protected override void OnSetup(UsbControlRequest request, UsbEp0 ep0)
    {
        Logger.LogDebug(static request => $"{nameof(OnSetup)}(0x{request.RequestType:x2}, 0x{request.Request:x2}, 0x{request.Value:x4}, 0x{request.Index:x4}, {request.Length})", request);
        if (request.IsDeviceRecipient)
        {
            if (request.IsDirectionIn)
            {
                if (request.IsVendorRequest)
                {
                    switch (request.Request)
                    {
                        case 0x02 when request.Index is 0xa1 or 0x10a1: // 0xa1 is wLength = 2, 0x10a1 is wLength >= 2 && wLength <= 64
                            Logger.LogDebug(static request => $"Read EDID at 0x{BinaryPrimitives.ReverseEndianness(request.Value):x4}:{request.Length - 1}", request);
                            Span<byte> edidResponse = stackalloc byte[request.Length];
                            lock (_edidLock)
                            {
                                GetEdidResponse(_edid, BinaryPrimitives.ReverseEndianness(request.Value), request.Length, edidResponse);
                            }

                            ep0.Write(edidResponse);
                            return;

                        case 0x04:
                            Logger.LogDebug(static args => $"Read RAM at 0x{args.request.Index:x4}: {Convert.ToHexStringLower(GetMemoryRegion(args._memory.Ram, args.request.Index, args.request.Length))}", (request, _memory));
                            ep0.Write(GetMemoryRegion(_memory.Ram, request.Index, request.Length));
                            return;

                        case 0x05: // wValue = 0x0000, wIndex = 0x0002
                            Logger.LogDebug("Verify 0x5f checksum");
                            Span<byte> checksum = stackalloc byte[4];
                            BinaryPrimitives.WriteInt32LittleEndian(checksum, _vendorDescriptor.ComputeChecksum());
                            ep0.Write(checksum);
                            return;

                        case 0x06: // wValue = 0x0000, wIndex = 0x0000
                            Logger.LogDebug("Get device flags");
                            Span<byte> flags = stackalloc byte[4];
                            BinaryPrimitives.WriteUInt32LittleEndian(flags, GetDeviceFlags(SupportsVendorDescriptor));
                            ep0.Write(flags);
                            return;
                    }
                }
                else if (request.IsGetDescriptorRequest && request.Value == 0x5f00) // GET_DESCRIPTOR 0x5f
                {
                    Logger.LogDebug("GET_DESCRIPTOR 0x5f");
                    ep0.Write(((UsbDescriptor)_vendorDescriptor).RawBytes.Span);
                    return;
                }
            }
            else if (request.IsVendorRequest)
            {
                Span<byte> readBuffer = stackalloc byte[request.Length];
                switch (request.Request)
                {
                    case 0x03:
                        ep0.ReadExactly(readBuffer);
                        Logger.LogDebug($"Write RAM at 0x{request.Index:x4}: {Convert.ToHexStringLower(readBuffer)}");
                        readBuffer.CopyTo(GetMemoryRegion(_memory.Ram, request.Index, request.Length));
                        return;

                    // TODO: add support for encryption? these are null keys and I have not encountered a device/driver using other than these
                    case 0x12:
                        ep0.ReadExactly(readBuffer);
                        Logger.LogDebug("Set encryption key: " + Convert.ToHexStringLower(readBuffer));

                        if (!readBuffer.SequenceEqual((ReadOnlySpan<byte>)[0x57, 0xcd, 0xdc, 0xa7, 0x1c, 0x88, 0x5e, 0x15, 0x60, 0xfe, 0xc6, 0x97, 0x16, 0x3d, 0x47, 0xf2]) &&
                            !readBuffer.SequenceEqual((ReadOnlySpan<byte>)[0x47, 0x3d, 0x16, 0x97, 0xc6, 0xfe, 0x60, 0x15, 0x5e, 0x88, 0x1c, 0xa7, 0xdc, 0xb7, 0x6f, 0xf2]))
                        {
                            Logger.LogError($"Unexpected encryption key: " + Convert.ToHexStringLower(readBuffer));
                        }

                        return;

                    case 0x14:
                        ep0.ReadExactly(readBuffer);
                        return;
                }
            }
        }

        base.OnSetup(request, ep0);
    }

    private static void GetEdidResponse(ReadOnlySpan<byte> edid, uint offset, int requestLength, Span<byte> response)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(response.Length, requestLength);
        if (offset > (uint)edid.Length || (uint)requestLength - 1 > (uint)edid.Length || offset + (uint)requestLength - 1 > (uint)edid.Length)
        {
            response[0..requestLength].Clear();
            response[0] = 1;
        }
        else
        {
            response[0] = 0;
            edid.Slice((int)offset, requestLength - 1).CopyTo(response[1..]);
        }
    }

    private static Span<byte> GetMemoryRegion(Span<byte> memory, ushort offset, ushort length)
    {
        return offset + length <= memory.Length ? memory.Slice(offset, length) : new byte[length];
    }

    private static uint GetDeviceFlags(bool reconfigure)
    {
        const uint ChipAsic = 0b11U << 30;
        const uint BootStandalone = 0b11U << 28;
        const uint Reconfigure = 0b1;
        const uint UnknownFlags = 0x5 << 12;

        return ChipAsic | BootStandalone | (reconfigure ? Reconfigure : 0) | UnknownFlags;
    }

    private readonly DlMemory _memory;
    private const int ReceiveBufferSize = 2 * 1024 * 1024;
    private const int UnprocessedBufferSize = 1024;
    private readonly byte[] _unprocessedBuffer;
    private int _unprocessedCount;

    private event Action? _frameBufferUpdated;

    public event Action FrameBufferUpdated
    {
        add
        {
            _memory.RegistersUpdate += value;
            _frameBufferUpdated += value;
        }
        remove
        {
            _memory.RegistersUpdate -= value;
            _frameBufferUpdated -= value;
        }
    }

    public FrameArea CopyFrameBufferTo(Span<uint> fb)
    {
        return _memory.CopyFrameBuffer16To(fb);
    }

    public void ReceiveFrameBuffer()
    {
        AioContext? aio = _aio;
        if (aio is null)
        {
            ThrowNotInitialized();
        }

        bool statisticsEnabled = Logger.StatisticsEnabled;
        Span<AioContext.Event> events = statisticsEnabled ? ReadEventsWithStats(aio) : aio.ReadEvents();
        try
        {
            foreach (AioContext.Event @event in events)
            {
                try
                {
                    if (@event.Ep == _ep1)
                    {
                        int unprocessedCount = _unprocessedCount;
                        Span<byte> stream;
                        if (unprocessedCount > 0)
                        {
                            stream = @event.FullBuffer.Slice(UnprocessedBufferSize - unprocessedCount);
                            _unprocessedBuffer.AsSpan(0, unprocessedCount).CopyTo(stream);
                            unprocessedCount = 0;
                            _unprocessedCount = 0;
                        }
                        else
                        {
                            stream = @event.Buffer;
                        }

                        int processedCount = statisticsEnabled ?
                            ProcessWithStats(stream, _memory) :
                            DlDecoder.Process(stream, _memory);

                        long timestamp = Stopwatch.GetTimestamp();
                        if (Stopwatch.GetElapsedTime(_memory.LastRegistersUpdateTimestamp, timestamp) > TimeSpan.FromMilliseconds(50))
                        {
                            _frameBufferUpdated?.Invoke();
                            _memory.LastRegistersUpdateTimestamp = timestamp;
                        }

                        if (processedCount > 0 && processedCount < stream.Length)
                        {
                            unprocessedCount = stream.Length - processedCount;
                            if (unprocessedCount > UnprocessedBufferSize)
                            {
                                ThrowBufferOverlow();
                            }

                            stream.Slice(processedCount).CopyTo(_unprocessedBuffer);
                            _unprocessedCount = unprocessedCount;
                            Logger.LogDebug(static (unprocessedCount) => $"Unprocessed: {unprocessedCount} bytes", unprocessedCount);
                        }
                    }
                }
                finally
                {
                    aio.PrepareRead(@event);
                }
            }
        }
        finally
        {
            if (statisticsEnabled)
            {
                SubmitWithStats(aio);
            }
            else
            {
                aio.Submit();
            }
        }

        [DoesNotReturn]
        static void ThrowBufferOverlow()
        {
            throw new InternalBufferOverflowException("Too many unprocessed data");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static Span<AioContext.Event> ReadEventsWithStats(AioContext aio)
        {
            long timestamp = Stopwatch.GetTimestamp();
            Span<AioContext.Event> events = aio.ReadEvents();
            Logger.CurrentStatistics.DlTotalIoTime += Stopwatch.GetTimestamp() - timestamp;
            return events;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void SubmitWithStats(AioContext aio)
        {
            long timestamp = Stopwatch.GetTimestamp();
            aio.Submit();
            Logger.CurrentStatistics.DlTotalIoTime += Stopwatch.GetTimestamp() - timestamp;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static int ProcessWithStats(Span<byte> stream, DlMemory memory)
        {
            Logger.CurrentStatistics.DlTotalReceived += stream.Length;

            long timestamp = Stopwatch.GetTimestamp();
            int processedCount = DlDecoder.Process(stream, memory);
            Logger.CurrentStatistics.DlTotalProcessTime += Stopwatch.GetTimestamp() - timestamp;
            return processedCount;
        }
    }

    [DoesNotReturn]
    private static void ThrowNotInitialized()
    {
        throw new InvalidOperationException("Not initialized");
    }

    protected override void DisposeInternal()
    {
        base.DisposeInternal();
        _ep1?.Dispose();
        _aio?.Dispose();
    }

    public class ExclusiveEdid : IDisposable
    {
        internal ExclusiveEdid(byte[] edid, Lock @lock)
        {
            _edidBytes = edid;
            _lock = @lock;
        }

        private byte[]? _edidBytes;
        private readonly Lock _lock;

        public ref Edid Value => ref Unsafe.As<byte, Edid>(ref RawBytes.Span[0]);

        public Memory<byte> RawBytes
        {
            get
            {
                byte[]? edid = _edidBytes;
                ObjectDisposedException.ThrowIf(edid is null, this);
                return edid;
            }
        }

        public void Dispose()
        {
            byte[]? edidBytes = Interlocked.Exchange(ref _edidBytes, null);
            if (edidBytes is not null)
            {
                ref Edid edid = ref Unsafe.As<byte, Edid>(ref edidBytes[0]);
                edid.Checksum = edid.ComputeChecksum();
                _lock.Exit();
            }
        }
    }
}
