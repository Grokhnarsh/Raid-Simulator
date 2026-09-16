using System.Collections.Generic;

namespace RaidSim.Core.Diagnostics
{
    /// <summary>
    /// Keeps log lines in memory. Used by tests and by headless simulation runs that want to report
    /// warnings alongside their statistics.
    /// </summary>
    public sealed class CollectingLogSink : ISimLogSink
    {
        private readonly List<LogEntry> _entries = new List<LogEntry>();

        public CollectingLogSink(LogChannel channels = LogChannel.All, LogSeverity minimumSeverity = LogSeverity.Info)
        {
            Channels = channels;
            MinimumSeverity = minimumSeverity;
        }

        public LogChannel Channels { get; set; }

        public LogSeverity MinimumSeverity { get; set; }

        public IReadOnlyList<LogEntry> Entries => _entries;

        public bool IsEnabled(LogChannel channel, LogSeverity severity) =>
            severity >= MinimumSeverity && (Channels & channel) != 0;

        public void Log(LogChannel channel, LogSeverity severity, string message) =>
            _entries.Add(new LogEntry(channel, severity, message));

        public void Clear() => _entries.Clear();

        public int CountOf(LogSeverity severity)
        {
            int count = 0;
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Severity == severity)
                {
                    count++;
                }
            }

            return count;
        }

        public readonly struct LogEntry
        {
            public readonly LogChannel Channel;
            public readonly LogSeverity Severity;
            public readonly string Message;

            public LogEntry(LogChannel channel, LogSeverity severity, string message)
            {
                Channel = channel;
                Severity = severity;
                Message = message;
            }

            public override string ToString() => $"[{Severity}][{Channel}] {Message}";
        }
    }
}
