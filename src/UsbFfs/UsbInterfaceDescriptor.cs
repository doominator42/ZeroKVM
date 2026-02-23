using System.Runtime.InteropServices;

namespace UsbFfs;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct UsbInterfaceDescriptor
{
    public UsbInterfaceDescriptor()
    {
        _length = (byte)Marshal.SizeOf<UsbInterfaceDescriptor>();
    }

    private readonly byte _length;
    public byte DescriptorType;
    public byte InterfaceNumber;
    public byte AlternateSetting;
    public byte NumEndpoints;
    public byte InterfaceClass;
    public byte InterfaceSubClass;
    public byte InterfaceProtocol;
    public byte InterfaceStringIndex;

    public static implicit operator UsbDescriptor(in UsbInterfaceDescriptor descriptor)
    {
        return UsbDescriptor.Create(in descriptor);
    }
}
