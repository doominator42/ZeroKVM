using System.Runtime.InteropServices;
using UsbFfs;

namespace ZeroKvm;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal readonly struct DlVendorUsbDescriptor
{
    public DlVendorUsbDescriptor(ushort maxWidth, ushort maxHeight, uint maxPixels)
    {
        _length = (byte)Marshal.SizeOf<DlVendorUsbDescriptor>();
        _descriptorType = 0x5f;
        _version = 0x0001;
        _length2 = (byte)(_length - 2);
        _maxPixelsHeader = new()
        {
            Key = Keys.MaxArea,
            Length = 4,
        };
        MaxPixels = maxPixels;
        _maxWidthHeader = new()
        {
            Key = Keys.MaxWidth,
            Length = 4,
        };
        MaxWidth = maxWidth;
        _maxHeightHeader = new()
        {
            Key = Keys.MaxHeight,
            Length = 4,
        };
        MaxHeight = maxHeight;
    }

    private readonly byte _length;
    private readonly byte _descriptorType;
    private readonly ushort _version;
    private readonly byte _length2;

    private readonly DataHeader _maxPixelsHeader;
    public readonly uint MaxPixels;

    private readonly DataHeader _maxWidthHeader;
    public readonly uint MaxWidth;

    private readonly DataHeader _maxHeightHeader;
    public readonly uint MaxHeight;

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct DataHeader
    {
        public Keys Key;
        public byte Length;
    }

    private enum Keys : ushort
    {
        RenderingProtocolVersion = 0x0000,
        RawMode = 0x0001,
        RlMode = 0x0002,
        RawRlMode = 0x0003,
        RlxMode = 0x0004,
        DiffMode = 0x0005,
        DiffPadding = 0x0006,
        RdiffFlags = 0x0007,
        ObType = 0x0100,
        ObMethod = 0x0101,
        ObTaps = 0x0102,
        ObLength = 0x0103,
        ObCycles = 0x0104,
        MaxArea = 0x0200,
        MaxWidth = 0x0201,
        MaxHeight = 0x0202,
        MaxPixelClock = 0x0203,
        MinPixelClock = 0x0204,
        VideoRamStart = 0x0300,
        VideoRamEnd = 0x0301,
        RamBandwidth = 0x0302,
        ChipId = 0x0400,
        ProtoEngineChannels = 0x0401,
        MinSwVersion = 0x0402,
        MinSwRevision = 0x0403,
        ModeRegisterSize = 0x0500,
        BigEndianFrequency = 0x0501,
        SyncPulsePolarity688 = 0x0502,
        ExtendedEdid = 0x0600,
        AviInfoframe = 0x0601,
    }

    public static implicit operator UsbDescriptor(in DlVendorUsbDescriptor descriptor)
    {
        return UsbDescriptor.Create(in descriptor);
    }

    public int ComputeChecksum()
    {
        return ComputeChecksum(MemoryMarshal.AsBytes(new ReadOnlySpan<DlVendorUsbDescriptor>(in this)).Slice(2));
    }

    private static int ComputeChecksum(ReadOnlySpan<byte> descriptorBytes)
    {
        int checksum = 0;
        foreach (byte value in descriptorBytes)
        {
            checksum = (-((value ^ checksum) & 1) & 0x101e) ^ checksum;
            checksum = (-(((value >> 1) ^ (checksum >> 1)) & 1) & 0x101e) ^ (checksum >> 1);
            checksum = (-(((value >> 2) ^ (checksum >> 1)) & 1) & 0x101e) ^ (checksum >> 1);
            checksum = (-(((value >> 3) ^ (checksum >> 1)) & 1) & 0x101e) ^ (checksum >> 1);
            checksum = (-(((value >> 4) ^ (checksum >> 1)) & 1) & 0x101e) ^ (checksum >> 1);
            checksum = (-(((value >> 5) ^ (checksum >> 1)) & 1) & 0x101e) ^ (checksum >> 1);
            checksum = (-(((value >> 6) ^ (checksum >> 1)) & 1) & 0x101e) ^ (checksum >> 1);
            checksum = ((-(((value >> 7) ^ (checksum >> 1)) & 1) & 0x101e) ^ (checksum >> 1)) >> 1;
        }

        return checksum;
    }
}
