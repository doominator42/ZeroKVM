using System.Runtime.InteropServices;

namespace UsbFfs;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct UsbEndpointDescriptor
{
    public UsbEndpointDescriptor()
    {
        _length = (byte)Marshal.SizeOf<UsbEndpointDescriptor>();
    }

    private readonly byte _length;
    public byte DescriptorType;
    public byte EndpointAddress;
    public byte Attributes;
    public ushort MaxPacketSize;
    public byte Interval;

    public static implicit operator UsbDescriptor(in UsbEndpointDescriptor descriptor)
    {
        return UsbDescriptor.Create(in descriptor);
    }
}
