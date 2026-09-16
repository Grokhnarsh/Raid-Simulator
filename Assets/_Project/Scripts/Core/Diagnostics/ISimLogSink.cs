namespace RaidSim.Core.Diagnostics
{
    public enum LogSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2,
    }

    /// <summary>
    /// Where the simulation kernel sends diagnostics.
    /// </summary>
    /// <remarks>
    /// The kernel must not call <c>UnityEngine.Debug</c>. Unity installs a sink that forwards to the
    /// console; headless runs install one that writes to stdout or collects lines for assertions.
    /// </remarks>
    public interface ISimLogSink
    {
        bool IsEnabled(LogChannel channel, LogSeverity severity);

        void Log(LogChannel channel, LogSeverity severity, string message);
    }

    public static class SimLogSinkExtensions
    {
        public static void Info(this ISimLogSink sink, LogChannel channel, string message)
        {
            if (sink.IsEnabled(channel, LogSeverity.Info))
            {
                sink.Log(channel, LogSeverity.Info, message);
            }
        }

        public static void Warn(this ISimLogSink sink, LogChannel channel, string message)
        {
            if (sink.IsEnabled(channel, LogSeverity.Warning))
            {
                sink.Log(channel, LogSeverity.Warning, message);
            }
        }

        public static void Error(this ISimLogSink sink, LogChannel channel, string message)
        {
            if (sink.IsEnabled(channel, LogSeverity.Error))
            {
                sink.Log(channel, LogSeverity.Error, message);
            }
        }
    }
}
