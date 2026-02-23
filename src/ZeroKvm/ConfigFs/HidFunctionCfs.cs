namespace ZeroKvm.ConfigFs;

// TODO: add support for "interval" property (Linux 6.16+), add option for polling rate in GUI
internal sealed class HidFunctionCfs : UsbFunctionCfs
{
    public new const string FunctionType = "hid";

    private const string DevFileName = "dev";
    private const string NoOutEndpointFileName = "no_out_endpoint";
    private const string SubClassFileName = "subclass";
    private const string ProtocolFileName = "protocol";
    private const string ReportDescriptorFileName = "report_desc";
    private const string ReportLengthFileName = "report_length";

    public HidFunctionCfs(string functionPath)
        : base(functionPath, FunctionType)
    { }

    public HidFunctionCfs(UsbGadgetCfs gadget, string name)
        : this(GetFuntionPath(gadget, name, FunctionType))
    { }

    public (uint Major, uint Minor) Device => ReadDevMajorMinor(DevFileName);

    public bool NoOutEndpoint
    {
        get => ReadIntAttr<uint>(NoOutEndpointFileName) != 0;
        set => WriteIntAttr(NoOutEndpointFileName, value ? 1 : 0);
    }

    public byte SubClass
    {
        get => ReadIntAttr<byte>(SubClassFileName);
        set => WriteIntAttr(SubClassFileName, value);
    }

    public byte Protocol
    {
        get => ReadIntAttr<byte>(ProtocolFileName);
        set => WriteIntAttr(ProtocolFileName, value);
    }

    public ushort ReportLength
    {
        get => ReadIntAttr<ushort>(ReportLengthFileName);
        set => WriteIntAttr(ReportLengthFileName, value);
    }

    public ReadOnlySpan<byte> ReportDescriptor
    {
        get => ReadBytesAttr(ReportDescriptorFileName);
        set => WriteBytesAttr(ReportDescriptorFileName, value);
    }
}
