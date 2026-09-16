using RaidSim.Core.Common;

namespace RaidSim.Core.Events
{
    /// <summary>Raised after an entity joined the simulation.</summary>
    public readonly struct EntityRegisteredEvent
    {
        public readonly EntityId Entity;
        public readonly Faction Faction;

        public EntityRegisteredEvent(EntityId entity, Faction faction)
        {
            Entity = entity;
            Faction = faction;
        }
    }

    /// <summary>Raised after an entity left the simulation. Listeners must drop cached references.</summary>
    public readonly struct EntityUnregisteredEvent
    {
        public readonly EntityId Entity;
        public readonly Faction Faction;

        public EntityUnregisteredEvent(EntityId entity, Faction faction)
        {
            Entity = entity;
            Faction = faction;
        }
    }
}
