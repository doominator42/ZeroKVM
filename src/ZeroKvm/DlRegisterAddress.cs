namespace ZeroKvm;

internal enum DlRegisterAddress : byte
{
    ColorDepth = 0x00,
    XDisplayStart = 0x01,
    XDisplayEnd = 0x03,
    YDisplayStart = 0x05,
    YDisplayEnd = 0x07,
    XEndCount = 0x09,
    HSyncStart = 0x0b,
    HSyncEnd = 0x0d,
    HPixels = 0x0f,
    YEndCount = 0x11,
    VSyncStart = 0x13,
    VSyncEnd = 0x15,
    VPixels = 0x17,
    PixelClock5khz = 0x1b,
    BlankOutput = 0x1f,
    BaseOffset16 = 0x20,
    LineStride16 = 0x23,
    BaseOffset8 = 0x26,
    LineStride8 = 0x29,
    RegistersUpdate = 0xff,
}
