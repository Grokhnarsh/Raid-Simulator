using System.Collections.Generic;
using EmberDepths.Core.Grid;
using EmberDepths.Core.Sim;

namespace EmberDepths.Content
{
    /// <summary>
    /// Read-only view of a live actor, as seen from content code.
    ///
    /// Effects are authored as ScriptableObject data and must not reach into the
    /// Gameplay assembly, so this is the entire vocabulary they get for asking
    /// questions about a target.
    /// </summary>
    public interface IActorHandle
    {
        int Id { get; }
        string DisplayName { get; }
        Faction Faction { get; }
        GridCoord Cell { get; }
        bool IsAlive { get; }
        float Health { get; }
        float MaxHealth { get; }
        float HealthFraction { get; }
        ActorFlags Flags { get; }
        EnemyRank Rank { get; }
        bool HasStatus(StatusEffectDefinition def);
        int StatusStacks(StatusEffectDefinition def);
    }

    /// <summary>
    /// Everything an <see cref="AbilityEffect"/> is allowed to do to the world.
    ///
    /// This interface is the contract between authored content and the simulation.
    /// Adding a new verb here is a deliberate act: it widens what designers can
    /// express without touching C#. Implemented by the Gameplay assembly.
    /// </summary>
    public interface IAbilityContext
    {
        IActorHandle Caster { get; }

        /// <summary>The actor the player or AI pointed at. Null for ground-targeted casts.</summary>
        IActorHandle PrimaryTarget { get; }

        /// <summary>Cell the cast resolved against — the target's cell, or the clicked ground.</summary>
        GridCoord TargetCell { get; }

        /// <summary>
        /// Everyone inside the ability's shape, already filtered by
        /// <see cref="AbilityDefinition.AffectsFaction"/> and line of sight.
        /// </summary>
        IReadOnlyList<IActorHandle> Targets { get; }

        /// <summary>Cells inside the ability's shape. Useful for hazards and visuals.</summary>
        IReadOnlyList<GridCoord> Cells { get; }

        /// <summary>Ability-scoped random stream. Seeded from the run, so rolls replay.</summary>
        DeterministicRandom Rng { get; }

        SimClock Clock { get; }

        // --- verbs --------------------------------------------------------------

        /// <summary>
        /// <paramref name="amount"/> is a coefficient, not a final number: it is
        /// multiplied by the caster's Power before mitigation.
        /// </summary>
        void DealDamage(IActorHandle target, float amount, DamageType type, bool canCrit = true);

        void Heal(IActorHandle target, float amount, bool canCrit = true);

        /// <summary>Absorbs damage until spent or expired.</summary>
        void ApplyShield(IActorHandle target, float amount, float duration);

        void ApplyStatus(IActorHandle target, StatusEffectDefinition status, float duration, int stacks = 1);

        void RemoveStatus(IActorHandle target, StatusEffectDefinition status);

        /// <summary>
        /// Strips up to <paramref name="count"/> dispellable effects. Returns how
        /// many went, so an ability can react to a wasted cast.
        /// </summary>
        int Dispel(IActorHandle target, int count, bool debuffs);

        /// <summary>Pushes a target away from <paramref name="from"/>. No-op on Unmovable actors.</summary>
        void Knockback(IActorHandle target, GridCoord from, int tiles);

        /// <summary>Teleports the caster, clamped to the nearest reachable cell.</summary>
        void Displace(IActorHandle target, GridCoord destination);

        /// <summary>Adds raw threat on top of what damage and healing already generated.</summary>
        void AddThreat(IActorHandle target, IActorHandle towards, float amount);

        /// <summary>Forces an enemy to treat <paramref name="taunter"/> as its top-threat target.</summary>
        void Taunt(IActorHandle enemy, IActorHandle taunter, float duration);

        /// <summary>Drops a persistent ground effect (fire pool, meteor scar).</summary>
        void SpawnHazard(HazardDefinition hazard, GridCoord cell, int radius, float duration);

        /// <summary>Spawns adds. Used by the boss's phase transitions.</summary>
        void Summon(EnemyDefinition enemy, GridCoord near, int count);

        /// <summary>Shows a timed warning shape before an effect lands.</summary>
        void Telegraph(TargetShape shape, GridCoord origin, GridCoord target, float leadTime);

        /// <summary>Fire-and-forget presentation hook. Never affects simulation state.</summary>
        void PlayVfx(string vfxKey, GridCoord at);

        void Log(string message);
    }
}
