using System.IO;

namespace ZeroKvm.ConfigFs;

internal abstract class UsbFunctionCfs : CfsBase
{
    public static ReadOnlySpan<char> GetFunctionType(ReadOnlySpan<char> functionPath)
    {
        return Path.GetFileNameWithoutExtension(functionPath.TrimEnd(Path.DirectorySeparatorChar));
    }

    protected static string GetFuntionPath(UsbGadgetCfs gadget, string name, string type) =>
        Path.Combine(gadget.GadgetPath, "functions", type + "." + name);

    protected UsbFunctionCfs(string functionPath, string functionType)
        : base(functionPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(functionType);
        if (!GetFunctionType(FunctionPath).Equals(functionType, StringComparison.Ordinal))
        {
            throw new ArgumentException("Wrong function type", nameof(functionType));
        }

        FunctionType = functionType;
        FunctionName = new(Path.GetExtension(FunctionPath.AsSpan()).TrimStart('.'));
    }

    public string FunctionPath => CfsBasePath;

    public string FunctionType { get; }

    public string FunctionName { get; }
}
