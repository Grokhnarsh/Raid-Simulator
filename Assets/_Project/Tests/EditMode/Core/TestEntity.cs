using RaidSim.Core.Common;
using RaidSim.Core.Entities;
using RaidSim.Core.Mathematics;
using RaidSim.Core.Stats;
using RaidSim.Core.Vitals;

namespace RaidSim.Tests.Core
{
    /// <summary>
    /// Minimal <see cref="ICombatEntity"/> for tests.
    /// </summary>
    /// <remarks>
    /// Its existence is itself an architecture check: if a kernel system ever needs more than the
    /// published interfaces to work, this stub stops compiling and the leak is caught immediately.
    /// </remarks>
    internal sealed class TestEntity : ICombatEntity
    {
        public TestEntity(
            string displayName,
            Faction faction = Faction.Raid,
            CombatRole role = CombatRole.None,
            float maxHealth = 100f,
            Vec3 position = default,
            float radius = 0.5f,
            int level = 1)
        {
            Id = EntityId.Next();
            DisplayName = displayName;
            Faction = faction;
            Role = role;
            Position = position;
            Radius = radius;
            Level = level;
            Facing = Vec3.Forward;

            Stats = new StatBlock(new StatSet().With(StatType.MaxHealth, maxHealth));
            Health = new Health(Stats);
            Resource = new ResourcePool(Stats, ResourceKind.None);
        }

        public EntityId Id { get; }

        public string DisplayName { get; }

        public Faction Faction { get; set; }

        public Vec3 Position { get; set; }

        public Vec3 Facing { get; set; }

        public float Radius { get; set; }

        public bool IsAlive => Health.IsAlive;

        public StatBlock Stats { get; }

        public Health Health { get; }

        public ResourcePool Resource { get; }

        public CombatRole Role { get; }

        public int Level { get; }

        public TestEntity Kill()
        {
            Health.Empty();
            return this;
        }

        /// <summary>Sets a base stat. Fluent so a test can describe an entity in one expression.</summary>
        public TestEntity WithStat(StatType stat, float value)
        {
            Stats.SetBase(stat, value);
            return this;
        }
    }
}
