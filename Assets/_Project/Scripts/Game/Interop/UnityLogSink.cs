using RaidSim.Core.Diagnostics;
using UnityEngine;

namespace RaidSim.Game.Interop
{
    /// <summary>
    /// Forwards kernel diagnostics to the Unity console.
    /// </summary>
    /// <remarks>
    /// <para>The kernel never calls <c>UnityEngine.Debug</c> itself; it writes to an
    /// <see cref="ISimLogSink"/> and Unity installs this one during bootstrap.</para>
    /// <para>The channel mask and severity floor are settable so a noisy subsystem can be silenced
    /// during a long simulation run without recompiling.</para>
    /// </remarks>
    public sealed class UnityLogSink : ISimLogSink
    {
        public UnityLogSink(LogChannel channels = LogChannel.All, LogSeverity minimumSeverity = LogSeverity.Info)
        {
            Channels = channels;
            MinimumSeverity = minimumSeverity;
        }

        public LogChannel Channels { get; set; }

        public LogSeverity MinimumSeverity { get; set; }

        public bool IsEnabled(LogChannel channel, LogSeverity severity) =>
            severity >= MinimumSeverity && (Channels & channel) != 0;

        public void Log(LogChannel channel, LogSeverity severity, string message)
        {
            string line = $"[{channel}] {message}";
            switch (severity)
            {
                case LogSeverity.Error:
                    Debug.LogError(line);
                    break;
                case LogSeverity.Warning:
                    Debug.LogWarning(line);
                    break;
                default:
                    Debug.Log(line);
                    break;
            }
        }
    }
}
