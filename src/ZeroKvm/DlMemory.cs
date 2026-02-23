using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace ZeroKvm;

internal class DlMemory
{
    private const int MaxPixels = 1152 * 2048;
    private const int RegisterMemoryOffset = 0xc300;

    public DlMemory()
    {
        _ram = GC.AllocateArray<byte>(64 * 1024, true);
        _frameBuffer = GC.AllocateUninitializedArray<byte>((16 * 1024 * 1024) + 256, true); // 256 more bytes to allow overflowing commands
        MemoryMarshal.Cast<byte, ushort>(_frameBuffer.AsSpan()).Fill(0b0000011111100000);
        _frameBufferDiff16 = GC.AllocateArray<ushort>(MaxPixels, true);
        _frameBufferDiff8 = GC.AllocateArray<byte>(MaxPixels, true);
    }

    private readonly byte[] _ram;
    public Span<byte> Ram => _ram;

    public RgbColorDepth ColorDepth => (RgbColorDepth)_ram[RegisterMemoryOffset + (int)DlRegisterAddress.ColorDepth];

    public bool BlankOutput => _ram[RegisterMemoryOffset + (int)DlRegisterAddress.BlankOutput] != 0;

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
        // TODO: properly handle different line strides for 16 and 8 bits buffers
        int lineStride = _fb16LineStride / 2;
        if (lineStride <= 0)
        {
            return default;
        }

        int width = _horizontalResolution;
        int height = _verticalResolution;
        ReadOnlySpan<ushort> fb16 = MemoryMarshal.Cast<byte, ushort>(_frameBuffer.AsSpan(_fb16BaseOffset, lineStride * height * 2));
        var (modifiedX1, modifiedY1, modifiedX2, modifiedY2) = ColorDepth == RgbColorDepth.Rgb24Bits ?
            CopyPixels24(
                fb16,
                _frameBufferDiff16,
                lineStride,
                _frameBuffer.AsSpan(_fb8BaseOffset, lineStride * height),
                _frameBufferDiff8,
                _fb8LineStride,
                fb) :
            CopyPixels16(
                fb16,
                _frameBufferDiff16,
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

    public FrameArea CopyFrameBuffer16To(Span<uint> fb)
    {
        int lineStride = _fb16LineStride / 2;
        if (lineStride <= 0)
        {
            return default;
        }

        int width = _horizontalResolution;
        int height = _verticalResolution;
        var (modifiedX1, modifiedY1, modifiedX2, modifiedY2) = CopyPixels16(
            MemoryMarshal.Cast<byte, ushort>(_frameBuffer.AsSpan(_fb16BaseOffset, lineStride * height * 2)),
            _frameBufferDiff16,
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

    private static (int X1, int Y1, int X2, int Y2) CopyPixels16(
        ReadOnlySpan<ushort> source,
        Span<ushort> sourceDiff,
        int lineStride,
        Span<uint> destination)
    {
        ArgumentOutOfRangeException.ThrowIfZero(source.Length);
        ArgumentOutOfRangeException.ThrowIfLessThan(sourceDiff.Length, source.Length);
        ArgumentOutOfRangeException.ThrowIfLessThan(destination.Length, source.Length);
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
        for (int i = 0; i < length; i += lineStride)
        {
            (int lineX1, int lineX2) = CopyLine16(
                ref Unsafe.Add(ref sourceRef, i),
                ref Unsafe.Add(ref sourceDiffRef, i),
                ref Unsafe.Add(ref destinationRef, i),
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
        Span<uint> destination)
    {
        // TODO
        Console.WriteLine($"{nameof(CopyPixels24)}({source16.Length}, {sourceDiff16.Length}, {lineStride16}, {source8.Length}, {sourceDiff8.Length}, {lineStride8}, {destination.Length})");
        throw new NotImplementedException();
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
