using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ZeroKvm;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct Edid
{
    public enum BitDepth
    {
        Undefined = 0,
        Six = 1,
        Eight = 2,
        Ten = 3,
        Twelve = 4,
        Fourteen = 5,
        Sixteen = 6,
    }

    public enum InterfaceType
    {
        Undefined = 0,
        Dvi = 1,
        HdmiA = 2,
        HdmiB = 3,
        Mddi = 4,
        DisplayPort = 5,
    }

    public enum AspectRatio
    {
        Aspect16_10 = 0,
        Aspect4_3 = 1,
        Aspect5_4 = 2,
        Aspect16_9 = 3,
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly struct StandardTiming
    {
        public static StandardTiming Unused => new(1, 1);

        private StandardTiming(byte xResolution, byte @params)
        {
            _xResolution = xResolution;
            _params = @params;
        }

        public StandardTiming(int xResolution, AspectRatio aspectRatio, int verticalFrequency)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(xResolution, 256);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(xResolution, 2288);
            ArgumentOutOfRangeException.ThrowIfLessThan(verticalFrequency, 60);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(verticalFrequency, 123);

            _xResolution = (byte)((xResolution / 8) - 31);
            _params = (byte)(((uint)aspectRatio << 6) | ((uint)verticalFrequency - 60));
        }

        private readonly byte _xResolution;
        private readonly byte _params;
        public int XResolution => (_xResolution + 31) * 8;

        public AspectRatio AspectRatio => (AspectRatio)(_params >> 6);

        public int VerticalFrequency => (_params & 0b111111) + 60;

        public bool IsUnused => _xResolution == 1 && _params == 1;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly struct TimingDescriptor
    {
        public ushort PixelClock { get; init; }

        private readonly ulong _bits0;
        private readonly ulong _bits1;

        public int HorizontalActivePixels
        {
            get => (int)(GetBits(_bits0, 0, 8) | (GetBits(_bits0, 20, 4) << 8));
            init
            {
                SetBits(ref _bits0, 0, 8, (ulong)value & 0xff);
                SetBits(ref _bits0, 20, 4, ((ulong)value >> 8) & 0xf);
            }
        }

        public int HorizontalBlankingPixels
        {
            get => (int)(GetBits(_bits0, 8, 8) | (GetBits(_bits0, 16, 4) << 8));
            init
            {
                SetBits(ref _bits0, 8, 8, (ulong)value & 0xff);
                SetBits(ref _bits0, 16, 4, ((ulong)value >> 8) & 0xf);
            }
        }

        public int VerticalActiveLines
        {
            get => (int)(GetBits(_bits0, 24, 8) | (GetBits(_bits0, 44, 4) << 8));
            init
            {
                SetBits(ref _bits0, 24, 8, (ulong)value & 0xff);
                SetBits(ref _bits0, 44, 4, ((ulong)value >> 8) & 0xf);
            }
        }

        public int VerticalBlankingLines
        {
            get => (int)(GetBits(_bits0, 32, 8) | (GetBits(_bits0, 40, 4) << 8));
            init
            {
                SetBits(ref _bits0, 32, 8, (ulong)value & 0xff);
                SetBits(ref _bits0, 40, 4, ((ulong)value >> 8) & 0xf);
            }
        }

        public int HorizontalFrontPorch
        {
            get => (int)(GetBits(_bits0, 48, 8) | (GetBits(_bits1, 14, 2) << 8));
            init
            {
                SetBits(ref _bits0, 48, 8, (ulong)value & 0xff);
                SetBits(ref _bits1, 14, 2, ((ulong)value >> 8) & 0b11);
            }
        }

        public int HorizontalSyncPulsePixels
        {
            get => (int)(GetBits(_bits0, 56, 8) | (GetBits(_bits1, 12, 2) << 8));
            init
            {
                SetBits(ref _bits0, 56, 8, (ulong)value & 0xff);
                SetBits(ref _bits1, 12, 2, ((ulong)value >> 8) & 0b11);
            }
        }

        public int VerticalFrontPorch
        {
            get => (int)(GetBits(_bits1, 4, 4) | (GetBits(_bits1, 10, 2) << 4));
            init
            {
                SetBits(ref _bits1, 4, 4, (ulong)value & 0xf);
                SetBits(ref _bits1, 10, 2, ((ulong)value >> 4) & 0b11);
            }
        }

        public int VerticalSyncPulseLines
        {
            get => (int)(GetBits(_bits1, 0, 4) | (GetBits(_bits1, 8, 2) << 4));
            init
            {
                SetBits(ref _bits1, 0, 4, (ulong)value & 0xf);
                SetBits(ref _bits1, 8, 2, ((ulong)value >> 4) & 0b11);
            }
        }

        public int HorizontalImageSize
        {
            get => (int)(GetBits(_bits1, 16, 8) | (GetBits(_bits1, 36, 4) << 8));
            init
            {
                SetBits(ref _bits1, 16, 8, (ulong)value & 0xff);
                SetBits(ref _bits1, 36, 4, ((ulong)value >> 8) & 0xf);
            }
        }

        public int VerticalImageSize
        {
            get => (int)(GetBits(_bits1, 24, 8) | (GetBits(_bits1, 32, 4) << 8));
            init
            {
                SetBits(ref _bits1, 24, 8, (ulong)value & 0xff);
                SetBits(ref _bits1, 32, 4, ((ulong)value >> 8) & 0xf);
            }
        }

        public int HorizontalBorderPixels
        {
            get => (int)GetBits(_bits1, 40, 8);
            init
            {
                SetBits(ref _bits1, 40, 8, (ulong)value & 0xff);
            }
        }

        public int VeticalBorderLines
        {
            get => (int)GetBits(_bits1, 48, 8);
            init
            {
                SetBits(ref _bits1, 48, 8, (ulong)value & 0xff);
            }
        }

        public bool InterlacedSignal { get => GetBit(_bits1, 63); init => SetBit(ref _bits1, 63, value); }
        public bool DigitalSync { get => GetBit(_bits1, 60); init => SetBit(ref _bits1, 60, value); }
    }

    public enum MonitorDescriptorType : byte
    {
        DummyIdentifier = 0x10,
        AdditionalStandardTiming3 = 0xf7,
        MonitorName = 0xfc,
        MonitorSerialNumber = 0xff,
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly struct MonitorDescriptor
    {
        public MonitorDescriptor(MonitorDescriptorType descriptorType)
        {
            DescriptorType = descriptorType;
        }

        public MonitorDescriptor(MonitorDescriptorType descriptorType, ReadOnlySpan<char> text)
        {
            DescriptorType = descriptorType;
            Span<byte> textSpan = MemoryMarshal.AsBytes(new Span<Bytes13>(ref _text));
            EncodeBasicCp437(text, textSpan);
            if (text.Length > textSpan.Length)
            {
                textSpan[text.Length] = (byte)'\n';
                textSpan[(text.Length + 1)..].Fill((byte)' ');
            }
        }

        private readonly ushort _reserved0;
        private readonly byte _reserved1;
        public readonly MonitorDescriptorType DescriptorType;
        private readonly byte _reserved2;
        private readonly Bytes13 _text;

        public int TextLength
        {
            get
            {
                ReadOnlySpan<byte> text = MemoryMarshal.AsBytes(new ReadOnlySpan<Bytes13>(in _text));
                int endIndex = text.IndexOf((byte)'\n');
                return endIndex < 0 ? text.Length : endIndex;
            }
        }

        public string Text => string.Create(
            TextLength,
            _text,
            (buf, text) =>
            {
                DecodeBasicCp437(MemoryMarshal.AsBytes(new ReadOnlySpan<Bytes13>(in text))[0..buf.Length], buf);
            });

        [InlineArray(13)]
        private struct Bytes13
        {
            private byte _byte0;
        }
    }

    private static void EncodeBasicCp437(ReadOnlySpan<char> chars, Span<byte> bytes)
    {
        for (int i = 0; i < chars.Length; i++)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(chars[i], 32);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(chars[i], 126);
            bytes[i] = (byte)chars[i];
        }
    }

    private static void DecodeBasicCp437(ReadOnlySpan<byte> bytes, Span<char> chars)
    {
        for (int i = 0; i < chars.Length; i++)
        {
            chars[i] = (char)bytes[i];
        }
    }

    public static ReadOnlySpan<byte> AsReadOnlyBytes(in Edid edid)
    {
        return MemoryMarshal.AsBytes(new ReadOnlySpan<Edid>(in edid));
    }

    public static Span<byte> AsBytes(ref Edid edid)
    {
        return MemoryMarshal.AsBytes(new Span<Edid>(ref edid));
    }

    public Edid() { }

    private readonly byte _header0 = 0x00;
    private readonly byte _header1 = 0xff;
    private readonly byte _header2 = 0xff;
    private readonly byte _header3 = 0xff;
    private readonly byte _header4 = 0xff;
    private readonly byte _header5 = 0xff;
    private readonly byte _header6 = 0xff;
    private readonly byte _header7 = 0x00;

    public ushort ManufacturerId;
    public ushort ManufacturerProduct;
    public uint SerialNumber;
    public byte ManufacturedWeek;
    private byte _manufacturedYear;
    public int ManufacturedYear
    {
        readonly get => _manufacturedYear + 1990;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1990);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, 2245);
            _manufacturedYear = (byte)(value - 1990);
        }
    }

    public byte EdidVersion = 1;
    public byte EdidRevision = 3;

    private byte _videoInputParams = 0b10000000;
    public readonly bool IsDigital => GetBit(_videoInputParams, 7);
    public BitDepth VideoBitDepth { readonly get => (BitDepth)GetBits(_videoInputParams, 4, 3); set => SetBits(ref _videoInputParams, 4, 3, (byte)value); }
    public InterfaceType VideoInterface { readonly get => (InterfaceType)GetBits(_videoInputParams, 0, 3); set => SetBits(ref _videoInputParams, 0, 3, (byte)value); }

    public byte HorizontalScreenSize;
    public byte VerticalScreenSize;
    private readonly byte _displayGamma;

    private byte _supportedFeatures;
    public bool DpmsStandbySupported { readonly get => GetBit(_supportedFeatures, 7); set => SetBit(ref _supportedFeatures, 7, value); }
    public bool DpmsSuspendSupported { readonly get => GetBit(_supportedFeatures, 6); set => SetBit(ref _supportedFeatures, 6, value); }
    public bool DpmsActiveOffSupported { readonly get => GetBit(_supportedFeatures, 5); set => SetBit(ref _supportedFeatures, 5, value); }
    public bool YCrCb422Supported { readonly get => GetBit(_supportedFeatures, 4); set => SetBit(ref _supportedFeatures, 4, value); }
    public bool YCrCb444Supported { readonly get => GetBit(_supportedFeatures, 3); set => SetBit(ref _supportedFeatures, 3, value); }

    private readonly ulong _chromaticity0;
    private readonly ushort _chromaticity1;

    private ushort _supportedTimings0;
    private byte _supportedTimings1;
    public bool Timing720x400x70HzSupported { readonly get => GetBit(_supportedTimings0, 7); set => SetBit(ref _supportedTimings0, 7, value); }
    public bool Timing720x400x88HzSupported { readonly get => GetBit(_supportedTimings0, 6); set => SetBit(ref _supportedTimings0, 6, value); }
    public bool Timing640x480x60HzSupported { readonly get => GetBit(_supportedTimings0, 5); set => SetBit(ref _supportedTimings0, 5, value); }
    public bool Timing640x480x67HzSupported { readonly get => GetBit(_supportedTimings0, 4); set => SetBit(ref _supportedTimings0, 4, value); }
    public bool Timing640x480x72HzSupported { readonly get => GetBit(_supportedTimings0, 3); set => SetBit(ref _supportedTimings0, 3, value); }
    public bool Timing640x480x75HzSupported { readonly get => GetBit(_supportedTimings0, 2); set => SetBit(ref _supportedTimings0, 2, value); }
    public bool Timing800x600x56HzSupported { readonly get => GetBit(_supportedTimings0, 1); set => SetBit(ref _supportedTimings0, 1, value); }
    public bool Timing800x600x60HzSupported { readonly get => GetBit(_supportedTimings0, 0); set => SetBit(ref _supportedTimings0, 0, value); }
    public bool Timing800x600x72HzSupported { readonly get => GetBit(_supportedTimings0, 15); set => SetBit(ref _supportedTimings0, 15, value); }
    public bool Timing800x600x75HzSupported { readonly get => GetBit(_supportedTimings0, 14); set => SetBit(ref _supportedTimings0, 14, value); }
    public bool Timing832x624x75HzSupported { readonly get => GetBit(_supportedTimings0, 13); set => SetBit(ref _supportedTimings0, 13, value); }
    public bool Timing1024x768x87HzSupported { readonly get => GetBit(_supportedTimings0, 12); set => SetBit(ref _supportedTimings0, 12, value); }
    public bool Timing1024x768x60HzSupported { readonly get => GetBit(_supportedTimings0, 11); set => SetBit(ref _supportedTimings0, 11, value); }
    public bool Timing1024x768x70HzSupported { readonly get => GetBit(_supportedTimings0, 10); set => SetBit(ref _supportedTimings0, 10, value); }
    public bool Timing1024x768x75HzSupported { readonly get => GetBit(_supportedTimings0, 9); set => SetBit(ref _supportedTimings0, 9, value); }
    public bool Timing1280x1024x75HzSupported { readonly get => GetBit(_supportedTimings0, 8); set => SetBit(ref _supportedTimings0, 8, value); }
    public bool Timing1152x870x75HzSupported { readonly get => GetBit(_supportedTimings1, 7); set => SetBit(ref _supportedTimings1, 7, value); }

    public StandardTiming StandardTiming1 = StandardTiming.Unused;
    public StandardTiming StandardTiming2 = StandardTiming.Unused;
    public StandardTiming StandardTiming3 = StandardTiming.Unused;
    public StandardTiming StandardTiming4 = StandardTiming.Unused;
    public StandardTiming StandardTiming5 = StandardTiming.Unused;
    public StandardTiming StandardTiming6 = StandardTiming.Unused;
    public StandardTiming StandardTiming7 = StandardTiming.Unused;
    public StandardTiming StandardTiming8 = StandardTiming.Unused;

    public TimingDescriptor PreferredTimingDescriptor;
    public MonitorDescriptor Descriptor2 = new(MonitorDescriptorType.DummyIdentifier);
    public MonitorDescriptor Descriptor3 = new(MonitorDescriptorType.DummyIdentifier);
    public MonitorDescriptor Descriptor4 = new(MonitorDescriptorType.DummyIdentifier);

    public byte ExtensionCount;
    public byte Checksum;

    public readonly byte ComputeChecksum()
    {
        ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(new ReadOnlySpan<Edid>(in this));
        uint sum = 0;
        for (int i = bytes.Length - 2; i >= 0; i--)
        {
            sum += bytes[i];
        }

        return (byte)(256 - sum);
    }

    private static bool GetBit(byte bits, int position)
    {
        return (bits & (1U << position)) != 0;
    }

    private static bool GetBit(uint bits, int position)
    {
        return (bits & (1U << position)) != 0;
    }

    private static bool GetBit(ulong bits, int position)
    {
        return (bits & (1UL << position)) != 0;
    }

    private static void SetBit(ref byte bits, int position, bool value)
    {
        if (value)
        {
            bits |= (byte)(1U << position);
        }
        else
        {
            bits &= (byte)~(1U << position);
        }
    }

    private static void SetBit(ref ushort bits, int position, bool value)
    {
        if (value)
        {
            bits |= (ushort)(1U << position);
        }
        else
        {
            bits &= (ushort)~(1U << position);
        }
    }

    private static void SetBit(ref ulong bits, int position, bool value)
    {
        if (value)
        {
            bits |= 1UL << position;
        }
        else
        {
            bits &= ~(1UL << position);
        }
    }

    private static uint GetBits(uint bits, int position, int size)
    {
        return (bits >> position) & ((1U << size) - 1);
    }

    private static ulong GetBits(ulong bits, int position, int size)
    {
        return (bits >> position) & ((1UL << size) - 1);
    }

    private static void SetBits(ref byte bits, int position, int size, byte value)
    {
        uint mask = ((1U << size) - 1) << position;
        bits = (byte)((bits & ~mask) | (((uint)value << position) & mask));
    }

    private static void SetBits(ref uint bits, int position, int size, uint value)
    {
        uint mask = ((1U << size) - 1) << position;
        bits = (bits & ~mask) | ((value << position) & mask);
    }

    private static void SetBits(ref ulong bits, int position, int size, ulong value)
    {
        ulong mask = ((1UL << size) - 1) << position;
        bits = (bits & ~mask) | ((value << position) & mask);
    }
}
