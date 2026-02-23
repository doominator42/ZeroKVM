using System.Runtime.InteropServices;

namespace UsbFfs;

public readonly struct UsbDescriptor
{
    public static UsbDescriptor Create<T>(scoped in T descriptor)
        where T : unmanaged
    {
        return new(MemoryMarshal.AsBytes(new ReadOnlySpan<T>(in descriptor)));
    }

    private UsbDescriptor(ReadOnlySpan<byte> rawBytes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(rawBytes.Length, 2);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(rawBytes.Length, 255);
        ArgumentOutOfRangeException.ThrowIfNotEqual(rawBytes.Length, rawBytes[0]);

        _rawBytes = rawBytes.ToArray();
    }

    private readonly byte[] _rawBytes;

    public ReadOnlyMemory<byte> RawBytes => _rawBytes;
}
