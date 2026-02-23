using System.Buffers;
using System.Runtime.InteropServices;

namespace UsbFfs;

internal static class ArrayBufferWriterExtensions
{
    public static void WriteAsBytes<T>(this ArrayBufferWriter<byte> buffer, in T value)
        where T : unmanaged
    {
        buffer.Write(MemoryMarshal.AsBytes(new ReadOnlySpan<T>(in value)));
    }
}
