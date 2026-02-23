namespace UsbFfs;

internal enum FfsEventType : byte
{
    Bind,
    Unbind,
    Enable,
    Disable,
    Setup,
    Suspend,
    Resume,
}
