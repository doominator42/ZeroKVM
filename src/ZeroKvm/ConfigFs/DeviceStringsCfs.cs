using System.IO;

namespace ZeroKvm.ConfigFs;

internal sealed class DeviceStringsCfs : CfsBase
{
    private const string StringsDirectoryName = "strings";
    private const string ManufacturerFileName = "manufacturer";
    private const string ProductFileName = "product";
    private const string SerialNumberFileName = "serialnumber";

    public static DeviceStringsCfs? ReadFirstIfExists(UsbGadgetCfs gadget)
    {
        string stringsPath = Path.Combine(gadget.GadgetPath, StringsDirectoryName);
        string[] langs = Directory.Exists(stringsPath) ? Directory.GetFileSystemEntries(stringsPath) : [];
        if (langs.Length == 0)
        {
            return null;
        }

        return new DeviceStringsCfs(langs[0]);
    }

    public DeviceStringsCfs(UsbGadgetCfs gadget, ushort langCode)
        : this(Path.Combine(gadget.GadgetPath, StringsDirectoryName, "0x" + langCode.ToString("x")))
    {
        LangCode = langCode;
    }

    private DeviceStringsCfs(string stringsPath)
        : base(stringsPath)
    {
        LangCode = ParseLangCode(Path.GetFileName(stringsPath));
    }

    public string StringsPath => CfsBasePath;

    public ushort LangCode { get; }

    public ReadOnlySpan<char> Manfufacturer
    {
        get => ReadSingleLineAttr(ManufacturerFileName);
        set => WriteSingleLineAttr(ManufacturerFileName, value);
    }

    public ReadOnlySpan<char> Product
    {
        get => ReadSingleLineAttr(ProductFileName);
        set => WriteSingleLineAttr(ProductFileName, value);
    }

    public ReadOnlySpan<char> SerialNumber
    {
        get => ReadSingleLineAttr(SerialNumberFileName);
        set => WriteSingleLineAttr(SerialNumberFileName, value);
    }
}
