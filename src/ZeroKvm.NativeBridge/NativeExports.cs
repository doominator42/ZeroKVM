using System.Runtime.InteropServices;

namespace ZeroKvm;

internal sealed class NativeBridgeContext
{
    public NativeBridgeContext(int maxWidth, int maxHeight)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxWidth, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxHeight, 1);

        MaxWidth = maxWidth;
        MaxHeight = maxHeight;
        Memory = new DlMemory();
    }

    public int MaxWidth { get; }

    public int MaxHeight { get; }

    public DlMemory Memory { get; }
}

[StructLayout(LayoutKind.Sequential)]
public struct NativeFrameArea
{
    public ushort Width;
    public ushort Height;
    public ushort LineStride;
    public ushort ModifiedX1;
    public ushort ModifiedY1;
    public ushort ModifiedX2;
    public ushort ModifiedY2;
}

public static unsafe class NativeExports
{
    [UnmanagedCallersOnly(EntryPoint = "zerokvm_udl_create")]
    public static nint Create(uint maxWidth, uint maxHeight)
    {
        try
        {
            GCHandle handle = GCHandle.Alloc(new NativeBridgeContext(checked((int)maxWidth), checked((int)maxHeight)));
            return GCHandle.ToIntPtr(handle);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex);
            return 0;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "zerokvm_udl_destroy")]
    public static void Destroy(nint handle)
    {
        if (handle == 0)
        {
            return;
        }

        try
        {
            GCHandle.FromIntPtr(handle).Free();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex);
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "zerokvm_udl_process")]
    public static int Process(nint handle, byte* data, nuint length)
    {
        if (handle == 0 || data is null)
        {
            return -1;
        }

        try
        {
            NativeBridgeContext context = GetContext(handle);
            return DlDecoder.Process(new ReadOnlySpan<byte>(data, checked((int)length)), context.Memory);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex);
            return -1;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "zerokvm_udl_copy_framebuffers")]
    public static int CopyFramebuffers(nint handle,
                                       ushort* rgb565,
                                       uint rgb565StridePixels,
                                       uint* xrgb8888,
                                       uint xrgb8888StridePixels,
                                       NativeFrameArea* area)
    {
        if (handle == 0)
        {
            return -1;
        }

        try
        {
            NativeBridgeContext context = GetContext(handle);
            DlMemory memory = context.Memory;
            FrameArea frameArea = default;

            if (xrgb8888 is not null && xrgb8888StridePixels != 0)
            {
                Span<uint> xrgb = new(xrgb8888, checked((int)(xrgb8888StridePixels * (uint)context.MaxHeight)));
                frameArea = memory.CopyFrameBufferTo(xrgb);
            }
            else
            {
                frameArea = CreateFullFrameArea(memory);
            }

            if (rgb565 is not null && rgb565StridePixels != 0)
            {
                Span<ushort> rgb = new(rgb565, checked((int)(rgb565StridePixels * (uint)context.MaxHeight)));
                memory.CopyFrameBuffer16To(rgb, checked((int)rgb565StridePixels));
            }

            if (area is not null)
            {
                *area = ToNative(frameArea);
            }

            return 0;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex);
            return -1;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "zerokvm_udl_copy_registers")]
    public static int CopyRegisters(nint handle, byte* registers, nuint length)
    {
        if (handle == 0 || registers is null || length < 256)
        {
            return -1;
        }

        try
        {
            NativeBridgeContext context = GetContext(handle);
            DlMemory memory = context.Memory;
            const int registerMemoryOffset = 0xc300;

            Span<byte> dst = new(registers, 256);
            memory.Ram.Slice(registerMemoryOffset, 256).CopyTo(dst);
            return 0;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex);
            return -1;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "zerokvm_udl_get_color_depth")]
    public static byte GetColorDepth(nint handle)
    {
        return handle == 0 ? (byte)0 : (byte)GetContext(handle).Memory.ColorDepth;
    }

    [UnmanagedCallersOnly(EntryPoint = "zerokvm_udl_get_hpixels")]
    public static ushort GetHPixels(nint handle)
    {
        return handle == 0 ? (ushort)0 : checked((ushort)Math.Max(0, GetContext(handle).Memory.HorizontalResolution));
    }

    [UnmanagedCallersOnly(EntryPoint = "zerokvm_udl_get_vpixels")]
    public static ushort GetVPixels(nint handle)
    {
        return handle == 0 ? (ushort)0 : checked((ushort)Math.Max(0, GetContext(handle).Memory.VerticalResolution));
    }

    [UnmanagedCallersOnly(EntryPoint = "zerokvm_udl_get_base16bpp")]
    public static uint GetBase16Bpp(nint handle)
    {
        return handle == 0 ? 0u : checked((uint)Math.Max(0, GetContext(handle).Memory.FrameBuffer16BaseOffset));
    }

    [UnmanagedCallersOnly(EntryPoint = "zerokvm_udl_get_base8bpp")]
    public static uint GetBase8Bpp(nint handle)
    {
        return handle == 0 ? 0u : checked((uint)Math.Max(0, GetContext(handle).Memory.FrameBuffer8BaseOffset));
    }

    private static NativeBridgeContext GetContext(nint handle)
    {
        return (NativeBridgeContext)(GCHandle.FromIntPtr(handle).Target ?? throw new InvalidOperationException("Bridge context was released"));
    }

    private static NativeFrameArea ToNative(FrameArea area)
    {
        return new NativeFrameArea
        {
            Width = area.Width,
            Height = area.Height,
            LineStride = area.LineStride,
            ModifiedX1 = area.ModifiedX1,
            ModifiedY1 = area.ModifiedY1,
            ModifiedX2 = area.ModifiedX2,
            ModifiedY2 = area.ModifiedY2,
        };
    }

    private static FrameArea CreateFullFrameArea(DlMemory memory)
    {
        int width = Math.Max(0, memory.HorizontalResolution);
        int height = Math.Max(0, memory.VerticalResolution);
        int lineStride = Math.Max(0, memory.FrameBuffer16LineStride / 2);

        return new FrameArea
        {
            Width = checked((ushort)Math.Min(width, ushort.MaxValue)),
            Height = checked((ushort)Math.Min(height, ushort.MaxValue)),
            LineStride = checked((ushort)Math.Min(lineStride, ushort.MaxValue)),
            ModifiedX1 = 0,
            ModifiedY1 = 0,
            ModifiedX2 = checked((ushort)Math.Min(width, ushort.MaxValue)),
            ModifiedY2 = checked((ushort)Math.Min(height, ushort.MaxValue)),
        };
    }
}