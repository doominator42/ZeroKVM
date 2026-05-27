using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;

namespace ZeroKvm;

internal class DlMemory
{
    private const int MaxPixels = 1152 * 2048;
    private const int RegisterMemoryOffset = 0xc300;

    public DlMemory()
    {
        _ram = GC.AllocateArray<byte>(64 * 1024, true);
        _frameBuffer = GC.AllocateUninitializedArray<byte>((20 * 1024 * 1024) + 256, true); // 256 more bytes to allow overflowing commands
        MemoryMarshal.Cast<byte, ushort>(_frameBuffer.AsSpan()).Fill(0b0000011111100000);
        _frameBufferDiff16 = GC.AllocateArray<ushort>(MaxPixels, true);
        _frameBufferDiff8 = GC.AllocateArray<byte>(MaxPixels, true);
    }

    private readonly byte[] _ram;
    public Span<byte> Ram => _ram;

    public RgbColorDepth ColorDepth => (RgbColorDepth)_ram[RegisterMemoryOffset + (int)DlRegisterAddress.ColorDepth];

    public bool BlankOutput => _ram[RegisterMemoryOffset + (int)DlRegisterAddress.BlankOutput] != 0;
    public int HorizontalResolution => _horizontalResolution;
    public int VerticalResolution => _verticalResolution;
    public int FrameBuffer16BaseOffset => _fb16BaseOffset;
    public int FrameBuffer16LineStride => _fb16LineStride;
    public int FrameBuffer8BaseOffset => _fb8BaseOffset;
    public int FrameBuffer8LineStride => _fb8LineStride;

    private int _horizontalResolution;
    private int _verticalResolution;
    private int _fb16BaseOffset;
    private int _fb16LineStride;
    private int _fb8BaseOffset;
    private int _fb8LineStride;

    private readonly byte[] _frameBuffer;
    public Span<byte> FrameBuffer => _frameBuffer;

    private readonly ushort[] _frameBufferDiff16;
    private readonly byte[] _frameBufferDiff8;

    public DecompLookupEntry[]? DecompTable8Lookup;
    public byte[]? DecompTable8Colors;
    public DecompLookupEntry[]? DecompTable16Lookup;
    public ushort[]? DecompTable16Colors;

    public event Action? RegistersUpdate;
    public long LastRegistersUpdateTimestamp;

