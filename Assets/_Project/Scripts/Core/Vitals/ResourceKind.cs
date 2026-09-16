namespace RaidSim.Core.Vitals
{
    /// <summary>
    /// The flavour of resource a class spends. Chosen per class in authored data.
    /// </summary>
    /// <remarks>
    /// Values are persisted in save files and referenced by authored assets; append, never renumber.
    /// </remarks>
    public enum ResourceKind
    {
        /// <summary>The class spends nothing. Abilities are gated by cooldowns alone.</summary>
        None = 0,

        /// <summary>Starts full, regenerates over time, runs dry under sustained casting.</summary>
        Mana = 1,

        /// <summary>Starts empty, built by dealing and taking damage.</summary>
        Rage = 2,

        /// <summary>Starts full, regenerates quickly, small pool.</summary>
        Energy = 3,

        /// <summary>Starts empty, built by spending another resource.</summary>
        Focus = 4,
    }

    public static class ResourceKindExtensions
    {
        /// <summary>
        /// Whether the pool begins an encounter at maximum. Builder resources start empty.
        /// </summary>
        public static bool StartsFull(this ResourceKind kind)
        {
            switch (kind)
            {
                case ResourceKind.Mana:
                case ResourceKind.Energy:
                    return true;
                default:
                    return false;
            }
        }
    }
}
