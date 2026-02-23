using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace UsbFfs;

internal static unsafe partial class Aio
{
    private const string LibraryName = "aio-wrapper";

    public static nint Init(int epCount, int maxEvents)
    {
        nint ctx = (nint)ffs_aio_init(epCount, maxEvents);
        if (ctx < 0)
        {
            ThrowErrno((int)ctx);
        }

        return ctx;
    }

    [LibraryImport(LibraryName)]
    private static partial void* ffs_aio_init(int ep_count, int max_events);

    public static void InitEp(nint ctx, int epIndex, SafeFileHandle file, int bufCount, int bufSize, int bufOffset, bool isWrite)
    {
        int ret = ffs_aio_init_ep((void*)ctx, epIndex, (int)file.DangerousGetHandle(), bufCount, bufSize, bufOffset, isWrite ? 1 : 0);
        if (ret != 0)
        {
            ThrowErrno(ret);
        }
    }

    [LibraryImport(LibraryName)]
    private static partial int ffs_aio_init_ep(void* ctx, int ep_index, int fd, int buf_count, int buf_size, int buf_offset, int is_write);

    public static int ReadEvents(nint ctx)
    {
        int ret = ffs_aio_read_events((void*)ctx);
        if (ret < 0)
        {
            ThrowErrno(ret);
        }

        return ret;
    }

    [LibraryImport(LibraryName)]
    private static partial int ffs_aio_read_events(void* ctx);

    public static void GetEventData(nint ctx, int eventIndex, out EventData data)
    {
        data = default;
        ffs_aio_event_data((void*)ctx, eventIndex, (EventData*)Unsafe.AsPointer(ref data));
    }

    [LibraryImport(LibraryName)]
    private static partial void ffs_aio_event_data(void* ctx, int event_index, EventData* data);

    public static void PrepareRead(nint ctx, int eventIndex)
    {
        ffs_aio_prep_read((void*)ctx, eventIndex);
    }

    [LibraryImport(LibraryName)]
    private static partial void ffs_aio_prep_read(void* ctx, int event_index);

    public static void PrepareWrite(nint ctx, int eventIndex)
    {
        ffs_aio_prep_write((void*)ctx, eventIndex);
    }

    [LibraryImport(LibraryName)]
    private static partial void ffs_aio_prep_write(void* ctx, int event_index);

    public static int Submit(nint ctx)
    {
        int ret = ffs_aio_submit((void*)ctx);
        if (ret < 0)
        {
            ThrowErrno(ret);
        }

        return ret;
    }

    [LibraryImport(LibraryName)]
    private static partial int ffs_aio_submit(void* ctx);

    public static void Free(nint ctx)
    {
        ffs_aio_free((void*)ctx);
    }

    [LibraryImport(LibraryName)]
    private static partial void ffs_aio_free(void* ctx);

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public ref struct EventData
    {
        public int Fd;
        public int Result;
        public int Offset;
        public byte* Buf;
    }

    [DoesNotReturn]
    public static void ThrowErrno(int errno)
    {
        throw new ErrnoIOException(errno);
    }
}
