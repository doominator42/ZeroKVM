namespace UsbFfs;

public class FunctionFs : IDisposable
{
    public FunctionFs(string mountPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(mountPath);
        _mountPath = mountPath;
        _ep0 = new(mountPath);
    }

    private readonly string _mountPath;
    private readonly UsbEp0 _ep0;

    public bool HandleConfig0Setup { get; init; }

    public bool HandleAllControlRequests { get; init; }

    public bool IsBound { get; private set; }

    public bool IsEnabled { get; private set; }

    public bool IsSuspended { get; private set; }

    protected virtual void WriteDescriptors(
        ReadOnlySpan<UsbDescriptor> fullSpeedDescriptors,
        ReadOnlySpan<UsbDescriptor> highSpeedDescriptors = default,
        ReadOnlySpan<UsbDescriptor> superSpeedDescriptors = default)
    {
        _ep0.WriteDescriptors(
            fullSpeedDescriptors,
            highSpeedDescriptors,
            superSpeedDescriptors,
            (HandleConfig0Setup ? FfsDescsFlags.Config0Setup : 0) |
            (HandleAllControlRequests ? FfsDescsFlags.AllCtrlRecip : 0));
    }

    protected virtual void WriteStrings(ushort langCode, ReadOnlySpan<string> strings)
    {
        _ep0.WriteStrings(langCode, strings);
    }

    protected UsbInEp OpenInEp(int address)
    {
        return new(_mountPath, address);
    }

    protected UsbInEp OpenInEpForAio(int address, int bufferCount, int bufferSize, int bufferOffset)
    {
        CheckAioBuffers(bufferCount, bufferSize, bufferOffset);

        return new(_mountPath, address)
        {
            AioBufferCount = bufferCount,
            AioBufferSize = bufferSize,
            AioBufferOffset = bufferOffset,
        };
    }

    protected UsbOutEp OpenOutEp(int address)
    {
        return new(_mountPath, address);
    }

    protected UsbOutEp OpenOutEpForAio(int address, int bufferCount, int bufferSize, int bufferOffset)
    {
        CheckAioBuffers(bufferCount, bufferSize, bufferOffset);

        return new(_mountPath, address)
        {
            AioBufferCount = bufferCount,
            AioBufferSize = bufferSize,
            AioBufferOffset = bufferOffset,
        };
    }

    private static void CheckAioBuffers(int bufferCount, int bufferSize, int bufferOffset)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bufferCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bufferSize);
        ArgumentOutOfRangeException.ThrowIfNegative(bufferOffset);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(bufferOffset, bufferSize);
    }

    protected static AioContext InitializeAio(ReadOnlySpan<UsbEp> eps)
    {
        ArgumentOutOfRangeException.ThrowIfZero(eps.Length);

        int maxEvents = 0;
        UsbEp[] ctxEps = new UsbEp[eps.Length];
        for (int i = 0; i < eps.Length; i++)
        {
            ArgumentNullException.ThrowIfNull(eps[i]);

            ctxEps[i] = eps[i];
            maxEvents += eps[i].AioBufferCount;
        }

        AioContext context = new(Aio.Init(eps.Length, maxEvents), maxEvents, ctxEps);
        try
        {
            for (int i = 0; i < eps.Length; i++)
            {
                Aio.InitEp(context._ptr, i, eps[i].FileHandle, eps[i].AioBufferCount, eps[i].AioBufferSize, eps[i].AioBufferOffset, eps[i] is UsbInEp);
            }
        }
        catch
        {
            context.Dispose();
            throw;
        }

        return context;
    }

    public virtual void ProcessControlRequests()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        Span<FfsEvent> events = stackalloc FfsEvent[64];
        int eventCount = _ep0.ReadEvents(events);
        foreach (FfsEvent ffsEvent in events.Slice(0, eventCount))
        {
            switch (ffsEvent.Type)
            {
                case FfsEventType.Bind:
                    IsBound = true;
                    OnBind();
                    StateChanged?.Invoke(this);
                    break;

                case FfsEventType.Unbind:
                    IsBound = false;
                    OnUnbind();
                    StateChanged?.Invoke(this);
                    Thread.Sleep(10); // Avoids some deadlock in the kernel when setting UDC = ""
                    break;

                case FfsEventType.Enable:
                    IsEnabled = true;
                    IsBound = true;
                    OnEnable();
                    StateChanged?.Invoke(this);
                    break;

                case FfsEventType.Disable:
                    IsEnabled = false;
                    OnDisable();
                    StateChanged?.Invoke(this);
                    break;

                case FfsEventType.Suspend:
                    IsSuspended = true;
                    OnSuspend();
                    StateChanged?.Invoke(this);
                    break;

                case FfsEventType.Resume:
                    IsSuspended = false;
                    OnResume();
                    StateChanged?.Invoke(this);
                    break;

                case FfsEventType.Setup:
                    OnSetup(ffsEvent.Setup, _ep0);
                    break;

                default:
                    break;
            }
        }
    }

    public event Action<FunctionFs>? StateChanged;

    protected virtual void OnBind() { }

    protected virtual void OnUnbind() { }

    protected virtual void OnEnable() { }

    protected virtual void OnDisable() { }

    protected virtual void OnSuspend() { }

    protected virtual void OnResume() { }

    protected virtual void OnSetup(UsbControlRequest request, UsbEp0 ep0)
    {
        if (request.IsDirectionIn)
        {
            ep0.Write([]);
        }
        else
        {
            ep0.ReadExactly([]);
        }
    }

    private int _disposed;
    public bool IsDisposed => _disposed != 0;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        DisposeInternal();
    }

    protected virtual void DisposeInternal()
    {
        _ep0.Dispose();
    }
}
