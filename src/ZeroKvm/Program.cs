using System.Buffers;
using System.CommandLine;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using ZeroKvm.Hid;
using ZeroKvm.Jpeg;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Win32.SafeHandles;
using UsbFfs;
using ZeroKvm.HttpApi;
using ZeroKvm.ConfigFs;
using System.Security.Cryptography;

namespace ZeroKvm;

internal class Program : IDisposable
{
    private const int MaxScreenWidth = 1920;
    private const int MaxScreenHeight = 1080;

    public static int Main(string[] args)
    {
        try
        {
            RootCommand rootCommand = ProgramOptions.CreateRootCommand();
            rootCommand.SetAction(MainCommand);

            ParseResult parsedArgs = rootCommand.Parse(args);
            if (parsedArgs.GetValue(ProgramOptions.DebugEnabled))
            {
                Logger.DebugLogEnabled = true;
            }

            if (parsedArgs.GetValue(ProgramOptions.StatisticsEnabled))
            {
                Logger.StatisticsEnabled = true;
            }

            return parsedArgs.Invoke();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex.ToString());
            return 1;
        }
    }

    private static int MainCommand(ParseResult args)
    {
        using CancellationTokenSource cts = new();
        KestrelServer? httpServer = null;

        try
        {
            using Program program = new(new UsbGadgetCfs(args.GetRequiredValue(ProgramOptions.ConfigFsGadgetPath).FullName))
            {
                UdcName = args.GetRequiredValue(ProgramOptions.UdcName),
            };

            HttpApp.ListenOption[] listenOptions = args.GetRequiredValue(ProgramOptions.Listen);
            httpServer = HttpApp.CreateServer(
                listenOptions,
                LoadCertificateFullChain(
                    args.GetValue(ProgramOptions.CertificatePath)?.FullName,
                    args.GetValue(ProgramOptions.CertificateKeyPath)?.FullName,
                    args.GetValue(ProgramOptions.AutoCreateCertificate)));

            using HttpApp httpApp = new(program, args.GetValue(ProgramOptions.WwwrootPath)?.FullName, args.GetValue(ProgramOptions.Proxy) ?? [])
            {
                RedirectToHttpsPort = args.GetValue(ProgramOptions.HttpsRedirect) ? GetHttpsPort(listenOptions) : null,
            };
            httpServer.StartAsync(httpApp, cts.Token).GetAwaiter().GetResult();

            program.ConfigureUsbGadget();

            if (Logger.StatisticsEnabled)
            {
                Logger.StartStatisticsLogger(cts.Token);
            }

            Thread dlThread = new(() =>
            {
                try
                {
                    program.RunDlGadget(cts.Token);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex.ToString());
                    cts.Cancel();
                }
            })
            {
                IsBackground = true,
                Name = "DlGadget loop",
            };
            dlThread.Start();

            while (program._dlFunction is null)
            {
                Thread.Sleep(1);
            }

            DlFunctionFs dlFunction = program._dlFunction;
            program.MonitorUsbState(dlFunction);
            program.MonitorKeyboardLeds(cts.Token);

            if (args.GetValue(ProgramOptions.Attach))
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(1000);
                    try
                    {
                        Logger.LogDebug("Attaching to UDC " + program.UdcName);
                        program.UsbGadget.Udc = program.UdcName;
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Attach failed", ex);
                    }
                });
            }

            while (!dlFunction.IsDisposed && !cts.Token.IsCancellationRequested)
            {
                dlFunction.ProcessControlRequests();
            }

            cts.Cancel();

            return 0;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex.ToString());
            cts.Cancel();
            return 1;
        }
        finally
        {
            httpServer?.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
            httpServer?.Dispose();
        }
    }

    private static X509Certificate2Collection? LoadCertificateFullChain(string? certificateFullChainFile, string? certificateKeyFile, bool create)
    {
        if (certificateFullChainFile is null)
        {
            return null;
        }
        else if (create && !File.Exists(certificateFullChainFile) && (certificateKeyFile is null || !File.Exists(certificateKeyFile)))
        {
            WriteCertAsPem(GenerateSelfSignedCert(), certificateFullChainFile, certificateKeyFile);
        }

        X509Certificate2Collection certs = new();
        string certPem = ReadCertToPem(certificateFullChainFile);
        certs.ImportFromPem(certPem);
        if (certs.Count == 0)
        {
            throw new Exception($"No certificate found in {certificateFullChainFile}");
        }

        certs[0] = X509Certificate2.CreateFromPem(
            certPem,
            certificateKeyFile is null ? certPem : ReadPrivateKeyToPem(certificateKeyFile, certs[0].GetKeyAlgorithm()));

        return certs;

        static X509Certificate2 GenerateSelfSignedCert()
        {
            Logger.LogDebug("Generating HTTPS certificate ...");
            using ECDsa key = ECDsa.Create();
            CertificateRequest request = new("cn=zerokvm", key, HashAlgorithmName.SHA256);
            DateTimeOffset now = DateTimeOffset.UtcNow;
            return request.CreateSelfSigned(now.AddDays(-1), now.AddYears(10));
        }

        static void WriteCertAsPem(X509Certificate2 cert, string certFileName, string? keyFileName)
        {
            string certPem = cert.ExportCertificatePem() + "\n";
            string keyPem = (cert.GetECDsaPrivateKey()?.ExportECPrivateKeyPem() ?? throw new ArgumentException("ECDSA key not found")) + "\n";
            if (keyFileName is null)
            {
                certPem += keyPem;
                Logger.LogDebug("Creating PEM certificate+key file: " + certFileName);
            }
            else
            {
                Logger.LogDebug("Creating PEM key file: " + keyFileName);
                WriteNewFile(keyFileName, keyPem);
                Logger.LogDebug("Creating PEM certificate file: " + certFileName);
            }

            WriteNewFile(certFileName, certPem);
            File.SetUnixFileMode(keyFileName ?? certFileName, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        static void WriteNewFile(string fileName, string content)
        {
            string? dir = Path.GetDirectoryName(fileName);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            using (FileStream certFile = new(fileName, FileMode.CreateNew, FileAccess.Write))
            {
                certFile.Write(Encoding.UTF8.GetBytes(content));
            }
        }

        static string ReadCertToPem(string fileName)
        {
            byte[] content = File.ReadAllBytes(fileName);
            return IsPem(content) ? Encoding.UTF8.GetString(content) : CertDerToPem(content);
        }

        static string CertDerToPem(ReadOnlySpan<byte> content)
        {
            CheckIsDer(content);
            return $"-----BEGIN CERTIFICATE-----\n{Convert.ToBase64String(content)}\n-----END CERTIFICATE-----\n";
        }

        static string ReadPrivateKeyToPem(string fileName, string keyAlgorithm)
        {
            byte[] content = File.ReadAllBytes(fileName);
            return IsPem(content) ? Encoding.UTF8.GetString(content) : PrivateKeyDerToPem(content, keyAlgorithm);
        }

        static string PrivateKeyDerToPem(ReadOnlySpan<byte> content, string keyAlgorithm)
        {
            CheckIsDer(content);

            // https://github.com/dotnet/runtime/blob/18a3069ebfd2f1c1c07c5cb567e4714999e81e61/src/libraries/System.Security.Cryptography/src/System/Security/Cryptography/X509Certificates/X509Certificate2.cs#L1363
            string? keyLabel = keyAlgorithm switch
            {
                "1.2.840.113549.1.1.1" => "RSA",
                "1.2.840.10045.2.1" => "EC",
                _ => null,
            };

            if (keyLabel is not null)
            {
                keyLabel += " ";
            }

            return $"""
            -----BEGIN {keyLabel}PRIVATE KEY-----
            {Convert.ToBase64String(content)}
            -----END {keyLabel}PRIVATE KEY-----{"\n"}
            """;
        }

        static bool IsPem(ReadOnlySpan<byte> content)
        {
            return content.TrimStart("\x20\t\r\n"u8).StartsWith("---"u8);
        }

        static void CheckIsDer(ReadOnlySpan<byte> content)
        {
            if (content.Length < 32 || content[0] != 0x30) // SEQUENCE
            {
                throw new Exception("Certificate and key files must be in PEM or DER format");
            }
        }
    }

    private static int? GetHttpsPort(IEnumerable<HttpApp.ListenOption> listenOptions)
    {
        foreach (HttpApp.ListenOption option in listenOptions)
        {
            if (option.SslProtocols != System.Security.Authentication.SslProtocols.None)
            {
                return option.EndPoint.Port;
            }
        }

        return null;
    }

    public Program(UsbGadgetCfs usbGadget)
    {
        ArgumentNullException.ThrowIfNull(usbGadget);
        _frameBuffer = new(MaxScreenWidth * MaxScreenHeight, MaxScreenWidth, MaxScreenHeight);
        _eventBus = new();
        UsbGadget = usbGadget;
    }

    public UsbGadgetCfs UsbGadget { get; }

    public required string UdcName { get; init; }

    private readonly FrameBuffer _frameBuffer;
    private DlFunctionFs? _dlFunction;
    private readonly EventBus _eventBus;

    public bool IsDlEnabled => _dlFunction is { } f && f.IsEnabled;

    private static readonly HidDescriptor _compositeMouseDescriptor = new HidDescriptorBuilder(255)
        .BeginMouseCollection()
        .AddReport<HidAbsoluteMouseReport>()
        .AddReport<HidWheelReport>()
        .EndCollection()
        .Build();

    public void ConfigureUsbGadget()
    {
        UsbGadgetCfs gadget = UsbGadget;
        gadget.EnsureCreated();
        gadget.IdVendor = 0x17e9;
        gadget.IdProduct = 0x4010;
        gadget.BcdDevice = 0x0130;
        gadget.BcdUsb = 0x0200;
        gadget.DeviceClass = 0;
        gadget.DeviceSubClass = 0;
        gadget.DeviceProtocol = 0;

        gadget.Strings ??= new(gadget, 0x409);
        gadget.Strings.Manfufacturer = "ZeroKVM"; // TODO: configurable
        gadget.Strings.Product = "ZeroKVM";
        gadget.Strings.SerialNumber = "123456";

        UsbConfigCfs config = new(gadget, "c.1");
        gadget.Configs.Add(config);
        config.RemoveAllFunctions();
        config.MaxPower = 500;

        FunctionFsCfs dl = new(gadget, "dl");
        gadget.Functions.Add(dl);
        DevUtils.MountFfs("dl");

        HidFunctionCfs hidKeyboard = new(gadget, "bkb");
        gadget.Functions.Add(hidKeyboard);
        hidKeyboard.SubClass = 1;
        hidKeyboard.Protocol = 1;
        hidKeyboard.NoOutEndpoint = false;
        hidKeyboard.ReportLength = (byte)HidBootKeyboardReport.ReportLength;
        hidKeyboard.ReportDescriptor = HidBootKeyboardReport.Descriptor;

        HidFunctionCfs hidBootMouse = new(gadget, "bms");
        gadget.Functions.Add(hidBootMouse);
        hidBootMouse.SubClass = 1;
        hidBootMouse.Protocol = 2;
        hidBootMouse.NoOutEndpoint = true;
        hidBootMouse.ReportLength = (byte)HidBootMouseReport.ReportLength;
        hidBootMouse.ReportDescriptor = HidBootMouseReport.Descriptor;

        HidFunctionCfs hidCompositeMouse = new(gadget, "cms");
        gadget.Functions.Add(hidCompositeMouse);
        hidCompositeMouse.SubClass = 0;
        hidCompositeMouse.Protocol = 0;
        hidCompositeMouse.NoOutEndpoint = true;
        hidCompositeMouse.ReportLength = (byte)_compositeMouseDescriptor.MaximumReportLength;
        hidCompositeMouse.ReportDescriptor = _compositeMouseDescriptor.Bytes.Span;

        config.AddFunction(dl);
        config.AddFunction(hidKeyboard);
        config.AddFunction(hidCompositeMouse);
        _currentMouseFunction = hidCompositeMouse;
    }

    public void RunDlGadget(CancellationToken cancellationToken)
    {
        FrameBuffer frameBuffer = _frameBuffer;
        using DlFunctionFs ffs = new(
            "/dev/dl",
            MaxScreenWidth,
            MaxScreenHeight,
            MaxScreenWidth * MaxScreenHeight);

        using (var edid = ffs.AcquireEdid())
        {
            edid.Value = new()
            {
                ManufacturedWeek = 1,
                ManufacturedYear = 2026,
                VideoInterface = Edid.InterfaceType.DisplayPort,
                HorizontalScreenSize = 32,
                VerticalScreenSize = 18,
                Timing640x480x60HzSupported = true,
                Timing800x600x60HzSupported = true,
                Timing1024x768x60HzSupported = true,
                StandardTiming1 = new(1024, Edid.AspectRatio.Aspect16_9, 60),
                StandardTiming2 = new(1280, Edid.AspectRatio.Aspect16_9, 60),
                StandardTiming3 = new(1600, Edid.AspectRatio.Aspect16_9, 60),
                StandardTiming4 = new(1920, Edid.AspectRatio.Aspect16_9, 60),
                PreferredTimingDescriptor = new()
                {
                    PixelClock = 13850,
                    HorizontalActivePixels = 1920,
                    HorizontalBlankingPixels = 160,
                    HorizontalFrontPorch = 48,
                    HorizontalSyncPulsePixels = 32,
                    HorizontalImageSize = 477,
                    HorizontalBorderPixels = 0,
                    VerticalActiveLines = 1080,
                    VerticalBlankingLines = 31,
                    VerticalFrontPorch = 3,
                    VerticalSyncPulseLines = 5,
                    VerticalImageSize = 268,
                    VeticalBorderLines = 0,
                    DigitalSync = true,
                },
                Descriptor2 = new(Edid.MonitorDescriptorType.MonitorName, "ZeroKVM"), // TODO: configurable
            };
        }

        ffs.FrameBufferUpdated += () =>
        {
            FrameArea frame;
            if (frameBuffer.TryLock(out uint updateCount))
            {
                long timestamp = 0;
                try
                {
                    if (Logger.StatisticsEnabled)
                    {
                        timestamp = Stopwatch.GetTimestamp();
                    }

                    frame = ffs.CopyFrameBufferTo(frameBuffer.AllPixels);
                    if (frame.WasModified)
                    {
                        frameBuffer.Update(frame.ModifiedX1, frame.ModifiedY1, frame.ModifiedX2, frame.ModifiedY2);
                    }
                }
                finally
                {
                    frameBuffer.ReleaseLockAfterUpdate(updateCount);
                }

                if (timestamp != 0)
                {
                    Logger.CurrentStatistics.TotalFramesCopied++;
                    Logger.CurrentStatistics.TotalFrameCopyTime += Stopwatch.GetTimestamp() - timestamp;
                }

                if (frame.LineStride > 0 && frame.Height > 0 && (frameBuffer.Width != frame.LineStride || frameBuffer.Height != frame.Height))
                {
                    Logger.LogDebug(static arg => $"Resizing screen to {arg.LineStride}x{arg.Height}", (frame.LineStride, frame.Height));
                    frameBuffer.Resize(frame.LineStride, frame.Height);
                }
            }
            else if (Logger.StatisticsEnabled)
            {
                Logger.CurrentStatistics.TotalFramesSkipped++;
            }
        };

        if (Logger.DebugLogEnabled)
        {
            ffs.StateChanged += _ =>
            {
                Logger.LogDebug($"FunctionFs state changed: {nameof(ffs.IsBound)}={ffs.IsBound} {nameof(ffs.IsEnabled)}={ffs.IsEnabled} {nameof(ffs.IsSuspended)}={ffs.IsSuspended}");
            };
        }

        ffs.Initialize();
        _dlFunction = ffs;
        ffs.BeginReceiver();

        while (!cancellationToken.IsCancellationRequested)
        {
            while (!(ffs.IsEnabled && ffs.IsBound))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                Thread.Sleep(10);
            }

            try
            {
                ffs.ReceiveFrameBuffer();
            }
            catch (IOException ex)
            {
                if (!IsIoErrorIgnored(ex))
                {
                    Logger.LogError("IO error receiving framebuffer", ex);
                }

                Thread.Sleep(10);
            }
        }
    }

    private static bool IsIoErrorIgnored(Exception ex)
    {
        const int ESHUTDOWN = 108;
        return !Logger.DebugLogEnabled &&
            ((ex is ErrnoIOException errnoEx && errnoEx.Errno == ESHUTDOWN) ||
            (ex is IOException ioEx && ioEx.HResult == ESHUTDOWN));
    }

    private void MonitorUsbState(DlFunctionFs ffs)
    {
        Udc.State lastState = Udc.State.Unknown;
        ffs.StateChanged += _ =>
        {
            try
            {
                Udc.State state = Udc.GetState(UdcName);
                if (state.IsAttachedState() != lastState.IsAttachedState())
                {
                    lastState = state;
                    _eventBus.Publish(new UsbStateEvent()
                    {
                        Attached = state.IsAttachedState(),
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error while monitoring USB state", ex);
            }
        };
    }

    public HidLedsReport LastLedsReport { get; private set; }

    private void MonitorKeyboardLeds(CancellationToken cancellationToken)
    {
        Thread thread = new(Worker)
        {
            IsBackground = true,
            Name = "Keyboard LEDs",
        };
        thread.Start();

        void Worker()
        {
            HidFunctionCfs function = UsbGadget.GetFunction<HidFunctionCfs>("bkb");
            string? hidPath = null;
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (hidPath is null)
                    {
                        hidPath = DevUtils.FindDevicePath(function.Device);
                    }
                    else if (!File.Exists(hidPath))
                    {
                        hidPath = null;
                    }

                    if (hidPath is null)
                    {
                        Thread.Sleep(1000);
                    }
                    else if (ReadReport(hidPath, out HidLedsReport report, cancellationToken))
                    {
                        Logger.LogDebug(static report => $"Got keyboard LEDs report: {report}", report);
                        LastLedsReport = report;
                        _eventBus.Publish(new KeyboardLedsEvent()
                        {
                            NumLock = report.HasFlag(HidLedsReport.NumLock),
                            CapsLock = report.HasFlag(HidLedsReport.CapsLock),
                            ScrollLock = report.HasFlag(HidLedsReport.ScrollLock),
                            Compose = report.HasFlag(HidLedsReport.Compose),
                            Kana = report.HasFlag(HidLedsReport.Kana),
                        });
                    }
                }
                catch (Exception ex)
                {
                    hidPath = null;
                    if (!IsIoErrorIgnored(ex))
                    {
                        Logger.LogDebug("Keyboard LEDs monitor: " + ex.Message);
                    }

                    Thread.Sleep(1000);
                }
            }
        }

        static bool ReadReport(string path, out HidLedsReport report, CancellationToken cancellationToken)
        {
            report = 0;
            using SafeFileHandle file = File.OpenHandle(path, FileMode.Open, FileAccess.Read);
            using var ctr = cancellationToken.Register(() =>
            {
                file.Dispose();
            });

            Span<byte> buffer = MemoryMarshal.AsBytes(new Span<HidLedsReport>(ref report));
            return RandomAccess.Read(file, buffer, 0) == buffer.Length;
        }
    }

    public EventBus.Consumer GetEventBusConsumer(Guid? id, out bool isNew)
    {
        EventBus eventBus = _eventBus;
        if (id is null)
        {
            isNew = true;
            return eventBus.CreateConsumer();
        }

        var consumer = eventBus.GetConsumer(id.Value);
        isNew = consumer is null;
        return consumer ?? eventBus.CreateConsumer();
    }

    private static T SetUsbFunction<T>(UsbGadgetCfs gadget, UsbConfigCfs config, string name, ReadOnlySpan<T> functions)
        where T : UsbFunctionCfs
    {
        Logger.LogDebug(static name => $"Enabling USB function {name}", name);
        ReadOnlySpan<char> initialUdc = gadget.Udc;
        Udc.State initialState = initialUdc.Length > 0 ? Udc.GetState(new(initialUdc)) : Udc.State.NotAttached;
        if (initialUdc.Length > 0)
        {
            Logger.LogDebug("Setting UDC to empty");
            gadget.Udc = "";
        }

        foreach (T function in functions)
        {
            config.RemoveFunction(function);
        }

        foreach (T function in functions)
        {
            if (function.FunctionName == name)
            {
                config.AddFunction(function);
                if (initialUdc.Length > 0 && initialState == Udc.State.Configured)
                {
                    Logger.LogDebug(static udc => $"Setting UDC to {udc}", initialUdc);
                    gadget.Udc = initialUdc;
                }

                return function;
            }
        }

        throw new ArgumentException("The function was not found");
    }

    private readonly SemaphoreSlim _hidLock = new(1, 1);

    private HidBootKeyboardReport _currentBootKeyboardReport;
    private string? _currentKeyboardHidPath;
    public async Task<bool> SendBootKeyboardScansAsync(ReadOnlyMemory<KeyScan> keyScans, bool resetAllKeys, CancellationToken cancellationToken)
    {
        SemaphoreSlim hidLock = _hidLock;
        await hidLock.WaitAsync(cancellationToken);
        try
        {
            string? hidPath = _currentKeyboardHidPath;
            if (hidPath is null)
            {
                hidPath = DevUtils.FindDevicePath(UsbGadget.GetFunction<HidFunctionCfs>("bkb").Device);
                if (hidPath is null)
                {
                    return !IsDlEnabled;
                }

                _currentKeyboardHidPath = hidPath;
            }

            using SafeFileHandle file = File.OpenHandle(hidPath, FileMode.Open, FileAccess.Write);
            byte[] buffer = new byte[Unsafe.SizeOf<HidBootKeyboardReport>()];
            HidBootKeyboardReport lastReport = _currentBootKeyboardReport;
            for (int i = 0; i < keyScans.Length; i++)
            {
                KeyScan keyScan = keyScans.Span[i];
                if (keyScan.Delay > 0)
                {
                    await Task.Delay(keyScan.Delay, cancellationToken);
                }

                HidBootKeyboardReport report = resetAllKeys ? default : lastReport;
                resetAllKeys = false;
                report = keyScan.TryGetAsModifier(out HidKeyModifiers modifier) ?
                    keyScan.IsDown ? report.AddModifiers(modifier) : report.RemoveModifiers(modifier) :
                    keyScan.IsDown ? report.AddKeyPress(keyScan.Value) : report.RemoveKeyPress(keyScan.Value);

                if (report != lastReport)
                {
                    report.WriteTo(buffer);
                    await RandomAccess.WriteAsync(file, buffer, 0, cancellationToken);
                    lastReport = report;
                }
            }

            _currentBootKeyboardReport = lastReport;
            return true;
        }
        catch (Exception ex)
        {
            _currentKeyboardHidPath = null;
            if (!IsIoErrorIgnored(ex))
            {
                Logger.LogError("Error in HID keyboard gadget", ex);
            }

            return false;
        }
        finally
        {
            hidLock.Release();
        }
    }

    private HidFunctionCfs? _currentMouseFunction;
    private string? _currentMouseHidPath;
    private string? UseMouseFunctionHidFile(string name)
    {
        HidFunctionCfs? currentFunction = _currentMouseFunction;
        if (currentFunction?.FunctionName != name)
        {
            UsbGadgetCfs gadget = UsbGadget;
            currentFunction = SetUsbFunction(gadget, gadget.Configs[0], name, [
                gadget.GetFunction<HidFunctionCfs>("bms"),
                gadget.GetFunction<HidFunctionCfs>("cms"),
            ]);
            _currentMouseFunction = currentFunction;
            _currentMouseHidPath = null;
        }

        return _currentMouseHidPath ??= DevUtils.FindDevicePath(currentFunction.Device);
    }

    private HidBootMouseReport _currentBootMouseReport;
    public async Task<bool> SendBootMouseEventsAsync(ReadOnlyMemory<PointerEvent> events, bool resetButtons, CancellationToken cancellationToken)
    {
        SemaphoreSlim hidLock = _hidLock;
        await hidLock.WaitAsync(cancellationToken);
        try
        {
            string? hidPath = UseMouseFunctionHidFile("bms");
            if (hidPath is null)
            {
                return !IsDlEnabled;
            }

            _currentAbsoluteMouseReport = default;

            using SafeFileHandle file = File.OpenHandle(hidPath, FileMode.Open, FileAccess.Write);
            byte[] buffer = new byte[HidBootMouseReport.ReportLength];
            HidBootMouseReport lastReport = _currentBootMouseReport;
            for (int i = 0; i < events.Length; i++)
            {
                PointerEvent @event = events.Span[i];
                if (@event.Delay > 0)
                {
                    await Task.Delay(@event.Delay, cancellationToken);
                }

                HidBootMouseReport report = resetButtons ? lastReport.RemoveAllButtons() : lastReport;
                resetButtons = false;
                report = new(
                    (report.Buttons | ToHidButtons(@event.DownButtons)) & ~ToHidButtons(@event.UpButtons),
                    ToHidCoordinate(@event.Move?.X),
                    ToHidCoordinate(@event.Move?.Y),
                    @event.Wheel);

                int length = report.WriteTo(buffer);
                await RandomAccess.WriteAsync(file, buffer.AsMemory(0, length), 0, cancellationToken);
                lastReport = report;
            }

            _currentBootMouseReport = lastReport;
            return true;
        }
        catch (Exception ex)
        {
            _currentMouseHidPath = null;
            if (!IsIoErrorIgnored(ex))
            {
                Logger.LogError("Error in HID mouse gadget", ex);
            }

            return false;
        }
        finally
        {
            hidLock.Release();
        }

        static sbyte ToHidCoordinate(int? value)
        {
            return value is null ? (sbyte)0 : (sbyte)Math.Clamp(value.Value, -127, 127);
        }
    }

    // TODO: support HID Digitizer descriptor, should work on Android
    // See https://github.com/arpruss/USBComposite_stm32f1/pull/64/files
    private HidAbsoluteMouseReport _currentAbsoluteMouseReport;
    public async Task<bool> SendAbsoluteMouseEventsAsync(ReadOnlyMemory<PointerEvent> events, bool resetButtons, CancellationToken cancellationToken)
    {
        SemaphoreSlim hidLock = _hidLock;
        await hidLock.WaitAsync(cancellationToken);
        try
        {
            string? hidPath = UseMouseFunctionHidFile("cms");
            if (hidPath is null)
            {
                return !IsDlEnabled;
            }

            _currentBootMouseReport = default;

            using SafeFileHandle file = File.OpenHandle(hidPath, FileMode.Open, FileAccess.Write);
            byte[] buffer = new byte[_compositeMouseDescriptor.MaximumReportLength];
            HidAbsoluteMouseReport lastReport = _currentAbsoluteMouseReport;
            for (int i = 0; i < events.Length; i++)
            {
                PointerEvent @event = events.Span[i];
                if (@event.Delay > 0)
                {
                    await Task.Delay(@event.Delay, cancellationToken);
                }

                HidAbsoluteMouseReport report = resetButtons ? lastReport.RemoveAllButtons() : lastReport;
                resetButtons = false;
                report = new(
                    _compositeMouseDescriptor.GetReportId(typeof(HidAbsoluteMouseReport)),
                    (report.Buttons | ToHidButtons(@event.DownButtons)) & ~ToHidButtons(@event.UpButtons),
                    ToHidCoordinate(@event.Move?.X, _frameBuffer.Width, lastReport.X),
                    ToHidCoordinate(@event.Move?.Y, _frameBuffer.Height, lastReport.Y));

                if (report != lastReport)
                {
                    int length = report.WriteTo(buffer);
                    await RandomAccess.WriteAsync(file, buffer.AsMemory(0, length), 0, cancellationToken);
                    lastReport = report;
                }

                if (@event.Wheel != 0)
                {
                    int length = new HidWheelReport(_compositeMouseDescriptor.GetReportId(typeof(HidWheelReport)), @event.Wheel).WriteTo(buffer);
                    await RandomAccess.WriteAsync(file, buffer.AsMemory(0, length), 0, cancellationToken);
                }
            }

            _currentAbsoluteMouseReport = lastReport;
            return true;
        }
        catch (Exception ex)
        {
            _currentMouseHidPath = null;
            if (!IsIoErrorIgnored(ex))
            {
                Logger.LogError("Error in HID mouse gadget", ex);
            }

            return false;
        }
        finally
        {
            hidLock.Release();
        }

        static short ToHidCoordinate(int? position, int max, short lastReportPosition)
        {
            return position is null ? lastReportPosition : (short)Math.Clamp((int)((double)position / max * short.MaxValue), 0, short.MaxValue);
        }
    }

    private static HidMouseButtons ToHidButtons(PointerButtons buttons)
    {
        return (buttons.HasFlag(PointerButtons.Left) ? HidMouseButtons.Left : 0) |
            (buttons.HasFlag(PointerButtons.Middle) ? HidMouseButtons.Middle : 0) |
            (buttons.HasFlag(PointerButtons.Right) ? HidMouseButtons.Right : 0);
    }

    public async Task<ReadOnlyMemory<byte>> GetScreenshotAsync(JpegCompressor compressor)
    {
        FrameBuffer frameBuffer = _frameBuffer;
        int length;
        byte[] buffer = new byte[compressor.GetJpegMaxByteCount(frameBuffer.Width, frameBuffer.Height)];
        await frameBuffer.LockAsync();
        try
        {
            length = compressor.Compress(
                new JpegPixelBuffer()
                {
                    Buffer = MemoryMarshal.AsBytes(frameBuffer.Pixels),
                    Width = frameBuffer.Width,
                    Height = frameBuffer.Height,
                    BytesPerLine = frameBuffer.Width * Unsafe.SizeOf<uint>(),
                    Format = JpegPixelFormat.Rgbx,
                },
                buffer.AsSpan());
        }
        finally
        {
            frameBuffer.ReleaseLock();
        }

        return buffer.AsMemory(0, length);
    }

    public async Task WriteMultipartStreamAsync(PipeWriter output, string boundary, JpegCompressor compressor, CancellationToken cancellationToken)
    {
        FrameBuffer frameBuffer = _frameBuffer;
        byte[] crlfBoundaryCrlf = Encoding.ASCII.GetBytes("\r\n--" + boundary + "\r\n");

        output.Write(Encoding.ASCII.GetBytes("--" + boundary + "\r\n"));

        while (!cancellationToken.IsCancellationRequested)
        {
            long timestamp = 0;
            Task updateWaiter;

            await frameBuffer.LockAsync();
            try
            {
                updateWaiter = frameBuffer.UpdateWaiter;
                if (Logger.StatisticsEnabled)
                {
                    timestamp = Stopwatch.GetTimestamp();
                }

                Span<byte> bodySpan = output.GetSpan((int)compressor.GetJpegMaxByteCount(frameBuffer.Width, frameBuffer.Height) + 256);
                int headersLength = WriteMultipartHeaders(bodySpan, out Span<byte> contentLength);

                int length = compressor.Compress(
                    new JpegPixelBuffer()
                    {
                        Buffer = MemoryMarshal.AsBytes(frameBuffer.Pixels),
                        Width = frameBuffer.Width,
                        Height = frameBuffer.Height,
                        BytesPerLine = frameBuffer.Width * Unsafe.SizeOf<uint>(),
                        Format = JpegPixelFormat.Rgbx,
                    },
                    bodySpan[headersLength..^64]);

                FormatPadded(length, contentLength);
                crlfBoundaryCrlf.CopyTo(bodySpan.Slice(headersLength + length));

                output.Advance(headersLength + length + crlfBoundaryCrlf.Length);
            }
            finally
            {
                frameBuffer.ReleaseLock();
            }

            if (timestamp != 0)
            {
                Logger.CurrentStatistics.TotalVideoFramesEncoded++;
                Logger.CurrentStatistics.TotalVideoFramesEncodeTime += Stopwatch.GetTimestamp() - timestamp;
            }

            await output.FlushAsync(cancellationToken);

            await updateWaiter.WaitAsync(cancellationToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }

        static void FormatPadded(int value, Span<byte> output)
        {
            if (!value.TryFormat(output, out int length))
            {
                length = 0;
            }

            output.Slice(length).Fill((byte)' ');
        }

        static int WriteMultipartHeaders(Span<byte> buffer, out Span<byte> contentLengthBuffer)
        {
            ReadOnlySpan<byte> headers = "Content-Type: image/jpeg\r\nContent-Length: \0\0\0\0\0\0\0\0\0\0\r\n\r\n"u8;
            headers.CopyTo(buffer);

            contentLengthBuffer = buffer.Slice(headers.Length - 14, 10);
            return headers.Length;
        }
    }

    public async Task WriteRectsStreamAsync(PipeWriter output, JpegCompressor compressor, CancellationToken cancellationToken)
    {
        FrameBuffer frameBuffer = _frameBuffer;
        using FrameBuffer.Client client = frameBuffer.CreateClient();

        while (!cancellationToken.IsCancellationRequested)
        {
            long timestamp = 0;
            Task updateWaiter;

            await frameBuffer.LockAsync();
            try
            {
                updateWaiter = frameBuffer.UpdateWaiter;
                if (Logger.StatisticsEnabled)
                {
                    timestamp = Stopwatch.GetTimestamp();
                }

                ReadOnlySpan<uint> fbPixels = client.ConsumeUpdate(out int rectX, out int rectY, out int rectWidth, out int rectHeight, out int lineStride);

                if (fbPixels.Length > 0)
                {
                    Span<byte> bodySpan = output.GetSpan((int)compressor.GetJpegMaxByteCount(rectWidth, rectHeight) + Unsafe.SizeOf<MjpegRectHeader>());
                    int length = compressor.Compress(
                        new JpegPixelBuffer()
                        {
                            Buffer = MemoryMarshal.AsBytes(fbPixels),
                            Width = rectWidth,
                            Height = rectHeight,
                            BytesPerLine = lineStride * Unsafe.SizeOf<uint>(),
                            Format = JpegPixelFormat.Rgbx,
                        },
                        bodySpan.Slice(Unsafe.SizeOf<MjpegRectHeader>()),
                        rectWidth * rectHeight <= 4096 ? 100 : null);

                    Unsafe.As<byte, MjpegRectHeader>(ref MemoryMarshal.GetReference(bodySpan)) = new()
                    {
                        ScreenWidth = (ushort)frameBuffer.Width,
                        ScreenHeight = (ushort)frameBuffer.Height,
                        RectX = (ushort)rectX,
                        RectY = (ushort)rectY,
                        FrameLength = (uint)length,
                    };

                    output.Advance(length + Unsafe.SizeOf<MjpegRectHeader>());
                }
                else
                {
                    Logger.LogError("Bug: got empty rectangle update");
                }
            }
            finally
            {
                frameBuffer.ReleaseLock();
            }

            if (timestamp != 0)
            {
                Logger.CurrentStatistics.TotalVideoFramesEncoded++;
                Logger.CurrentStatistics.TotalVideoFramesEncodeTime += Stopwatch.GetTimestamp() - timestamp;
            }

            await output.FlushAsync(cancellationToken);

            await updateWaiter.WaitAsync(cancellationToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct MjpegRectHeader
    {
        public ushort ScreenWidth;
        public ushort ScreenHeight;
        public ushort RectX;
        public ushort RectY;
        public uint FrameLength;
    }

    public void Dispose()
    {
        Interlocked.Exchange(ref _dlFunction, null)?.Dispose();
        _frameBuffer.Dispose();
        _hidLock.Dispose();
    }
}
