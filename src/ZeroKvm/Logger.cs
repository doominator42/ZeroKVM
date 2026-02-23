using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ZeroKvm;

internal static class Logger
{
    public static bool DebugLogEnabled { get; set; }

    public static bool StatisticsEnabled { get; set; }

    public static void StartStatisticsLogger(CancellationToken cancellationToken)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    ReportStatistics();
                    await Task.Delay(1000, cancellationToken);
                }
            }
            catch { }
        });
    }

    public static void LogDebug(string message)
    {
        if (DebugLogEnabled)
        {
            Console.WriteLine(message);
        }
    }

    public static void LogDebug<TArgs>(Func<TArgs, string> formatter, TArgs args)
        where TArgs : allows ref struct
    {
        if (DebugLogEnabled)
        {
            Console.WriteLine(formatter(args));
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void LogError(string message, Exception? exception = null)
    {
        Console.Error.WriteLine(message);
        if (exception is not null)
        {
            Console.Error.WriteLine(exception.ToString());
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void LogError(Exception exception)
    {
        Console.Error.WriteLine(exception.ToString());
    }

    private static long _statisticsLastTimestamp = 0;
    private static Statistics _lastStatistics;

    public static Statistics CurrentStatistics;

    public static void ReportStatistics()
    {
        long timestamp = Stopwatch.GetTimestamp();
        TimeSpan elapsed = Stopwatch.GetElapsedTime(_statisticsLastTimestamp, timestamp);
        if (elapsed > TimeSpan.FromMilliseconds(1000))
        {
            Statistics stats = CurrentStatistics;
            Statistics lastStats = _lastStatistics;
            double elapsedSeconds = elapsed.TotalSeconds;
            long framesCopied = stats.TotalFramesCopied - lastStats.TotalFramesCopied;
            long videoFrames = stats.TotalVideoFramesEncoded - lastStats.TotalVideoFramesEncoded;
            Console.WriteLine(
                $"DL data: {stats.DlTotalReceived / 1000} kB ({(stats.DlTotalReceived - lastStats.DlTotalReceived) / 1000 / elapsedSeconds,5:0} kB/s), " +
                $"DL I/O: {Stopwatch.GetElapsedTime(lastStats.DlTotalIoTime, stats.DlTotalIoTime).TotalMilliseconds / framesCopied,5:0.0}ms, " +
                $"DL decode: {Stopwatch.GetElapsedTime(lastStats.DlTotalProcessTime, stats.DlTotalProcessTime).TotalMilliseconds / framesCopied,5:0.0}ms, " +
                $"FPS: {framesCopied / elapsedSeconds,2:0}, " +
                $"Skipped Frames: {stats.TotalFramesSkipped} ({(stats.TotalFramesSkipped - lastStats.TotalFramesSkipped) / elapsedSeconds,2:0}/s), " +
                $"Frame copy: {Stopwatch.GetElapsedTime(lastStats.TotalFrameCopyTime, stats.TotalFrameCopyTime).TotalMilliseconds / framesCopied,5:0.0}ms, " +
                $"Video FPS: {videoFrames / elapsedSeconds,2:0}, " +
                $"Video encode: {Stopwatch.GetElapsedTime(lastStats.TotalVideoFramesEncodeTime, stats.TotalVideoFramesEncodeTime).TotalMilliseconds / videoFrames,5:0.0}ms, " +
                $"Total GC: {GC.GetTotalAllocatedBytes() / 1000} kB");
            _lastStatistics = stats;
            _statisticsLastTimestamp = timestamp;
        }
    }

    public struct Statistics
    {
        public long DlTotalReceived;
        public long DlTotalIoTime;
        public long DlTotalProcessTime;
        public long TotalFramesCopied;
        public long TotalFramesSkipped;
        public long TotalFrameCopyTime;
        public long TotalVideoFramesEncoded;
        public long TotalVideoFramesEncodeTime;
    }
}
