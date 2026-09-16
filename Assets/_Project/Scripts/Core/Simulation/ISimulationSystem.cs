namespace RaidSim.Core.Simulation
{
    /// <summary>
    /// A system that needs a slice of each simulation tick.
    /// </summary>
    /// <remarks>
    /// <para>Systems are ticked in an explicit order by the encounter loop rather than relying on
    /// Unity's <c>Update</c> order. That ordering is part of the design — threat must settle before
    /// enemy AI picks a target, and AI must act before the combat resolution runs — and it must be
    /// readable in one place instead of spread across script execution-order settings.</para>
    /// <para>This also keeps the number of <c>Update</c> methods flat as content grows: a raid with
    /// forty players and thirty adds still has one tick loop, not seventy.</para>
    /// </remarks>
    public interface ISimulationSystem
    {
        /// <summary>
        /// Relative tick order. Lower runs earlier. Use the constants on <see cref="SystemOrder"/>
        /// rather than bare numbers.
        /// </summary>
        int Order { get; }

        /// <summary>Advances the system by <paramref name="deltaSeconds"/> of simulation time.</summary>
        void Tick(float deltaSeconds);
    }

    /// <summary>
    /// The canonical tick order. Named so the dependencies between systems are stated once, here,
    /// instead of being rediscovered from bug reports.
    /// </summary>
    public static class SystemOrder
    {
        /// <summary>Resource regeneration and other passive per-tick bookkeeping.</summary>
        public const int Vitals = 100;

        /// <summary>Cooldown and global-cooldown timers.</summary>
        public const int Cooldowns = 200;

        /// <summary>Buff/debuff ticks and expiry. Runs before anything reads a stat.</summary>
        public const int Effects = 300;

        /// <summary>Threat decay and taunt expiry. Must settle before AI reads the threat table.</summary>
        public const int Threat = 400;

        /// <summary>Boss phases and raid mechanics decide what happens this tick.</summary>
        public const int Encounter = 500;

        /// <summary>Enemy and raid AI choose targets, abilities and destinations.</summary>
        public const int Ai = 600;

        /// <summary>Casts progress and complete; abilities resolve.</summary>
        public const int Abilities = 700;

        /// <summary>Movement is integrated after everyone has decided where to go.</summary>
        public const int Locomotion = 800;

        /// <summary>Deaths, encounter end conditions and statistics.</summary>
        public const int CombatResolution = 900;
    }
}
