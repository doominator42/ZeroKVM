using System.Runtime.InteropServices;

namespace UsbFfs;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct FfsDescsHead
{
    public FfsMagic Magic;
    public uint Length;
    public FfsDescsFlags Flags;
}
