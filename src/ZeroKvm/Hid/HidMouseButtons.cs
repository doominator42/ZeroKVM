namespace ZeroKvm.Hid;

[Flags]
internal enum HidMouseButtons : byte
{
    None = 0,
    Left = 1,
    Right = 2,
    Middle = 4,
}
