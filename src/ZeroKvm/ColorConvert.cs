using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;

namespace ZeroKvm;

// TODO: Rgb565LeToRgb332 (8 bits color depth)
internal static class ColorConvert
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Rgb565LeToRgbx(ushort value)
    {
        return (((uint)value << 19) | // blue
            (((uint)value << 5) & (0b11111100U << 8)) | // green
            ((uint)value >> 8)) & // red
            0b111110001111110011111000U;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void Rgb565LeToRgbx(Vector128<ushort> rgb565, ref Vector256<uint> destination)
    {
        Vector128<ushort> redBlue = AdvSimd.ShiftRightAndInsert(rgb565 << 8, rgb565, 11);
        redBlue = (redBlue.AsByte() << 3).AsUInt16();
        Vector128<ushort> green = AdvSimd.ShiftLeftLogicalWideningLower(AdvSimd.ShiftRightLogicalNarrowingLower(rgb565, 5), 2);

        AdvSimd.Arm64.StoreVectorAndZip((byte*)Unsafe.AsPointer(ref destination), (redBlue.AsByte(), green.AsByte()));
    }

    public static unsafe void CopyRgb565LeToRgbx(ref ushort source, ref uint destination, nint count)
    {
        ref ushort sourceEnd = ref Unsafe.Add(ref source, count);
        ref ushort sourceVectorEnd = ref Unsafe.Add(ref source, count - (count % Vector128<ushort>.Count));
        while (Unsafe.IsAddressLessThan(ref source, ref sourceVectorEnd))
        {
            Vector128<ushort> sourceValues = Unsafe.As<ushort, Vector128<ushort>>(ref source);
            source = ref Unsafe.Add(ref source, Vector128<ushort>.Count);

            Vector128<ushort> redBlue = AdvSimd.ShiftRightAndInsert(sourceValues << 8, sourceValues, 11);
            redBlue = (redBlue.AsByte() << 3).AsUInt16();
            Vector128<ushort> green = AdvSimd.ShiftLeftLogicalWideningLower(AdvSimd.ShiftRightLogicalNarrowingLower(sourceValues, 5), 2);

            AdvSimd.Arm64.StoreVectorAndZip((byte*)Unsafe.AsPointer(ref destination), (redBlue.AsByte(), green.AsByte()));
            destination = ref Unsafe.Add(ref destination, Vector128<uint>.Count * 2);
        }

        while (Unsafe.IsAddressLessThan(ref source, ref sourceEnd))
        {
            ushort sourcePixel = source;
            source = ref Unsafe.Add(ref source, 1);
            destination = Rgb565LeToRgbx(sourcePixel);
            destination = ref Unsafe.Add(ref destination, 1);
        }
    }

    public static ushort CopyRgb565BeToRgb565Le(ref ushort source, ref ushort destination, nint count)
    {
        ref ushort sourceEnd = ref Unsafe.Add(ref source, count);
        ref ushort sourceVector128End = ref Unsafe.Subtract(ref sourceEnd, count % Vector128<ushort>.Count);
        Vector128<ushort> pixels128 = default;
        while (Unsafe.IsAddressLessThan(ref source, ref sourceVector128End))
        {
            pixels128 = Unsafe.As<ushort, Vector128<ushort>>(ref source);
            source = ref Unsafe.Add(ref source, Vector128<ushort>.Count);
            Unsafe.As<ushort, Vector128<ushort>>(ref destination) = AdvSimd.ReverseElement8(pixels128);
            destination = ref Unsafe.Add(ref destination, Vector128<ushort>.Count);
        }

        ushort pixel = pixels128.GetElement(7);
        if (!Unsafe.IsAddressGreaterThan(ref Unsafe.Add(ref source, Vector64<ushort>.Count), ref sourceEnd))
        {
            Vector64<ushort> pixels = Unsafe.As<ushort, Vector64<ushort>>(ref source);
            source = ref Unsafe.Add(ref source, Vector64<ushort>.Count);
            Unsafe.As<ushort, Vector64<ushort>>(ref destination) = AdvSimd.ReverseElement8(pixels);
            destination = ref Unsafe.Add(ref destination, Vector64<ushort>.Count);
            pixel = pixels.GetElement(3);
        }

        while (Unsafe.IsAddressLessThan(ref source, ref sourceEnd))
        {
            pixel = source;
            source = ref Unsafe.Add(ref source, 1);
            destination = BinaryPrimitives.ReverseEndianness(pixel);
            destination = ref Unsafe.Add(ref destination, 1);
        }

        return pixel;
    }
}
