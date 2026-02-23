namespace ZeroKvm.Hid;

[Flags]
internal enum HidKeyModifiers : byte
{
    None = 0,
    LeftControl = 1,
    LeftShift = 2,
    LeftAlt = 4,
    LeftGui = 8,
    RightControl = 16,
    RightShift = 32,
    RightAlt = 64,
    RightGui = 128,
}
