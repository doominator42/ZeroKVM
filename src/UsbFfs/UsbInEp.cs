using System.Diagnostics.CodeAnalysis;
using System.IO;
using Microsoft.Win32.SafeHandles;

namespace UsbFfs;

public sealed class UsbInEp : UsbEp
{
    internal UsbInEp(string gadgetPath, int address)
        : base(File.OpenHandle(
            path: Path.Combine(gadgetPath, FormattableString.Invariant($"ep{address}")),
            mode: FileMode.Open,
            access: FileAccess.Write))
    { }

    public void Write(ReadOnlySpan<byte> buffer)
    {
        Write(FileHandle, buffer);
    }

    internal static void Write(SafeFileHandle handle, ReadOnlySpan<byte> buffer)
    {
        int totalWritten = 0;
        do
        {
            nint result = LibC.Write(handle, buffer[totalWritten..]);
            if (result < 0)
            {
                ThrowWriteError(result);
            }
            else if (result == 0 && buffer.Length > 0)
            {
                ThrowEndOfStream();
            }

            totalWritten += (int)result;
        }
        while (totalWritten < buffer.Length);

        [DoesNotReturn]
        static void ThrowEndOfStream()
        {
            throw new EndOfStreamException();
        }

        [DoesNotReturn]
        static void ThrowWriteError(nint result)
        {
            throw new ErrnoIOException((int)result, "Write error: ");
        }
    }
}
