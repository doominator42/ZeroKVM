using System.IO;

namespace ZeroKvm.ConfigFs;

internal sealed class ConfigStringsCfs : CfsBase
{
    private const string StringsDirectoryName = "strings";
    private const string ConfigurationFileName = "configuration";

    public static ConfigStringsCfs? ReadFirstIfExists(UsbConfigCfs config)
    {
        string stringsPath = Path.Combine(config.ConfigPath, StringsDirectoryName);
        string[] langs = Directory.Exists(stringsPath) ? Directory.GetFileSystemEntries(stringsPath) : [];
        if (langs.Length == 0)
        {
            return null;
        }

        return new ConfigStringsCfs(langs[0]);
    }

    public ConfigStringsCfs(UsbConfigCfs config, ushort langCode)
        : this(Path.Combine(config.ConfigPath, StringsDirectoryName, "0x" + langCode.ToString("x")))
    { }

    private ConfigStringsCfs(string stringsPath)
        : base(stringsPath)
    {
        LangCode = ParseLangCode(Path.GetFileName(stringsPath));
    }

    public string StringsPath => CfsBasePath;

    public ushort LangCode { get; }

    public ReadOnlySpan<char> Configuration
    {
        get => ReadSingleLineAttr(ConfigurationFileName);
        set => WriteSingleLineAttr(ConfigurationFileName, value);
    }
}
