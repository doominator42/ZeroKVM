namespace ZeroKvm.Hid;

[Flags]
internal enum PointerButtons : byte
{
    None = 0,
    Left = 1,
    Middle = 2,
    Right = 4,
}
