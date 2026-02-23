using System.Runtime.InteropServices;

namespace UsbFfs;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct FfsStringsHead
{
    public FfsMagic Magic;
    public uint Length;
    public uint StrCount;
    public uint LangCount;
}
