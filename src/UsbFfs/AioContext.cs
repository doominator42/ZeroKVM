using System.Runtime.InteropServices;

namespace UsbFfs;

public unsafe class AioContext : IDisposable
{
    internal AioContext(nint ptr, int maxEvents, ReadOnlySpan<UsbEp> eps)
    {
        _ptr = ptr;
        _eps = eps.ToArray();
        _events = new Event[maxEvents];
    }

    internal nint _ptr;
    private readonly UsbEp[] _eps;
    private readonly Event[] _events;

    public Span<Event> ReadEvents()
    {
        int count = Aio.ReadEvents(_ptr);
        Span<Event> events = _events.AsSpan(0, count);
        for (int i = 0; i < events.Length; i++)
        {
            Aio.GetEventData(_ptr, i, out Aio.EventData eventData);
            events[i] = new(eventData.Buf, eventData.Offset, eventData.Result, i, GetEpFromFd(eventData.Fd));
        }

        return events;
    }

    public void PrepareRead(in Event @event)
    {
        Aio.PrepareRead(_ptr, @event.Index);
    }

    public void PrepareWrite(in Event @event)
    {
        Aio.PrepareWrite(_ptr, @event.Index);
    }

    public void Submit()
    {
        Aio.Submit(_ptr); // TODO: handle if not all iocbs are submitted (return value)
    }

    private UsbEp GetEpFromFd(int fd)
    {
        UsbEp[] eps = _eps;
        for (int i = 0; i < eps.Length; i++)
        {
            if (eps[i].FileHandle.DangerousGetHandle() == fd)
            {
                return _eps[i];
            }
        }

        throw new Exception("Invalid ep handle");
    }

    public void Dispose()
    {
        Aio.Free(_ptr);
        _ptr = 0;
    }

    [StructLayout(LayoutKind.Auto)]
    public readonly struct Event
    {
        internal Event(byte* buffer, int offset, int result, int index, UsbEp ep)
        {
            if (result < 0)
            {
                _buffer = null;
                Offset = 0;
            }
            else
            {
                _buffer = buffer;
                Offset = offset;
            }

            _result = result;
            Index = index;
            Ep = ep;
        }

        private readonly byte* _buffer;
        private readonly int _result;

        public int Offset { get; }

        public Span<byte> Buffer
        {
            get
            {
                if (_result < 0)
                {
                    Aio.ThrowErrno(_result);
                }

                return new(_buffer + Offset, _result);
            }
        }

        public Span<byte> FullBuffer
        {
            get
            {
                if (_result < 0)
                {
                    Aio.ThrowErrno(_result);
                }

                return new(_buffer, _result + Offset);
            }
        }

        internal int Index { get; }

        public UsbEp Ep { get; }
    }
}
