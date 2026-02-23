using System.Diagnostics;
using System.IO;
using Microsoft.Win32.SafeHandles;

namespace ZeroKvm;

internal static class Udc
{
    public enum State
    {
        Unknown,
        NotAttached,
        Attached,
        Powered,
        Reconnecting,
        Unauthenticated,
        Default,
        Address,
        Configured,
        Suspended,
    }

    private const string SysUdcPath = "/sys/class/udc";
    private const string StateFileName = "state";
    private const string SoftConnectFileName = "soft_connect";

    public static string[] GetUdcNames()
    {
        string[] paths = Directory.GetFileSystemEntries(SysUdcPath);
        for (int i = 0; i < paths.Length; i++)
        {
            paths[i] = Path.GetFileName(paths[i]);
        }

        return paths;
    }

    public static State GetState(string udc)
    {
        Span<byte> state = stackalloc byte[32];
        using (SafeFileHandle file = File.OpenHandle(Path.Combine(SysUdcPath, udc, StateFileName), FileMode.Open, FileAccess.Read))
        {
            state = state.Slice(0, RandomAccess.Read(file, state, 0)).TrimEnd((byte)'\n');
        }

        return state.SequenceEqual("not attached"u8) ? State.NotAttached :
            state.SequenceEqual("attached"u8) ? State.Attached :
            state.SequenceEqual("powered"u8) ? State.Powered :
            state.SequenceEqual("reconnecting"u8) ? State.Reconnecting :
            state.SequenceEqual("unauthenticated"u8) ? State.Unauthenticated :
            state.SequenceEqual("default"u8) ? State.Default :
            state.SequenceEqual("address"u8) ? State.Address :
            state.SequenceEqual("configured"u8) ? State.Configured :
            state.SequenceEqual("suspended"u8) ? State.Suspended :
            State.Unknown;
    }

    public static void SoftConnect(string udc, bool connect)
    {
        using SafeFileHandle file = File.OpenHandle(Path.Combine(SysUdcPath, udc, SoftConnectFileName), FileMode.Open, FileAccess.Write);
        RandomAccess.Write(file, connect ? "connect\n"u8 : "disconnect\n"u8, 0);
    }

    public static Task<State> WaitForStateAsync(string udc, Predicate<State> predicate, CancellationToken cancellationToken) =>
        WaitForStateAsync(udc, predicate, Timeout.InfiniteTimeSpan, cancellationToken);

    public static async Task<State> WaitForStateAsync(string udc, Predicate<State> predicate, TimeSpan timeout, CancellationToken cancellationToken)
    {
        long startTime = timeout.Ticks < 0 ? 0 : Stopwatch.GetTimestamp();

        State state = GetState(udc);
        while (!predicate(state) && (timeout.Ticks < 0 || Stopwatch.GetElapsedTime(startTime) < timeout))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(50);
            cancellationToken.ThrowIfCancellationRequested();
            state = GetState(udc);
        }

        return state;
    }

    public static bool IsAttachedState(this State state) => state is State.Attached or State.Configured;
}
