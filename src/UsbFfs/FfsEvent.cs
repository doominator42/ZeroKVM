using System.Runtime.InteropServices;

namespace UsbFfs;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal readonly struct FfsEvent
{
    public readonly UsbControlRequest Setup;
    public readonly FfsEventType Type;
    private readonly byte _pad0;
    private readonly byte _pad1;
    private readonly byte _pad2;
}
