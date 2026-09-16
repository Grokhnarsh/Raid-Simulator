namespace RaidSim.Core.Diagnostics
{
    /// <summary>Discards everything. Default when no sink was supplied.</summary>
    public sealed class NullLogSink : ISimLogSink
    {
        public static readonly NullLogSink Instance = new NullLogSink();

        private NullLogSink()
        {
        }

        public bool IsEnabled(LogChannel channel, LogSeverity severity) => false;

        public void Log(LogChannel channel, LogSeverity severity, string message)
        {
        }
    }
}
