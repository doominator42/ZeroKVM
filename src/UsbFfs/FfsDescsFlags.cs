namespace UsbFfs;

[Flags]
internal enum FfsDescsFlags : uint
{
    HasFullSpeedDesc = 1,
    HasHighSpeedDesc = 2,
    HasSuperSpeedDesc = 4,
    HasMsOsDesc = 8,
    VirtualAddr = 16,
    EventFd = 32,
    AllCtrlRecip = 64,
    Config0Setup = 128,
}
