namespace ZeroKvm.ConfigFs;

internal sealed class FunctionFsCfs : UsbFunctionCfs
{
    private const string ReadyFileName = "ready";

    public new const string FunctionType = "ffs";

    public FunctionFsCfs(string functionPath)
        : base(functionPath, FunctionType)
    { }

    public FunctionFsCfs(UsbGadgetCfs gadget, string name)
        : this(GetFuntionPath(gadget, name, FunctionType))
    { }

    public bool IsReady => ReadIntAttr<uint>(ReadyFileName) != 0;
}
