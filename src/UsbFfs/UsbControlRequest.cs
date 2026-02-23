using System.Runtime.InteropServices;

namespace UsbFfs;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct UsbControlRequest
{
    public readonly byte RequestType;
    public readonly byte Request;
    public readonly ushort Value;
    public readonly ushort Index;
    public readonly ushort Length;

    public bool IsDirectionIn => (RequestType & 0x80) != 0;

    public bool IsDeviceRecipient => (RequestType & 0x1f) == 0;
    public bool IsInterfaceRecipient => (RequestType & 0x1f) == 1;
    public bool IsEndpointRecipient => (RequestType & 0x1f) == 2;

    public bool IsStandardRequest => (RequestType & 0x60) == 0;
    public bool IsClassRequest => (RequestType & 0x60) == 0x20;
    public bool IsVendorRequest => (RequestType & 0x60) == 0x40;

    public bool IsGetDescriptorRequest => Request == 0x06;
}
