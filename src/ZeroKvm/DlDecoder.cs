using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace ZeroKvm;

internal static class DlDecoder
{
    private const byte CommandHeader = 0xaf;

    private enum Commands : ushort
    {
        SetRegister = CommandHeader | (0x20 << 8),
        Write8 = CommandHeader | (0x60 << 8),
        Write16 = CommandHeader | (0x68 << 8),
        Fill8 = CommandHeader | (0x61 << 8),
        Fill16 = CommandHeader | (0x69 << 8),
        Copy8 = CommandHeader | (0x62 << 8),
        Copy16 = CommandHeader | (0x6a << 8),
        WriteRlx8 = CommandHeader | (0x63 << 8),
        WriteRlx16 = CommandHeader | (0x6b << 8),
        WriteComp8 = CommandHeader | (0x70 << 8),
        WriteComp16 = CommandHeader | (0x78 << 8),
        FlushPipe = CommandHeader | (0xa0 << 8),
        LoadDecompTable = CommandHeader | (0xe0 << 8),
        NoOp = CommandHeader | (CommandHeader << 8),
        TrailingZero = CommandHeader << 8,
        TrailingDoubleZero = 0,
    }

    public static int Process(ReadOnlySpan<byte> commandStream, DlMemory memory)
    {
        ref byte streamStart = ref Unsafe.AsRef(in commandStream[0]);
        ref byte stream = ref streamStart;
        ref byte streamEnd = ref Unsafe.Add(ref stream, commandStream.Length);
        ref byte fb = ref MemoryMarshal.GetReference(memory.FrameBuffer);
        ref DlMemory.DecompLookupEntry decompTable8Lookup = ref Unsafe.NullRef<DlMemory.DecompLookupEntry>();
        ref byte decompTable8Colors = ref Unsafe.NullRef<byte>();
        ref DlMemory.DecompLookupEntry decompTable16Lookup = ref Unsafe.NullRef<DlMemory.DecompLookupEntry>();
        ref ushort decompTable16Colors = ref Unsafe.NullRef<ushort>();

        try
        {
            while (Unsafe.ByteOffset(ref stream, ref streamEnd) >= 2)
            {
                int commandLength;
                ushort header = Unsafe.As<byte, ushort>(ref stream);
                ref byte commandStart = ref Unsafe.Add(ref stream, 2);
                switch ((Commands)header)
                {
                    case Commands.WriteRlx8:
                        commandLength = WriteRlx8(ref commandStart, ref streamEnd, ref fb);
                        break;

                    case Commands.WriteRlx16:
                        commandLength = WriteRlx16(ref commandStart, ref streamEnd, ref fb);
                        break;

                    case Commands.WriteComp8:
                        if (Unsafe.IsNullRef(ref decompTable8Lookup))
                        {
                            decompTable8Lookup = ref GetArrayRef(memory.DecompTable8Lookup);
                            decompTable8Colors = ref GetArrayRef(memory.DecompTable8Colors);
                        }

                        commandLength = WriteComp8(ref commandStart, ref streamEnd, ref fb, in decompTable8Lookup, in decompTable8Colors);
                        break;

                    case Commands.WriteComp16:
                        if (Unsafe.IsNullRef(ref decompTable16Lookup))
                        {
                            decompTable16Lookup = ref GetArrayRef(memory.DecompTable16Lookup);
                            decompTable16Colors = ref GetArrayRef(memory.DecompTable16Colors);
                        }

                        commandLength = WriteComp16(ref commandStart, ref streamEnd, ref fb, in decompTable16Lookup, in decompTable16Colors);
                        break;

                    case Commands.FlushPipe:
                    case Commands.TrailingDoubleZero:
                        stream = ref Unsafe.Add(ref stream, 2);
                        continue;

                    case Commands.NoOp:
                    case Commands.TrailingZero:
                        stream = ref Unsafe.Add(ref stream, 1);
                        continue;

                    default:
                        commandLength = ProcessOther(header, ref streamStart, ref commandStart, ref streamEnd, memory);
                        if (commandLength < 0)
                        {
                            return commandStream.Length;
                        }

                        break;
                }

                if (commandLength == 0)
                {
                    return (int)Unsafe.ByteOffset(ref streamStart, ref stream);
                }
                else
                {
                    stream = ref Unsafe.Add(ref commandStart, commandLength);
                }
            }
        }
        catch
        {
            PrintCommandError("Error in command", ref streamStart, ref stream, ref streamEnd);
            throw;
        }

        return (int)Unsafe.ByteOffset(ref streamStart, ref stream);

        [MethodImpl(MethodImplOptions.NoInlining)]
        static int ProcessOther(ushort header, ref byte streamStart, ref byte commandStart, ref byte streamEnd, DlMemory memory)
        {
            ref byte fb = ref MemoryMarshal.GetReference(memory.FrameBuffer);
            switch ((Commands)header)
            {
                case Commands.SetRegister:
                    return SetRegister(ref commandStart, ref streamEnd, memory);

                case Commands.Write8:
                    return Write8(ref commandStart, ref streamEnd, ref fb);

                case Commands.Write16:
                    return Write16(ref commandStart, ref streamEnd, ref fb);

                case Commands.Fill8:
                    return Fill8(ref commandStart, ref streamEnd, ref fb);

                case Commands.Fill16:
                    return Fill16(ref commandStart, ref streamEnd, ref fb);

                case Commands.Copy8:
                    return Copy8(ref commandStart, ref streamEnd, ref fb);

                case Commands.Copy16:
                    return Copy16(ref commandStart, ref streamEnd, ref fb);

                case Commands.LoadDecompTable:
                    return LoadDecompTable(ref commandStart, ref streamEnd, memory);

                default:
                    PrintCommandError("Unknown command header", ref streamStart, ref Unsafe.Subtract(ref commandStart, 2), ref streamEnd);
                    return -1;
            }
        }

        static void PrintCommandError(string message, ref byte streamStart, ref byte stream, ref byte streamEnd)
        {
            ReadOnlySpan<byte> before = MemoryMarshal.CreateReadOnlySpan(in streamStart, (int)Unsafe.ByteOffset(ref streamStart, ref stream));
            ReadOnlySpan<byte> data = MemoryMarshal.CreateReadOnlySpan(in stream, (int)Unsafe.ByteOffset(ref stream, ref streamEnd));
            ushort header = Unsafe.As<byte, ushort>(ref stream);
            Logger.LogError($"""
                {message}: {header & 0xff:x2} {header >> 8:x2}
                Previous data: {Convert.ToHexStringLower(before.Length > 1024 ? before[^1024..] : before)}
                Data: {Convert.ToHexStringLower(data.Length > 1024 ? data[0..1024] : data)}
                """);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static ref T GetArrayRef<T>(T[]? array)
        {
            return ref array![0];
        }
    }

    /*
    address: uint8
    value: uint8
    */
    private static int SetRegister(ref byte stream, ref byte streamEnd, DlMemory memory)
    {
        if (Unsafe.ByteOffset(ref stream, ref streamEnd) < 2)
        {
            return 0;
        }

        ref byte streamStart = ref stream;
        ushort word = Unsafe.As<byte, ushort>(ref stream);
        stream = ref Unsafe.Add(ref stream, 2);
        memory.SetRegister((byte)word, (byte)(word >> 8));

        ref byte streamDwordEnd = ref Unsafe.Subtract(ref streamEnd, Unsafe.ByteOffset(ref stream, ref streamEnd) % 4);
        while (Unsafe.IsAddressLessThan(ref stream, ref streamDwordEnd))
        {
            uint dword = Unsafe.As<byte, uint>(ref stream);
            stream = ref Unsafe.Add(ref stream, 4);
            if ((dword & 0xffffUL) != (uint)Commands.SetRegister)
            {
                stream = ref Unsafe.Subtract(ref stream, 4);
                break;
            }

            memory.SetRegister((byte)(dword >> 16), (byte)(dword >> 24));
        }

        return (int)Unsafe.ByteOffset(ref streamStart, ref stream);
    }

    /*
    target_offset: uint24_be
    pixel_count: uint8_wrap256
    {
        pixel: uint8_rgb323
    } repeat until pixel_count is rendered
    */
    private static int Write8(ref byte stream, ref byte streamEnd, ref byte fb)
    {
        if (Unsafe.ByteOffset(ref stream, ref streamEnd) < 5)
        {
            return 0;
        }

        uint dword = Unsafe.As<byte, uint>(ref stream);
        stream = ref Unsafe.Add(ref stream, 4);

        int address = UInt24BeLsbToInt32(dword);
        int pixelCount = Wrap256((int)(dword >> 24));

        if (Unsafe.ByteOffset(ref stream, ref streamEnd) < pixelCount)
        {
            return 0;
        }

        MemoryMarshal.CreateReadOnlySpan(in stream, pixelCount)
            .TryCopyTo(MemoryMarshal.CreateSpan(ref Unsafe.Add(ref fb, address), pixelCount));

        return 4 + pixelCount;
    }

    /*
    target_offset: uint24_be
    pixel_count: uint8_wrap256
    {
        pixel: uint16_rgb565
    } repeat pixel_count times
    */
    private static int Write16(ref byte stream, ref byte streamEnd, ref byte fb)
    {
        if (Unsafe.ByteOffset(ref stream, ref streamEnd) < 5)
        {
            return 0;
        }

        uint dword = Unsafe.As<byte, uint>(ref stream);
        stream = ref Unsafe.Add(ref stream, 4);

        int address = UInt24BeLsbToInt32(dword);
        int pixelCount = Wrap256((int)(dword >> 24));

        if (Unsafe.ByteOffset(ref stream, ref streamEnd) < pixelCount * sizeof(ushort))
        {
            return 0;
        }

        ColorConvert.CopyRgb565BeToRgb565Le(
            ref Unsafe.As<byte, ushort>(ref stream),
            ref Unsafe.As<byte, ushort>(ref Unsafe.Add(ref fb, address)),
            pixelCount);

        return 4 + (pixelCount * sizeof(ushort));
    }

    /*
    target_offset: uint24_be
    total_pixel_count: uint8_wrap256
    {
        pixel_count: uint8_wrap256
        pixel: uint8_rgb323
    } repeat until total_pixel_count is rendered
    */
    private static int Fill8(ref byte stream, ref byte streamEnd, ref byte fb)
    {
        if (Unsafe.ByteOffset(ref stream, ref streamEnd) < 6)
        {
            return 0;
        }

        ref byte streamStart = ref stream;
        uint dword = Unsafe.As<byte, uint>(ref stream);
        stream = ref Unsafe.Add(ref stream, 4);

        int address = UInt24BeLsbToInt32(dword);
        int totalCount = Wrap256((int)(dword >> 24));
        ref byte fbPixels = ref Unsafe.Add(ref fb, address);
        do
        {
            if (Unsafe.ByteOffset(ref stream, ref streamEnd) < 2)
            {
                return 0;
            }

            uint word = Unsafe.As<byte, ushort>(ref stream);
            stream = ref Unsafe.Add(ref stream, 2);
            int pixelCount = Wrap256((byte)word);
            MemoryMarshal.CreateSpan(ref fbPixels, pixelCount).Fill((byte)(word >> 8));

            fbPixels = ref Unsafe.Add(ref fbPixels, pixelCount);
            totalCount -= pixelCount;
        }
        while (totalCount > 0);

        return (int)Unsafe.ByteOffset(ref streamStart, ref stream);
    }

    /*
    target_offset: uint24_be
    total_pixel_count: uint8_wrap256
    {
        pixel_count: uint8_wrap256
        pixel: uint16_rgb565be
    } repeat until total_pixel_count is rendered
    */
    private static int Fill16(ref byte stream, ref byte streamEnd, ref byte fb)
    {
        if (Unsafe.ByteOffset(ref stream, ref streamEnd) < 7)
        {
            return 0;
        }

        ref byte streamStart = ref stream;
        uint dword = Unsafe.As<byte, uint>(ref stream);
        stream = ref Unsafe.Add(ref stream, 4);

        int address = UInt24BeLsbToInt32(dword);
        int totalCount = Wrap256((int)(dword >> 24));
        ref ushort fbPixels = ref Unsafe.As<byte, ushort>(ref Unsafe.Add(ref fb, address));
        do
        {
            if (Unsafe.ByteOffset(ref stream, ref streamEnd) < 3)
            {
                return 0;
            }

            int pixelCount = Wrap256(stream);
            stream = ref Unsafe.Add(ref stream, 1);
            ushort pixelValue = Unsafe.As<byte, ushort>(ref stream);
            stream = ref Unsafe.Add(ref stream, 2);
            MemoryMarshal.CreateSpan(ref fbPixels, pixelCount).Fill(pixelValue);

            fbPixels = ref Unsafe.Add(ref fbPixels, pixelCount);
            totalCount -= pixelCount;
        }
        while (totalCount > 0);

        return (int)Unsafe.ByteOffset(ref streamStart, ref stream);
    }

    /*
    target_offset: uint24_be
    pixel_count: uint8_wrap256
    source_offset: uint24_be
    */
    private static int Copy8(ref byte stream, ref byte streamEnd, ref byte fb)
    {
        if (Unsafe.ByteOffset(ref stream, ref streamEnd) < 7)
        {
            return 0;
        }

        ulong qword = BinaryPrimitives.ReverseEndianness(Unsafe.As<byte, ulong>(ref Unsafe.Subtract(ref stream, 1)));
        int targetAddress = (int)((qword >> 32) & 0xffffffU);
        int count = Wrap256((int)((qword >> 24) & 0xffU));
        int sourceAddress = (int)(qword & 0xffffffUL);
        if (targetAddress != sourceAddress)
        {
            MemoryMarshal.CreateSpan(ref Unsafe.Add(ref fb, sourceAddress), count)
                .TryCopyTo(MemoryMarshal.CreateSpan(ref Unsafe.Add(ref fb, targetAddress), count));
        }

        return 7;
    }

    /*
    target_offset: uint24_be
    pixel_count: uint8_wrap256
    source_offset: uint24_be
    */
    private static int Copy16(ref byte stream, ref byte streamEnd, ref byte fb)
    {
        if (Unsafe.ByteOffset(ref stream, ref streamEnd) < 7)
        {
            return 0;
        }

        ulong qword = BinaryPrimitives.ReverseEndianness(Unsafe.As<byte, ulong>(ref Unsafe.Subtract(ref stream, 1)));
        int targetAddress = (int)((qword >> 32) & 0xffffffU);
        int count = Wrap256((int)((qword >> 24) & 0xffU));
        int sourceAddress = (int)(qword & 0xffffffUL);
        if (targetAddress != sourceAddress)
        {
            count *= sizeof(ushort);
            MemoryMarshal.CreateSpan(ref Unsafe.Add(ref fb, sourceAddress), count)
                .TryCopyTo(MemoryMarshal.CreateSpan(ref Unsafe.Add(ref fb, targetAddress), count));
        }

        return 7;
    }

    /*
    offset: uint24_be
    total_pixel_count: uint8_wrap256
    {
        pixel_count: uint8_wrap256
        {
            pixel: uint8_rgb323
        } * pixel_count
        last_pixel_repeat_count: uint8 (omitted for the last chunk if all pixels are rendered)
    } repeat until total_pixel_count is rendered
    */
    private static int WriteRlx8(ref byte stream, ref byte streamEnd, ref byte fb)
    {
        if (Unsafe.ByteOffset(ref stream, ref streamEnd) < 6)
        {
            return 0;
        }

        ref byte streamStart = ref stream;
        uint header = Unsafe.As<byte, uint>(ref stream);
        stream = ref Unsafe.Add(ref stream, 4);

        int address = UInt24BeLsbToInt32(header);
        int totalPixelCount = Wrap256((int)(header >> 24));
        ref byte fbPixels = ref Unsafe.Add(ref fb, address);
        do
        {
            if (Unsafe.ByteOffset(ref stream, ref streamEnd) < 2)
            {
                return 0;
            }

            int pixelCount = stream;
            stream = ref Unsafe.Add(ref stream, 1);
            pixelCount = Wrap256(pixelCount);
            if (Unsafe.ByteOffset(ref stream, ref streamEnd) < pixelCount)
            {
                return 0;
            }

            MemoryMarshal.CreateSpan(ref stream, pixelCount)
                .TryCopyTo(MemoryMarshal.CreateSpan(ref fbPixels, pixelCount));
            stream = ref Unsafe.Add(ref stream, pixelCount);
            fbPixels = ref Unsafe.Add(ref fbPixels, pixelCount);

            totalPixelCount -= pixelCount;
            if (totalPixelCount > 0)
            {
                if (!Unsafe.IsAddressLessThan(ref stream, ref streamEnd))
                {
                    return 0;
                }

                byte repeat = stream;
                stream = ref Unsafe.Add(ref stream, 1);
                if (repeat > 0)
                {
                    MemoryMarshal.CreateSpan(ref fbPixels, repeat).Fill(Unsafe.Add(ref stream, -2));
                    fbPixels = ref Unsafe.Add(ref fbPixels, repeat);
                    totalPixelCount -= repeat;
                }
            }
        }
        while (totalPixelCount > 0);

        return (int)Unsafe.ByteOffset(ref streamStart, ref stream);
    }

    /*
    offset: uint24_be
    total_pixel_count: uint8_wrap256
    {
        pixel_count: uint8_wrap256
        {
            pixel: uint16_rgb565
        } * pixel_count
        last_pixel_repeat_count: uint8 (omitted for the last chunk if all pixels are rendered)
    } repeat until total_pixel_count is rendered
    */
    private static int WriteRlx16(ref byte stream, ref byte streamEnd, ref byte fb)
    {
        if (Unsafe.ByteOffset(ref stream, ref streamEnd) < 7)
        {
            return 0;
        }

        ref byte streamStart = ref stream;
        uint header = Unsafe.As<byte, uint>(ref stream);
        stream = ref Unsafe.Add(ref stream, 4);

        int address = UInt24BeLsbToInt32(header);
        int totalPixelCount = Wrap256((int)(header >> 24));
        ref ushort fbPixels = ref Unsafe.As<byte, ushort>(ref Unsafe.Add(ref fb, address));
        do
        {
            int pixelCount = stream;
            stream = ref Unsafe.Add(ref stream, 1);
            pixelCount = Wrap256(pixelCount);
            if (Unsafe.IsAddressGreaterThan(ref Unsafe.Add(ref stream, pixelCount * 2), ref streamEnd))
            {
                break;
            }

            ushort lastPixel = ColorConvert.CopyRgb565BeToRgb565Le(ref Unsafe.As<byte, ushort>(ref stream), ref fbPixels, pixelCount);
            stream = ref Unsafe.Add(ref stream, pixelCount * 2);
            fbPixels = ref Unsafe.Add(ref fbPixels, pixelCount);

            totalPixelCount -= pixelCount;
            if (totalPixelCount <= 0 || !Unsafe.IsAddressLessThan(ref stream, ref streamEnd))
            {
                break;
            }

            byte repeat = stream;
            stream = ref Unsafe.Add(ref stream, 1);
            if (repeat > 0)
            {
                MemoryMarshal.CreateSpan(ref fbPixels, repeat).Fill(BinaryPrimitives.ReverseEndianness(lastPixel));
                fbPixels = ref Unsafe.Add(ref fbPixels, repeat);
                totalPixelCount -= repeat;
                if (totalPixelCount <= 0)
                {
                    break;
                }
            }
        }
        while (Unsafe.IsAddressLessThan(ref stream, ref streamEnd));

        return totalPixelCount > 0 ? 0 : (int)Unsafe.ByteOffset(ref streamStart, ref stream);
    }

    /*
    offset: uint24_be
    pixel_count: uint8_wrap256
    {
        table_lookup: bit
    } repeat until pixel_count is rendered
    */
    private static int WriteComp8(ref byte stream, ref byte streamEnd, ref byte fb, in DlMemory.DecompLookupEntry decompTable, in byte decompColors)
    {
        if (Unsafe.ByteOffset(ref stream, ref streamEnd) < 5)
        {
            return 0;
        }

        ref byte streamStart = ref stream;
        uint header = Unsafe.As<byte, uint>(ref stream);
        stream = ref Unsafe.Add(ref stream, 4);

        int address = UInt24BeLsbToInt32(header);
        uint pixelCount = Wrap256(header >> 24);
        ulong tableIndex = 0;
        ref byte fbPixels = ref Unsafe.Add(ref fb, address);
        ulong pixelBuf = default;
        uint pixelBufLength = 0;
        byte accumulator = 0;
        do
        {
            byte bits = stream;
            stream = ref Unsafe.Add(ref stream, 1);

            nuint lookupIndex = (nuint)((tableIndex << 8) | bits);
            DlMemory.DecompLookupEntry entry = Unsafe.Add(ref Unsafe.AsRef(in decompTable), lookupIndex);

            uint colorCount = entry.ColorCount;
            tableIndex = entry.Jump;
            if (pixelCount < colorCount)
            {
                colorCount = pixelCount;
            }

            pixelCount -= colorCount;

            Vector64<byte> entryColorsVector;
            if (colorCount >= 4)
            {
                entryColorsVector = Unsafe.Add(ref Unsafe.As<byte, Vector64<byte>>(ref Unsafe.AsRef(in decompColors)), lookupIndex);
            }
            else
            {
                entryColorsVector = Vector64.Create(Unsafe.As<Vector64<byte>, uint>(ref Unsafe.Add(ref Unsafe.As<byte, Vector64<byte>>(ref Unsafe.AsRef(in decompColors)), lookupIndex))).AsByte();
            }

            entryColorsVector += Vector64.Create(accumulator);
            accumulator = entryColorsVector.GetElement(7);
            ulong entryColors = entryColorsVector.AsUInt64().ToScalar() & ((1UL << ((int)colorCount * 8)) - 1);
            pixelBuf |= entryColors << (int)(pixelBufLength * 8);
            pixelBufLength += colorCount;
            if (pixelBufLength >= 8)
            {
                Unsafe.As<byte, ulong>(ref fbPixels) = pixelBuf;
                fbPixels = ref Unsafe.Add(ref fbPixels, 8);
                pixelBufLength -= 8;
                pixelBuf = entryColors >> (int)((colorCount - pixelBufLength) * 8);
            }
        }
        while (pixelCount > 0 && Unsafe.IsAddressLessThan(ref stream, ref streamEnd));

        if (pixelCount > 0)
        {
            return 0;
        }

        if (pixelBufLength >= 4)
        {
            Unsafe.As<byte, uint>(ref fbPixels) = (uint)pixelBuf;
            fbPixels = ref Unsafe.Add(ref fbPixels, 4);
            pixelBuf >>= 32;
            pixelBufLength -= 4;
        }

        if (pixelBufLength >= 2)
        {
            Unsafe.As<byte, ushort>(ref fbPixels) = (ushort)pixelBuf;
            fbPixels = ref Unsafe.Add(ref fbPixels, 2);
            pixelBuf >>= 16;
            pixelBufLength -= 2;
        }

        if (pixelBufLength == 1)
        {
            fbPixels = (byte)pixelBuf;
            fbPixels = ref Unsafe.Add(ref fbPixels, 1);
        }

        return (int)Unsafe.ByteOffset(ref streamStart, ref stream);
    }

    /*
    offset: uint24_be
    pixel_count: uint8_wrap256
    {
        table_lookup: bit
    } repeat until pixel_count is rendered
    */
    private static int WriteComp16(ref byte stream, ref byte streamEnd, ref byte fb, in DlMemory.DecompLookupEntry decompTable, in ushort decompColors)
    {
        if (Unsafe.ByteOffset(ref stream, ref streamEnd) < 5)
        {
            return 0;
        }

        ref byte streamStart = ref stream;
        uint header = Unsafe.As<byte, uint>(ref stream);
        stream = ref Unsafe.Add(ref stream, 4);

        int address = UInt24BeLsbToInt32(header);
        uint pixelCount = Wrap256(header >> 24);
        ulong tableIndex = 8;
        ushort accumulator = 0;
        ref ushort fbPixels = ref Unsafe.As<byte, ushort>(ref Unsafe.Add(ref fb, address));
        do
        {
            byte bits = stream;
            stream = ref Unsafe.Add(ref stream, 1);
            nuint lookupIndex = (nuint)((tableIndex << 8) | bits);
            DlMemory.DecompLookupEntry entry = Unsafe.Add(ref Unsafe.AsRef(in decompTable), lookupIndex);

            uint colorCount = entry.ColorCount;
            tableIndex = entry.Jump;
            if (pixelCount < colorCount)
            {
                colorCount = pixelCount;
            }

            pixelCount -= colorCount;

            /*ulong entryColors;
            if (colorCount >= 4)
            {
                Vector128<ushort> entryColorsVector = Unsafe.Add(ref Unsafe.As<ushort, Vector128<ushort>>(ref Unsafe.AsRef(in decompColors)), lookupIndex);
                entryColorsVector += Vector128.Create(accumulator);
                accumulator = entryColorsVector.GetElement(7);

                if (colorCount == 8)
                {
                    Unsafe.As<ushort, Vector128<ushort>>(ref fbPixels) = entryColorsVector;
                    fbPixels = ref Unsafe.Add(ref fbPixels, 8);
                    continue;
                }

                Unsafe.As<ushort, ulong>(ref fbPixels) = entryColorsVector.AsUInt64().GetElement(0);
                fbPixels = ref Unsafe.Add(ref fbPixels, 4);
                if (colorCount == 4)
                {
                    continue;
                }

                colorCount -= 4;
                entryColors = entryColorsVector.AsUInt64().GetElement(1);
            }
            else if (colorCount == 0)
            {
                accumulator += Unsafe.As<Vector128<ushort>, ushort>(ref Unsafe.Add(ref Unsafe.As<ushort, Vector128<ushort>>(ref Unsafe.AsRef(in decompColors)), lookupIndex));
                continue;
            }
            else
            {
                Vector64<ushort> entryColorsVector = Unsafe.As<Vector128<ushort>, Vector64<ushort>>(ref Unsafe.Add(ref Unsafe.As<ushort, Vector128<ushort>>(ref Unsafe.AsRef(in decompColors)), lookupIndex));
                entryColorsVector += Vector64.Create(accumulator);
                accumulator = entryColorsVector.GetElement(3);

                entryColors = entryColorsVector.AsUInt64().ToScalar();
            }

            if (colorCount >= 2)
            {
                Unsafe.As<ushort, uint>(ref fbPixels) = (uint)entryColors;
                fbPixels = ref Unsafe.Add(ref fbPixels, 2);
                entryColors >>= 32;
                colorCount -= 2;
            }

            if (colorCount == 1)
            {
                fbPixels = (ushort)entryColors;
                fbPixels = ref Unsafe.Add(ref fbPixels, 1);
            }*/

            if (colorCount == 8)
            {
                Vector128<ushort> entryColors = Unsafe.Add(ref Unsafe.As<ushort, Vector128<ushort>>(ref Unsafe.AsRef(in decompColors)), lookupIndex);
                entryColors += Vector128.Create(accumulator);
                Unsafe.As<ushort, Vector128<ushort>>(ref fbPixels) = entryColors;
                fbPixels = ref Unsafe.Add(ref fbPixels, Vector128<ushort>.Count);
                accumulator = entryColors.GetElement(7);
            }
            else
            {
                ref ushort entryColorsRef = ref Unsafe.As<Vector128<ushort>, ushort>(ref Unsafe.Add(ref Unsafe.As<ushort, Vector128<ushort>>(ref Unsafe.AsRef(in decompColors)), lookupIndex));
                ref ushort entryColorsRefEnd = ref Unsafe.Add(ref entryColorsRef, colorCount);
                while (Unsafe.IsAddressLessThan(ref entryColorsRef, ref entryColorsRefEnd))
                {
                    ushort entryColor = entryColorsRef;
                    entryColorsRef = ref Unsafe.Add(ref entryColorsRef, 1);
                    fbPixels = (ushort)(accumulator + entryColor);
                    fbPixels = ref Unsafe.Add(ref fbPixels, 1);
                }

                accumulator += entryColorsRef;
            }
        }
        while (pixelCount > 0 && Unsafe.IsAddressLessThan(ref stream, ref streamEnd));

        if (pixelCount > 0)
        {
            return 0;
        }

        return (int)Unsafe.ByteOffset(ref streamStart, ref stream);
    }

    /*
    header: 26 38 71 CD
    padding: uint16
    length: uint16_be
    {
        colorA: uint16_rgb565be
        repeatA: uint8
        unknownA: uint3_msb
        jumpA_msb: uint5_lsb
        jumpA_lsb: uint4_msb
        jumpB_lsb: uint4_lsb
        colorB: uint16_rgb565be
        repeatB: uint8
        unknownB: uint3_msb
        jumpB_msb: uint5_lsb
    } * length
    */
    private static int LoadDecompTable(ref byte stream, ref byte streamEnd, DlMemory memory)
    {
        if (Unsafe.ByteOffset(ref stream, ref streamEnd) < 8)
        {
            return 0;
        }

        uint header = Unsafe.As<byte, uint>(ref stream);
        stream = ref Unsafe.Add(ref stream, 6);
        if (header != 0xcd713826U)
        {
            Logger.LogError($"{nameof(LoadDecompTable)}: unknown header {BinaryPrimitives.ReverseEndianness(header):x8}");
        }

        int length = BinaryPrimitives.ReverseEndianness(Unsafe.As<byte, ushort>(ref stream));
        stream = ref Unsafe.Add(ref stream, 2);

        ArgumentOutOfRangeException.ThrowIfLessThan(length, 16);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(length, 4096);
        if (Unsafe.ByteOffset(ref stream, ref streamEnd) < length * DecompEntry.ByteLength)
        {
            return 0;
        }

        DecompEntry[] table = new DecompEntry[length * 2];
        ReadOnlySpan<byte> tableBytes = MemoryMarshal.CreateReadOnlySpan(in stream, length * DecompEntry.ByteLength);
        for (int i = 0; i < length; i++)
        {
            (table[i * 2], table[(i * 2) + 1]) = DecompEntry.Parse(
                tableBytes.Slice(i * DecompEntry.ByteLength, DecompEntry.ByteLength),
                length);
        }

        const int LookupBitCount = 8;
        ReallocArray(ref memory.DecompTable8Lookup, length * (1 << LookupBitCount));
        ReallocArray(ref memory.DecompTable8Colors, length * (1 << LookupBitCount) * LookupBitCount);
        ReallocArray(ref memory.DecompTable16Lookup, length * (1 << LookupBitCount));
        ReallocArray(ref memory.DecompTable16Colors, length * (1 << LookupBitCount) * LookupBitCount);
        BuildTableLookup(table, memory.DecompTable8Lookup, memory.DecompTable8Colors, 0, 0);
        BuildTableLookup(table, memory.DecompTable16Lookup, memory.DecompTable16Colors, 8, 8);

        return 8 + (length * DecompEntry.ByteLength);

        static void ReallocArray<T>([NotNull] ref T[]? array, int length)
            where T : unmanaged
        {
            if (array is null || array.Length != length)
            {
                array = GC.AllocateArray<T>(length, true);
            }
            else
            {
                array.AsSpan().Clear();
            }
        }

        static void BuildTableLookup<T>(DecompEntry[] table, DlMemory.DecompLookupEntry[] tableLookup, T[] tableColors, uint tableIndex, uint startIndex)
            where T : unmanaged, IBinaryInteger<T>
        {
            Span<DlMemory.DecompLookupEntry> subLookup = tableLookup.AsSpan((int)tableIndex * (1 << LookupBitCount), 1 << LookupBitCount);
            if (subLookup[0].IsSet)
            {
                return;
            }

            Span<T> subColors = tableColors.AsSpan((int)tableIndex * (1 << LookupBitCount) * LookupBitCount, (1 << LookupBitCount) * LookupBitCount);
            for (int i = 0; i < subLookup.Length; i++)
            {
                DlMemory.DecompLookupEntry entry = Lookup(
                    table,
                    subColors.Slice(i * LookupBitCount, LookupBitCount),
                    tableIndex,
                    startIndex,
                    (uint)i,
                    LookupBitCount);

                subLookup[i] = entry;
                if (entry.Jump != 0 && entry.Jump != startIndex)
                {
                    BuildTableLookup<T>(table, tableLookup, tableColors, entry.Jump, startIndex);
                }
            }
        }

        static DlMemory.DecompLookupEntry Lookup<T>(DecompEntry[] table, Span<T> colors, uint tableIndex, uint startIndex, uint bits, uint bitCount)
            where T : unmanaged, IBinaryInteger<T>
        {
            uint accumulator = 0;
            int colorCount = 0;
            do
            {
                do
                {
                    DecompEntry entry = table[(tableIndex << 1) + (bits & 1)];
                    accumulator += entry.Color;
                    tableIndex = entry.Jump;
                    bits >>= 1;
                    bitCount--;
                }
                while (tableIndex != 0 && bitCount != 0);

                if (tableIndex == 0)
                {
                    colors[colorCount] = T.CreateTruncating(accumulator);
                    colorCount++;
                    tableIndex = startIndex;
                }
            }
            while (bitCount != 0);

            colors[colorCount..LookupBitCount].Fill(T.CreateTruncating(accumulator));
            return new((ushort)colorCount, (ushort)tableIndex);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Wrap256(int value)
    {
        return value == 0 ? 256 : value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint Wrap256(uint value)
    {
        return value == 0 ? 256 : value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int UInt24BeLsbToInt32(uint dword)
    {
        return (int)BinaryPrimitives.ReverseEndianness(dword << 8);
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly struct DecompEntry
    {
        public const int ByteLength = 9;

        public static (DecompEntry, DecompEntry) Parse(ReadOnlySpan<byte> bytes, int totalEntries)
        {
            ArgumentOutOfRangeException.ThrowIfNotEqual(bytes.Length, ByteLength);

            ushort colorA = BinaryPrimitives.ReadUInt16BigEndian(bytes);
            ushort jumpA = (ushort)(((bytes[3] & 0x1fU) << 4) | ((uint)bytes[4] >> 4));

            ushort colorB = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(5));
            ushort jumpB = (ushort)(((bytes[8] & 0x1fU) << 4) | (bytes[4] & 0xfU));

            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(jumpA, totalEntries);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(jumpB, totalEntries);

            return (new(colorA, jumpA), new(colorB, jumpB));
        }

        private DecompEntry(ushort color, ushort jump)
        {
            Color = color;
            Jump = jump;
        }

        public readonly ushort Color;
        public readonly ushort Jump;
    }
}
