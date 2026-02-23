namespace ZeroKvm.Hid;

[Flags]
internal enum HidLedsReport : byte
{
    NumLock = 1,
    CapsLock = 2,
    ScrollLock = 4,
    Compose = 8,
    Kana = 16,
}
