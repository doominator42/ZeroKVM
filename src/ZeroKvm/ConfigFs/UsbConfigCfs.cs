using System.IO;

namespace ZeroKvm.ConfigFs;

internal sealed class UsbConfigCfs : CfsBase
{
    private const string ConfigsDirectoryName = "configs";
    private const string MaxPowerFileName = "MaxPower";
    private const string AttributesFileName = "bmAttributes";

    public static UsbConfigCfs? ReadFirstIfExists(UsbGadgetCfs gadget)
    {
        string configsPath = Path.Combine(gadget.GadgetPath, ConfigsDirectoryName);
        string[] configs = Directory.Exists(configsPath) ? Directory.GetFileSystemEntries(configsPath) : [];
        if (configs.Length == 0)
        {
            return null;
        }

        return new UsbConfigCfs(configs[0]);
    }

    public UsbConfigCfs(UsbGadgetCfs gadget, string name)
        : this(Path.Combine(gadget.GadgetPath, ConfigsDirectoryName, name))
    { }

    private UsbConfigCfs(string configPath)
        : base(configPath)
    {
        Strings = ConfigStringsCfs.ReadFirstIfExists(this);
    }

    public string ConfigPath => CfsBasePath;

    public ushort MaxPower
    {
        get => ReadIntAttr<ushort>(MaxPowerFileName);
        set
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, 2040);
            WriteIntAttr(MaxPowerFileName, value);
        }
    }

    public byte Attributes
    {
        get => ReadIntAttr<byte>(AttributesFileName);
        set => WriteIntAttr(AttributesFileName, value);
    }

    public ConfigStringsCfs? Strings { get; set; }

    public void AddFunction(UsbFunctionCfs function)
    {
        string linkPath = GetFunctionLinkPath(function);
        if (Directory.Exists(linkPath))
        {
            Logger.LogDebug(static args => $"Function {args.function.FunctionType}.{args.function.FunctionName} already added to config {Path.GetFileName(args.Item2.ConfigPath)}", (function, this));
        }
        else
        {
            Logger.LogDebug(static args => $"Adding function {args.function.FunctionType}.{args.function.FunctionName} to config {Path.GetFileName(args.Item2.ConfigPath)}", (function, this));
            Directory.CreateSymbolicLink(linkPath, function.FunctionPath);
        }
    }

    public void RemoveFunction(UsbFunctionCfs function)
    {
        string linkPath = GetFunctionLinkPath(function);
        if (Directory.Exists(linkPath))
        {
            Logger.LogDebug(static args => $"Removing function {args.function.FunctionType}.{args.function.FunctionName} from config {Path.GetFileName(args.Item2.ConfigPath)}", (function, this));
            File.Delete(linkPath);
        }
        else
        {
            Logger.LogDebug(static args => $"Function {args.function.FunctionType}.{args.function.FunctionName} already removed from config {Path.GetFileName(args.Item2.ConfigPath)}", (function, this));
        }
    }

    public void RemoveAllFunctions()
    {
        foreach (string fileName in Directory.GetFileSystemEntries(ConfigPath))
        {
            if (Path.GetFileName(fileName.AsSpan()).Contains('.'))
            {
                File.Delete(fileName);
            }
        }
    }

    private string GetFunctionLinkPath(UsbFunctionCfs function) =>
        Path.Combine(ConfigPath, Path.GetFileName(function.FunctionPath));
}
