using System;

namespace RaidSim.Core.Diagnostics
{
    /// <summary>
    /// Logical log channels. Filtering happens per channel so a noisy subsystem can be silenced
    /// without turning off the rest.
    /// </summary>
    [Flags]
    public enum LogChannel
    {
        None = 0,
        Bootstrap = 1 << 0,
        Events = 1 << 1,
        Combat = 1 << 2,
        Abilities = 1 << 3,
        Effects = 1 << 4,
        Threat = 1 << 5,
        Ai = 1 << 6,
        Encounter = 1 << 7,
        Loot = 1 << 8,
        Ui = 1 << 9,
        Simulation = 1 << 10,
        All = ~0,
    }
}
