using System;

namespace EmberDepths.Content
{
    /// <summary>
    /// Who fights whom. Kept as a small enum rather than a team id so that
    /// designer-facing dropdowns stay readable; add entries for charmed or
    /// neutral-until-provoked factions as the game grows.
    /// </summary>
    public enum Faction
    {
        Party = 0,
        Hostile = 1,
        /// <summary>Attacks nobody and is attacked by nobody (training dummies, props).</summary>
        Neutral = 2
    }

    /// <summary>
    /// Damage schools. Fire is the one that matters in this biome: the volcano's
    /// enemies deal it, and gear that resists it is the intended progression hook.
    /// </summary>
    public enum DamageType
    {
        Physical = 0,
        Fire = 1,
        Frost = 2,
        Arcane = 3,
        /// <summary>Ignores armour and every resistance. Reserve for boss enrage mechanics.</summary>
        True = 4
    }

    /// <summary>The classic trinity, used by the party AI and the threat model.</summary>
    public enum ActorRole
    {
        Tank = 0,
        Healer = 1,
        MeleeDps = 2,
        RangedDps = 3,
        CasterDps = 4
    }

    /// <summary>
    /// Difficulty tier. Drives health scaling, whether a health bar is shown, and
    /// whether the encounter counts as "cleared" for the room gate.
    /// </summary>
    public enum EnemyRank
    {
        Trash = 0,
        Elite = 1,
        Boss = 2
    }

    /// <summary>
    /// What an ability is allowed to point at. Validated before the cast starts, so
    /// a bad target produces a UI refusal rather than a wasted cooldown.
    /// </summary>
    public enum TargetKind
    {
        Self = 0,
        Ally = 1,
        Enemy = 2,
        /// <summary>Any cell, occupied or not — used by ground-targeted AoE.</summary>
        Ground = 3
    }

    /// <summary>
    /// Resource an ability spends. Separate pools per role keep the party's
    /// rotations from collapsing into one shared bar.
    /// </summary>
    public enum ResourceKind
    {
        None = 0,
        Mana = 1,
        Stamina = 2,
        Rage = 3
    }

    [Flags]
    public enum ActorFlags
    {
        None = 0,
        /// <summary>Immune to knockback and forced movement. Bosses set this.</summary>
        Unmovable = 1 << 0,
        /// <summary>Cannot be taunted; picks targets purely by its own logic.</summary>
        TauntImmune = 1 << 1,
        /// <summary>Walks over lava without taking terrain damage.</summary>
        LavaWalker = 1 << 2,
        /// <summary>Never blocks a cell — wisps and other incorporeal enemies.</summary>
        Incorporeal = 1 << 3
    }
}
