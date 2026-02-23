using System.IO;
using System.Runtime.InteropServices;

namespace UsbFfs;

public class ErrnoIOException : IOException
{
    public ErrnoIOException(int errno, string prefixMessage = "")
        : base(prefixMessage + Marshal.GetPInvokeErrorMessage(Math.Abs(errno)))
    {
        Errno = Math.Abs(errno);
    }

    public int Errno { get; }
}
