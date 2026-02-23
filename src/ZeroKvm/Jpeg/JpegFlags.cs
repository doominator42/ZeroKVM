namespace ZeroKvm.Jpeg;

[Flags]
internal enum JpegFlags
{
    BottomUp = 2,
    ForceMmx = 8,
    ForceSse = 16,
    ForceSse2 = 32,
    ForceSse3 = 128,
    FastUpSample = 256,
    NoRealloc = 1024,
    FastDct = 2048,
    AccurateDct = 4096,
    StopOnWarning = 8192,
    Progressive = 16384,
    LimitScans = 32768,
}
