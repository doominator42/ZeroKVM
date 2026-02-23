using System.Numerics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace UsbFfs;

internal static unsafe partial class LibC
{
    private const string LibraryName = "libc";

    public static nint Read(SafeFileHandle handle, Span<byte> buffer)
    {
        fixed (byte* buf = &MemoryMarshal.GetReference(buffer))
        {
            return ResultOrErrno(read((int)handle.DangerousGetHandle(), buf, (nuint)buffer.Length));
        }
    }

    [LibraryImport(LibraryName, SetLastError = true)]
    private static partial nint read(int fd, byte* buf, nuint count);

    public static nint Write(SafeFileHandle handle, ReadOnlySpan<byte> buffer)
    {
        fixed (byte* buf = &MemoryMarshal.GetReference(buffer))
        {
            return ResultOrErrno(write((int)handle.DangerousGetHandle(), buf, (nuint)buffer.Length));
        }
    }

    [LibraryImport(LibraryName, SetLastError = true)]
    private static partial nint write(int fd, byte* buf, nuint count);

    private static T ResultOrErrno<T>(T result)
        where T : IBinaryInteger<T>
    {
        if (result < T.Zero)
        {
            int errno = Marshal.GetLastPInvokeError();
            if (errno != 0)
            {
                return T.CreateTruncating(-Math.Abs(errno));
            }
        }

        return result;
    }
}
