namespace RaidSim.Core.Simulation
{
    /// <summary>
    /// The simulation's notion of time.
    /// </summary>
    /// <remarks>
    /// Nothing in the kernel reads <c>UnityEngine.Time</c>. Cooldowns, cast bars, effect durations
    /// and damage-over-time ticks all measure against this clock, which is what lets the same code
    /// run at frame rate in the editor, paused on a keypress, and as fast as the CPU allows inside
    /// the headless encounter simulator.
    /// </remarks>
    public interface ISimulationClock
    {
        /// <summary>Seconds elapsed since the encounter started, excluding paused time.</summary>
        float Now { get; }

        /// <summary>Length of the tick currently being processed, in seconds. Zero while paused.</summary>
        float DeltaTime { get; }

        /// <summary>Number of ticks advanced since the clock was reset.</summary>
        long TickCount { get; }

        /// <summary>Whether the clock is currently advancing.</summary>
        bool IsPaused { get; }
    }
}
