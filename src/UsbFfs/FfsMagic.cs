namespace UsbFfs;

[Flags]
internal enum FfsMagic : uint
{
    StringsMagic = 2,
    DescriptorsMagic = 3,
}
