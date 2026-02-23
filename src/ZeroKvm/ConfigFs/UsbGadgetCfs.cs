using System.IO;

namespace ZeroKvm.ConfigFs;

internal class UsbGadgetCfs : CfsBase
{
    public const string DefaultGadgetsBasePath = "/sys/kernel/config/usb_gadget";

    private const string IdVendorFileName = "idVendor";
    private const string IdProductFileName = "idProduct";
    private const string BcdDeviceFileName = "bcdDevice";
    private const string BcdUsbFileName = "bcdUSB";
    private const string DeviceClassFileName = "bDeviceClass";
    private const string DeviceSubClassFileName = "bDeviceSubClass";
    private const string DeviceProtocolFileName = "bDeviceProtocol";
    private const string UdcFileName = "UDC";

    public static UsbGadgetCfs FromGadgetName(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        return new(Path.Combine(DefaultGadgetsBasePath, name));
    }

    public static UsbGadgetCfs[] GetGadgets(string basePath = DefaultGadgetsBasePath)
    {
        return Array.ConvertAll(Directory.GetFileSystemEntries(basePath), path => new UsbGadgetCfs(path));
    }

    public UsbGadgetCfs(string gadgetPath)
        : base(gadgetPath)
    {
        Strings = DeviceStringsCfs.ReadFirstIfExists(this);
    }

    public string GadgetPath => CfsBasePath;

    public ushort IdVendor
    {
        get => ReadUIntHexAttr<ushort>(IdVendorFileName);
        set => WriteUIntHexAttr(IdVendorFileName, value);
    }

    public ushort IdProduct
    {
        get => ReadUIntHexAttr<ushort>(IdProductFileName);
        set => WriteUIntHexAttr(IdProductFileName, value);
    }

    public ushort BcdDevice
    {
        get => ReadUIntHexAttr<ushort>(BcdDeviceFileName);
        set => WriteUIntHexAttr(BcdDeviceFileName, value);
    }

    public ushort BcdUsb
    {
        get => ReadUIntHexAttr<ushort>(BcdUsbFileName);
        set => WriteUIntHexAttr(BcdUsbFileName, value);
    }

    public byte DeviceClass
    {
        get => ReadUIntHexAttr<byte>(DeviceClassFileName);
        set => WriteUIntHexAttr(DeviceClassFileName, value);
    }

    public byte DeviceSubClass
    {
        get => ReadUIntHexAttr<byte>(DeviceSubClassFileName);
        set => WriteUIntHexAttr(DeviceSubClassFileName, value);
    }

    public byte DeviceProtocol
    {
        get => ReadUIntHexAttr<byte>(DeviceProtocolFileName);
        set => WriteUIntHexAttr(DeviceProtocolFileName, value);
    }

    public ReadOnlySpan<char> Udc
    {
        get => ReadSingleLineAttr(UdcFileName);
        set => WriteSingleLineAttr(UdcFileName, value);
    }

    public DeviceStringsCfs? Strings
    {
        get;
        set
        {
            if (value is null && field is not null)
            {
                field.EnsureDeleted();
            }
            else if (value is not null && field is null)
            {
                value.EnsureCreated();
            }

            field = value;
        }
    }

    public CfsCollection<UsbFunctionCfs> Functions { get; } = new();

    public CfsCollection<UsbConfigCfs> Configs { get; } = new();

    public UsbFunctionCfs? TryGetFunction(string name)
    {
        foreach (UsbFunctionCfs function in Functions)
        {
            if (function.FunctionName == name)
            {
                return function;
            }
        }

        return null;
    }

    public T? TryGetFunction<T>()
        where T : UsbFunctionCfs
    {
        foreach (UsbFunctionCfs function in Functions)
        {
            if (function is T typedFunction)
            {
                return typedFunction;
            }
        }

        return null;
    }

    public T GetFunction<T>(string name)
        where T : UsbFunctionCfs
    {
        foreach (UsbFunctionCfs function in Functions)
        {
            if (function is T typedFunction && function.FunctionName == name)
            {
                return typedFunction;
            }
        }

        throw new ArgumentException($"Function '{name}' was not found");
    }
}
