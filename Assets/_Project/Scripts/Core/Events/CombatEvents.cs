using RaidSim.Core.Combat;
using RaidSim.Core.Common;

namespace RaidSim.Core.Events
{
    /// <summary>
    /// Raised when an attack or cast begins, before it resolves.
    /// </summary>
    /// <remarks>
    /// Separate from the damage event because animation, cast bars and (from Phase 5) the AI's
    /// interrupt logic all need to react at the start of a swing rather than at its landing.
    /// </remarks>
    public readonly struct AttackStartedEvent
    {
        public readonly EntityId Source;
        public readonly EntityId Target;

        /// <summary>Presentation-only name of the attack. Never a gameplay condition.</summary>
        public readonly string SourceLabel;

        public AttackStartedEvent(EntityId source, EntityId target, string sourceLabel)
        {
            Source = source;
            Target = target;
            SourceLabel = sourceLabel;
        }
    }

    /// <summary>
    /// Raised after damage resolves, from the attacker's perspective.
    /// </summary>
    /// <remarks>
    /// Damage raises two events rather than one because the interested parties differ: the combat
    /// log, the statistics collector and the threat system care who dealt it, while the healer AI
    /// and the health UI care who took it. One event would force every listener to filter.
    /// </remarks>
    public readonly struct DamageDealtEvent
    {
        public readonly DamageResult Result;

        public DamageDealtEvent(in DamageResult result)
        {
            Result = result;
        }
    }

    /// <summary>Raised after damage resolves, from the victim's perspective.</summary>
    public readonly struct DamageTakenEvent
    {
        public readonly DamageResult Result;

        public DamageTakenEvent(in DamageResult result)
        {
            Result = result;
        }
    }

    /// <summary>Raised after healing resolves.</summary>
    public readonly struct HealAppliedEvent
    {
        public readonly HealResult Result;

        public HealAppliedEvent(in HealResult result)
        {
            Result = result;
        }
    }

    /// <summary>
    /// Raised once when an entity's health reaches zero.
    /// </summary>
    /// <remarks>
    /// The encounter, loot, threat, the UI and the AI all key off this. It fires exactly once per
    /// death: the combat system tracks who it has already reported, so a corpse taking further
    /// damage — or two systems both noticing — cannot produce a second event.
    /// </remarks>
    public readonly struct EntityDiedEvent
    {
        public readonly EntityId Entity;
        public readonly Faction Faction;

        /// <summary>Who landed the killing blow, or <see cref="EntityId.None"/> if unattributed.</summary>
        public readonly EntityId Killer;

        /// <summary>Presentation-only name of the killing blow.</summary>
        public readonly string SourceLabel;

        public EntityDiedEvent(EntityId entity, Faction faction, EntityId killer, string sourceLabel)
        {
            Entity = entity;
            Faction = faction;
            Killer = killer;
            SourceLabel = sourceLabel;
        }
    }
}
