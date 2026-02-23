using System.Runtime.InteropServices;

namespace ZeroKvm.Hid;

internal class HidDescriptorBuilder
{
    public HidDescriptorBuilder(int capacity)
    {
        _buffer = new byte[capacity];
        _reports = new(2);
    }

    private readonly byte[] _buffer;
    private int _size;
    private int _maxmimumReportLength;
    private readonly List<Type> _reports;

    public HidDescriptorBuilder BeginMouseCollection()
    {
        Add([
            0x05, 0x01, // Usage Page (Generic Desktop Ctrls)
            0x09, 0x02, // Usage (Mouse)
            0xa1, 0x01, // Collection (Application)
        ]);

        return this;
    }

    public HidDescriptorBuilder AddReport<T>()
        where T : IHidReport
    {
        if (T.IsBootDescriptor)
        {
            throw new InvalidOperationException("Cannot compose boot descriptors");
        }

        ReadOnlySpan<byte> descriptor = T.Descriptor;
        T.SetReportId(Add(descriptor), (byte)(_reports.Count + 1));
        _reports.Add(typeof(T));
        _maxmimumReportLength = Math.Max(_maxmimumReportLength, T.ReportLength);

        return this;
    }

    private Span<byte> Add(ReadOnlySpan<byte> bytes)
    {
        Span<byte> buffer = _buffer;
        int size = _size;
        bytes.CopyTo(buffer.Slice(size));
        _size = size + bytes.Length;
        return buffer.Slice(size, bytes.Length);
    }

    public HidDescriptorBuilder EndCollection()
    {
        _buffer[_size] = 0xc0;
        _size++;

        return this;
    }

    public HidDescriptor Build()
    {
        return new(_buffer.AsSpan(0, _size), CollectionsMarshal.AsSpan(_reports), _maxmimumReportLength);
    }
}
