namespace RaidSim.Core.GameFlow
{
    /// <summary>
    /// The top-level mode the game is in.
    /// </summary>
    /// <remarks>
    /// Kept deliberately coarse. Encounter-level states (pulling, phase two, wiping) belong to the
    /// encounter state machine, not here; mixing the two is how a "GameManager" starts growing.
    /// </remarks>
    public enum GameState
    {
        /// <summary>Before bootstrap has finished wiring systems.</summary>
        None = 0,

        /// <summary>Systems are being constructed and content is loading.</summary>
        Booting = 1,

        /// <summary>Raid is loaded, simulation is running.</summary>
        Playing = 2,

        /// <summary>Simulation clock is frozen. Input for menus still works.</summary>
        Paused = 3,

        /// <summary>The encounter ended; results and loot are being shown.</summary>
        EncounterSummary = 4,
    }
}
