using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;

namespace ZeroKvm;

// TODO: Rgb565LeToRgb332 (8 bits color depth)
internal static class ColorConvert
{
    /// <summary>
    /// Returns true if every LE pixel in <paramref name="leDestination"/> already equals
    /// the byte-swapped form of the corresponding BE pixel in <paramref name="beSource"/>,
    /// i.e. the destination is already up-to-date and no write is needed.
    /// </summary>
    public static bool SpanMatchesBe(ref ushort beSource, ref ushort leDestination, int count)
    {
        ref ushort src = ref beSource;
        ref ushort dst = ref leDestination;
        ref ushort srcEnd = ref Unsafe.Add(ref src, count);

        if (AdvSimd.IsSupported)
        {
            ref ushort srcVec128End = ref Unsafe.Subtract(ref srcEnd, count % Vector128<ushort>.Count);

            while (Unsafe.IsAddressLessThan(ref src, ref srcVec128End))
            {
                Vector128<ushort> srcVec = Unsafe.As<ushort, Vector128<ushort>>(ref src);
                Vector128<ushort> dstVec = Unsafe.As<ushort, Vector128<ushort>>(ref dst);
                if (!Vector128.EqualsAll(AdvSimd.ReverseElement8(srcVec), dstVec))
                {
                    return false;
                }

                src = ref Unsafe.Add(ref src, Vector128<ushort>.Count);
                dst = ref Unsafe.Add(ref dst, Vector128<ushort>.Count);
            }

            // Handle 4-element tail via Vector64
            if (!Unsafe.IsAddressGreaterThan(ref Unsafe.Add(ref src, Vector64<ushort>.Count), ref srcEnd))
            {
                Vector64<ushort> srcVec = Unsafe.As<ushort, Vector64<ushort>>(ref src);
                Vector64<ushort> srcReversed = AdvSimd.ReverseElement8(srcVec);
                Vector64<ushort> dstVec = Unsafe.As<ushort, Vector64<ushort>>(ref dst);
                if (Unsafe.As<Vector64<ushort>, ulong>(ref srcReversed) != Unsafe.As<Vector64<ushort>, ulong>(ref dstVec))
                {
                    return false;
                }

                src = ref Unsafe.Add(ref src, Vector64<ushort>.Count);
                dst = ref Unsafe.Add(ref dst, Vector64<ushort>.Count);
            }
        }

        // Scalar tail (or full scalar on non-NEON)
        while (Unsafe.IsAddressLessThan(ref src, ref srcEnd))
        {
            if (dst != BinaryPrimitives.ReverseEndianness(src))
            {
                return false;
            }

            src = ref Unsafe.Add(ref src, 1);
            dst = ref Unsafe.Add(ref dst, 1);
        }

        return true;
    }

    /// <summary>
    /// Returns true if every element in <paramref name="span"/> equals <paramref name="value"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool SpanAllEqual(ref ushort span, int count, ushort value)
    {
        return MemoryMarshal.CreateReadOnlySpan(ref span, count).IndexOfAnyExcept(value) < 0;
    }

    /// <summary>
    /// Returns true if every element in <paramref name="span"/> equals <paramref name="value"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool SpanAllEqual(ref byte span, int count, byte value)
    {
        return MemoryMarshal.CreateReadOnlySpan(ref span, count).IndexOfAnyExcept(value) < 0;
    }


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
        if (!AdvSimd.Arm64.IsSupported)
        {
            ref uint scalarDestination = ref Unsafe.As<Vector256<uint>, uint>(ref destination);
            for (int i = 0; i < Vector128<ushort>.Count; i++)
            {
                Unsafe.Add(ref scalarDestination, i) = Rgb565LeToRgbx(rgb565.GetElement(i));
            }

            return;
        }

        Vector128<ushort> redBlue = AdvSimd.ShiftRightAndInsert(rgb565 << 8, rgb565, 11);
        redBlue = (redBlue.AsByte() << 3).AsUInt16();
        Vector128<ushort> green = AdvSimd.ShiftLeftLogicalWideningLower(AdvSimd.ShiftRightLogicalNarrowingLower(rgb565, 5), 2);

        AdvSimd.Arm64.StoreVectorAndZip((byte*)Unsafe.AsPointer(ref destination), (redBlue.AsByte(), green.AsByte()));
    }

    public static unsafe void CopyRgb565LeToRgbx(ref ushort source, ref uint destination, nint count)
    {
        if (!AdvSimd.Arm64.IsSupported)
        {
            ref ushort sourceEndScalar = ref Unsafe.Add(ref source, count);
            while (Unsafe.IsAddressLessThan(ref source, ref sourceEndScalar))
            {
                ushort sourcePixel = source;
                source = ref Unsafe.Add(ref source, 1);
                destination = Rgb565LeToRgbx(sourcePixel);
                destination = ref Unsafe.Add(ref destination, 1);
            }

            return;
        }

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
        if (!AdvSimd.IsSupported)
        {
            ref ushort sourceEndScalar = ref Unsafe.Add(ref source, count);
            ushort pixelScalar = 0;

            while (Unsafe.IsAddressLessThan(ref source, ref sourceEndScalar))
            {
                pixelScalar = source;
                source = ref Unsafe.Add(ref source, 1);
                destination = BinaryPrimitives.ReverseEndianness(pixelScalar);
                destination = ref Unsafe.Add(ref destination, 1);
            }

            return pixelScalar;
        }

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