    public void SetRegister(byte address, byte value)
    {
        ref byte registers = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_ram), RegisterMemoryOffset);
        if (address == (byte)DlRegisterAddress.RegistersUpdate)
        {
            Unsafe.Add(ref registers, (nint)DlRegisterAddress.RegistersUpdate) = value;
            if (value != 0)
            {
                ApplyRegisters(ref registers);
                RegistersUpdate?.Invoke();
                LastRegistersUpdateTimestamp = Stopwatch.GetTimestamp();
            }
        }
        else
        {
            Unsafe.Add(ref registers, address) = value;
            if (Unsafe.Add(ref registers, (nint)DlRegisterAddress.RegistersUpdate) != 0)
            {
                ApplyRegisters(ref registers);
            }
        }
    }

    private void ApplyRegisters(ref byte registers)
    {
        int horizontalResolution = ReadUInt16Be(ref Unsafe.Add(ref registers, (nint)DlRegisterAddress.HPixels));
        _horizontalResolution = horizontalResolution;
        _verticalResolution = ReadUInt16Be(ref Unsafe.Add(ref registers, (nint)DlRegisterAddress.VPixels));
        _fb16BaseOffset = ReadUInt24Be(ref Unsafe.Add(ref registers, (nint)DlRegisterAddress.BaseOffset16));
        int fb16LineStride = ReadUInt24Be(ref Unsafe.Add(ref registers, (nint)DlRegisterAddress.LineStride16));
        _fb16LineStride = fb16LineStride == 0 ? horizontalResolution * 2 : fb16LineStride;
        _fb8BaseOffset = ReadUInt24Be(ref Unsafe.Add(ref registers, (nint)DlRegisterAddress.BaseOffset8));
        int fb8LineStride = ReadUInt24Be(ref Unsafe.Add(ref registers, (nint)DlRegisterAddress.LineStride8));
        _fb8LineStride = fb8LineStride == 0 ? horizontalResolution : fb8LineStride;

        static int ReadUInt16Be(ref byte buf)
        {
            return BinaryPrimitives.ReverseEndianness(Unsafe.As<byte, ushort>(ref buf));
        }

        static int ReadUInt24Be(ref byte buf)
        {
            return (int)(BinaryPrimitives.ReverseEndianness(Unsafe.As<byte, uint>(ref buf)) >> 8);
        }
    }

    public FrameArea CopyFrameBufferTo(Span<uint> fb)
    {
        int lineStride = _fb16LineStride / 2;
        return CopyFrameBufferTo(fb, lineStride);
    }

    public FrameArea CopyFrameBufferTo(Span<uint> fb, int stridePixels)
    {
        // TODO: properly handle different line strides for 16 and 8 bits buffers
        int lineStride = _fb16LineStride / 2;
        if (lineStride <= 0 || stridePixels <= 0)
        {
            return default;
        }

        int width = _horizontalResolution;
        int height = _verticalResolution;
        int fb16Size = lineStride * height * 2;
        if (_fb16BaseOffset < 0 || (long)_fb16BaseOffset + fb16Size > _frameBuffer.Length)
        {
            return default;
        }
        ArgumentOutOfRangeException.ThrowIfLessThan(fb.Length, checked(stridePixels * height));

        ReadOnlySpan<ushort> fb16 = MemoryMarshal.Cast<byte, ushort>(_frameBuffer.AsSpan(_fb16BaseOffset, fb16Size));
        int modifiedX1, modifiedY1, modifiedX2, modifiedY2;
        if (ColorDepth == RgbColorDepth.Rgb24Bits)
        {
            int fb8Size = lineStride * height;
            if (_fb8BaseOffset < 0 || (long)_fb8BaseOffset + fb8Size > _frameBuffer.Length)
            {
                return default;
            }
            (modifiedX1, modifiedY1, modifiedX2, modifiedY2) = CopyPixels24(
                fb16,
                _frameBufferDiff16,
                lineStride,
                _frameBuffer.AsSpan(_fb8BaseOffset, fb8Size),
                _frameBufferDiff8,
                _fb8LineStride,
                stridePixels,
                fb);
        }
        else
        {
            (modifiedX1, modifiedY1, modifiedX2, modifiedY2) = CopyPixels16(
                fb16,
                _frameBufferDiff16,
                lineStride,
                stridePixels,
                fb);
        }

        return new()
        {
            Width = (ushort)width,
            Height = (ushort)height,
            LineStride = (ushort)lineStride,
            ModifiedX1 = (ushort)modifiedX1,
            ModifiedY1 = (ushort)modifiedY1,
            ModifiedX2 = (ushort)modifiedX2,
            ModifiedY2 = (ushort)modifiedY2,
        };
    }

    public FrameArea CopyFrameBuffer16To(Span<uint> fb)
    {
        int lineStride = _fb16LineStride / 2;
        if (lineStride <= 0)
        {
            return default;
        }

        int width = _horizontalResolution;
        int height = _verticalResolution;
        int fb16Size = lineStride * height * 2;
        if (_fb16BaseOffset < 0 || (long)_fb16BaseOffset + fb16Size > _frameBuffer.Length)
        {
            return default;
        }
        var (modifiedX1, modifiedY1, modifiedX2, modifiedY2) = CopyPixels16(
            MemoryMarshal.Cast<byte, ushort>(_frameBuffer.AsSpan(_fb16BaseOffset, fb16Size)),
            _frameBufferDiff16,
            lineStride,
            lineStride,
            fb);

        return new()
        {
            Width = (ushort)width,
            Height = (ushort)height,
            LineStride = (ushort)lineStride,
            ModifiedX1 = (ushort)modifiedX1,
            ModifiedY1 = (ushort)modifiedY1,
            ModifiedX2 = (ushort)modifiedX2,
            ModifiedY2 = (ushort)modifiedY2,
        };
    }

    public void CopyFrameBuffer16To(Span<ushort> fb, int stridePixels)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(stridePixels, 1);

        int lineStride = _fb16LineStride / 2;
        int width = _horizontalResolution;
        int height = _verticalResolution;
        if (lineStride <= 0 || width <= 0 || height <= 0)
        {
            return;
        }

        int copyWidth = Math.Min(Math.Min(width, stridePixels), lineStride);
        ArgumentOutOfRangeException.ThrowIfLessThan(fb.Length, stridePixels * height);

        int fb16Size = lineStride * height * 2;
        if (_fb16BaseOffset < 0 || (long)_fb16BaseOffset + fb16Size > _frameBuffer.Length)
        {
            return;
        }

        ReadOnlySpan<ushort> source = MemoryMarshal.Cast<byte, ushort>(_frameBuffer.AsSpan(_fb16BaseOffset, fb16Size));
        for (int y = 0; y < height; y++)
        {
            source.Slice(y * lineStride, copyWidth)
                .CopyTo(fb.Slice(y * stridePixels, copyWidth));
        }
    }

    private static (int X1, int Y1, int X2, int Y2) CopyPixels16(
        ReadOnlySpan<ushort> source,
        Span<ushort> sourceDiff,
        int lineStride,
        int destinationStride,
        Span<uint> destination)
    {
        ArgumentOutOfRangeException.ThrowIfZero(source.Length);
        ArgumentOutOfRangeException.ThrowIfLessThan(sourceDiff.Length, source.Length);
        ArgumentOutOfRangeException.ThrowIfLessThan(destination.Length, checked(destinationStride * (source.Length / lineStride)));
        ArgumentOutOfRangeException.ThrowIfLessThan(lineStride, 16);

        int x1 = ushort.MaxValue;
        int y1 = 0;
        int x2 = 0;
        int y2 = 0;
        ref ushort sourceRef = ref MemoryMarshal.GetReference(source);
        ref ushort sourceDiffRef = ref MemoryMarshal.GetReference(sourceDiff);
        ref uint destinationRef = ref MemoryMarshal.GetReference(destination);
        int length = source.Length;
        int vectorLineLength = lineStride - (lineStride % Vector128<ushort>.Count);
        for (int i = 0, y = 0; i < length; i += lineStride, y++)
        {
            ref uint lineDestinationRef = ref Unsafe.Add(ref destinationRef, y * destinationStride);
            (int lineX1, int lineX2) = CopyLine16(
                ref Unsafe.Add(ref sourceRef, i),
                ref Unsafe.Add(ref sourceDiffRef, i),
                ref lineDestinationRef,
                lineStride,
                vectorLineLength);

            if (lineX1 >= 0)
            {
                if (lineX1 < x1)
                {
                    x1 = lineX1;
                }

                if (lineX2 > x2)
                {
                    x2 = lineX2;
                }

                if (y2 == 0)
                {
                    y1 = i / lineStride;
                    y2 = y1 + 1;
                }
                else
                {
                    y2 = (i / lineStride) + 1;
                }
            }
        }

        return (x1, y1, x2, y2);
    }

    private static (int X1, int X2) CopyLine16(ref ushort source, ref ushort sourceDiff, ref uint destination, int lineLength, int vectorLineLength)
    {
        scoped ref ushort diffStart = ref Unsafe.NullRef<ushort>();
        scoped ref ushort diffEnd = ref Unsafe.NullRef<ushort>();
        scoped ref ushort sourceStart = ref source;
        scoped ref ushort sourceVectorEnd = ref Unsafe.Add(ref source, vectorLineLength);

        Vector128<ushort> sourcePixels;

    diffLoop:
        do
        {
            (ulong, ulong) sourcePixels2 = Unsafe.As<ushort, (ulong, ulong)>(ref source);
            source = ref Unsafe.Add(ref source, Vector128<ushort>.Count);
            (ulong, ulong) sourceDiffPixels = Unsafe.As<ushort, (ulong, ulong)>(ref sourceDiff);
            sourceDiff = ref Unsafe.Add(ref sourceDiff, Vector128<ushort>.Count);

            if (sourcePixels2 != sourceDiffPixels)
            {
                source = ref Unsafe.Subtract(ref source, Vector128<ushort>.Count);
                sourceDiff = ref Unsafe.Subtract(ref sourceDiff, Vector128<ushort>.Count);
                sourcePixels = Vector128.Create(sourcePixels2.Item1, sourcePixels2.Item2).AsUInt16();
                goto copyLoop;
            }
        }
        while (Unsafe.IsAddressLessThan(ref source, ref sourceVectorEnd));

        goto remaining;

    copyLoop:
        if (Unsafe.IsNullRef(ref diffStart))
        {
            diffStart = ref source;
        }

        scoped ref Vector256<uint> copyDestination = ref Unsafe.As<uint, Vector256<uint>>(ref Unsafe.AddByteOffset(ref destination, Unsafe.ByteOffset(ref sourceStart, ref source) * 2));
        do
        {
            Unsafe.As<ushort, Vector128<ushort>>(ref sourceDiff) = sourcePixels;
            sourceDiff = ref Unsafe.Add(ref sourceDiff, Vector128<ushort>.Count);
            ColorConvert.Rgb565LeToRgbx(sourcePixels, ref copyDestination);
            copyDestination = ref Unsafe.Add(ref copyDestination, 1);
            source = ref Unsafe.Add(ref source, Vector128<ushort>.Count);
            if (!Unsafe.IsAddressLessThan(ref source, ref sourceVectorEnd))
            {
                diffEnd = ref source;
                goto remaining;
            }

            sourcePixels = Unsafe.As<ushort, Vector128<ushort>>(ref source);
        }
        while (sourcePixels != Unsafe.As<ushort, Vector128<ushort>>(ref sourceDiff));

        diffEnd = ref source;
        goto diffLoop;

    remaining:
        if (lineLength != vectorLineLength)
        {
            nuint offset = (nuint)Vector128<ushort>.Count - (nuint)(lineLength - vectorLineLength);
            sourcePixels = Unsafe.As<ushort, Vector128<ushort>>(ref Unsafe.Subtract(ref source, offset));
            if (sourcePixels != Unsafe.As<ushort, Vector128<ushort>>(ref Unsafe.Subtract(ref sourceDiff, offset)))
            {
                Unsafe.As<ushort, Vector128<ushort>>(ref Unsafe.Subtract(ref sourceDiff, offset)) = sourcePixels;
                ColorConvert.Rgb565LeToRgbx(sourcePixels, ref Unsafe.As<uint, Vector256<uint>>(ref Unsafe.Add(ref destination, lineLength - Vector256<uint>.Count)));
                return (
                    Unsafe.IsNullRef(ref diffStart) ? 0 : (int)Unsafe.ByteOffset(ref sourceStart, ref diffStart) / sizeof(ushort),
                    lineLength
                );
            }
        }

        return Unsafe.IsNullRef(ref diffStart) ?
            (-1, -1) :
            (
                (int)Unsafe.ByteOffset(ref sourceStart, ref diffStart) / sizeof(ushort),
                (int)Unsafe.ByteOffset(ref sourceStart, ref diffEnd) / sizeof(ushort)
            );
    }

    private static (int X1, int Y1, int X2, int Y2) CopyPixels24(
        ReadOnlySpan<ushort> source16,
        Span<ushort> sourceDiff16,
        int lineStride16,
        ReadOnlySpan<byte> source8,
        Span<byte> sourceDiff8,
        int lineStride8,
        int destinationStride,
        Span<uint> destination)
    {
        // Combine the 16-bit plane (RGB565, providing MSBs of each channel) with the
        // 8-bit plane (RGB323, providing LSBs).  The per-channel merge is:
        //   R8 = R565<<3 | R323   (5+3 = 8 bits)
        //   G8 = G565<<2 | G323   (6+2 = 8 bits)
        //   B8 = B565<<3 | B323   (5+3 = 8 bits)
        // Output format is XBGR8888 (matching Rgb565LeToRgbx): B at bits 23:16,
        // G at bits 15:8, R at bits 7:0.
        int height = source16.Length / lineStride16;
        int x1 = ushort.MaxValue;
        int y1 = 0;
        int x2 = 0;
        int y2 = 0;
        ArgumentOutOfRangeException.ThrowIfLessThan(destination.Length, checked(destinationStride * height));

        ref ushort src16Ref = ref MemoryMarshal.GetReference(source16);
        ref ushort diff16Ref = ref MemoryMarshal.GetReference(sourceDiff16);
        ref byte src8Ref = ref MemoryMarshal.GetReference(source8);
        ref byte diff8Ref = ref MemoryMarshal.GetReference(sourceDiff8);
        ref uint dstRef = ref MemoryMarshal.GetReference(destination);
        bool useNeon = AdvSimd.Arm64.IsSupported;

        for (int y = 0; y < height; y++)
        {
            int base16 = y * lineStride16;
            int base8 = y * lineStride8;
            int baseDst = y * destinationStride;
            int lineX1 = -1;
            int lineX2 = -1;
            int x = 0;

            if (useNeon)
            {
                for (; x <= lineStride16 - 8; x += 8)
                {
                    ref ushort src16 = ref Unsafe.Add(ref src16Ref, base16 + x);
                    ref ushort diff16 = ref Unsafe.Add(ref diff16Ref, base16 + x);
                    ref byte src8 = ref Unsafe.Add(ref src8Ref, base8 + x);
                    ref byte diff8 = ref Unsafe.Add(ref diff8Ref, base8 + x);

                    Vector128<ushort> v16 = Unsafe.As<ushort, Vector128<ushort>>(ref src16);
                    Vector128<ushort> d16 = Unsafe.As<ushort, Vector128<ushort>>(ref diff16);
                    Vector64<byte> v8 = Unsafe.As<byte, Vector64<byte>>(ref src8);
                    Vector64<byte> d8 = Unsafe.As<byte, Vector64<byte>>(ref diff8);

                    if (Vector128.EqualsAll(v16, d16) &&
                        v8.AsUInt64().ToScalar() == d8.AsUInt64().ToScalar())
                    {
                        continue;
                    }

                    Unsafe.As<ushort, Vector128<ushort>>(ref diff16) = v16;
                    Unsafe.As<byte, Vector64<byte>>(ref diff8) = v8;

                    Vector128<ushort> px8_u16 = AdvSimd.ZeroExtendWideningLower(v8);
                    Vector128<uint> px8_u32_lo = AdvSimd.ZeroExtendWideningLower(px8_u16.GetLower());
                    Vector128<uint> px8_u32_hi = AdvSimd.ZeroExtendWideningLower(px8_u16.GetUpper());

                    // px8 bit layout: r2r1r0 g1g0 b2b1b0 → fill lower bits of each 8-bit channel
                    Vector128<uint> contrib_lo =
                        ((px8_u32_lo >> 5) & Vector128.Create(0x07u)) |
                        (((px8_u32_lo >> 3) & Vector128.Create(0x03u)) << 8) |
                        ((px8_u32_lo & Vector128.Create(0x07u)) << 16);
                    Vector128<uint> contrib_hi =
                        ((px8_u32_hi >> 5) & Vector128.Create(0x07u)) |
                        (((px8_u32_hi >> 3) & Vector128.Create(0x03u)) << 8) |
                        ((px8_u32_hi & Vector128.Create(0x07u)) << 16);

                    Vector256<uint> rgbx = default;
                    ColorConvert.Rgb565LeToRgbx(v16, ref rgbx);
                    Vector128<uint> rgbxLo = Unsafe.As<Vector256<uint>, Vector128<uint>>(ref rgbx);
                    Vector128<uint> rgbxHi = Unsafe.Add(ref Unsafe.As<Vector256<uint>, Vector128<uint>>(ref rgbx), 1);

                    Unsafe.As<uint, Vector128<uint>>(ref Unsafe.Add(ref dstRef, baseDst + x)) = rgbxLo | contrib_lo;
                    Unsafe.As<uint, Vector128<uint>>(ref Unsafe.Add(ref dstRef, baseDst + x + 4)) = rgbxHi | contrib_hi;

                    if (lineX1 < 0) lineX1 = x;
                    lineX2 = x + 8;
                }
            }

            // Scalar path for tail (and full loop on non-NEON)
            for (; x < lineStride16; x++)
            {
                ushort px16 = Unsafe.Add(ref src16Ref, base16 + x);
                byte px8 = Unsafe.Add(ref src8Ref, base8 + x);

                if (px16 != Unsafe.Add(ref diff16Ref, base16 + x) ||
                    px8 != Unsafe.Add(ref diff8Ref, base8 + x))
                {
                    Unsafe.Add(ref diff16Ref, base16 + x) = px16;
                    Unsafe.Add(ref diff8Ref, base8 + x) = px8;

                    uint r8 = (((uint)px16 >> 8) & 0xF8u) | ((uint)px8 >> 5);
                    uint g8 = (((uint)px16 >> 3) & 0xFCu) | (((uint)px8 >> 3) & 0x03u);
                    uint b8 = (((uint)px16 << 3) & 0xF8u) | ((uint)px8 & 0x07u);
                    Unsafe.Add(ref dstRef, baseDst + x) = (b8 << 16) | (g8 << 8) | r8;

                    if (lineX1 < 0) lineX1 = x;
                    lineX2 = x + 1;
                }
            }

            if (lineX1 >= 0)
            {
                if (lineX1 < x1) x1 = lineX1;
                if (lineX2 > x2) x2 = lineX2;
                if (y2 == 0)
                {
                    y1 = y;
                    y2 = y + 1;
                }
                else
                {
                    y2 = y + 1;
                }
            }
        }

        return (x1, y1, x2, y2);
    }

    public readonly struct DecompLookupEntry
    {
        public DecompLookupEntry(ushort colorCount, ushort jump)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(colorCount, (1 << 4) - 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(jump, (1 << 12) - 1);
            _value = (ushort)(colorCount | ((uint)jump << 4));
        }

        private readonly ushort _value;

        public uint ColorCount => _value & 0xfU;
        public uint Jump => (uint)_value >> 4;

        public bool IsSet => _value != 0;
    }
}
