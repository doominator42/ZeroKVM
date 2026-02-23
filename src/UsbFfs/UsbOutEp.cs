using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace UsbFfs;

public sealed class UsbOutEp : UsbEp
{
    internal UsbOutEp(string gadgetPath, int address)
        : base(File.OpenHandle(
            path: Path.Combine(gadgetPath, FormattableString.Invariant($"ep{address}")),
            mode: FileMode.Open,
            access: FileAccess.Read))
    { }

    public int Read(Span<byte> buffer)
    {
        nint result = LibC.Read(FileHandle, buffer);
        if (result < 0)
        {
            ThrowReadError(result);
        }

        return (int)result;

        [DoesNotReturn]
        static void ThrowReadError(nint result)
        {
            throw new ErrnoIOException((int)result, "Read error: ");
        }
    }

    public void ReadExactly(Span<byte> buffer)
    {
        do
        {
            int count = Read(buffer);
            if (count == 0 && buffer.Length > 0)
            {
                ThrowEndOfStream();
            }

            buffer = buffer.Slice(count);
        }
        while (buffer.Length > 0);

        [DoesNotReturn]
        static void ThrowEndOfStream()
        {
            throw new EndOfStreamException();
        }
    }
}
