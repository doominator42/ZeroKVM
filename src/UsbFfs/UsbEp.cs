using Microsoft.Win32.SafeHandles;

namespace UsbFfs;

public abstract class UsbEp : IDisposable
{
    internal UsbEp(SafeFileHandle fileHandle)
    {
        FileHandle = fileHandle;
    }

    internal SafeFileHandle FileHandle { get; }

    internal int AioBufferCount { get; init; }

    internal int AioBufferSize { get; init; }

    internal int AioBufferOffset { get; init; }

    public void Dispose()
    {
        FileHandle.Dispose();
    }
}
