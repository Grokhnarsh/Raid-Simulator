using NUnit.Framework;
using RaidSim.Core.Stats;
using RaidSim.Core.Vitals;

namespace RaidSim.Tests.Core
{
    [TestFixture]
    public sealed class HealthTests
    {
        private const float Tolerance = 0.001f;

        private static Health MakeHealth(float maxHealth, out StatBlock stats)
        {
            stats = new StatBlock(new StatSet().With(StatType.MaxHealth, maxHealth));
            return new Health(stats);
        }

        [Test]
        public void NewPool_StartsFull()
        {
            Health health = MakeHealth(1000f, out _);

            Assert.That(health.Current, Is.EqualTo(1000f).Within(Tolerance));
            Assert.That(health.IsFull, Is.True);
            Assert.That(health.IsAlive, Is.True);
        }

        [Test]
        public void Remove_SubtractsAndReportsTheAmountApplied()
        {
            Health health = MakeHealth(1000f, out _);

            float applied = health.Remove(250f);

            Assert.That(applied, Is.EqualTo(250f).Within(Tolerance));
            Assert.That(health.Current, Is.EqualTo(750f).Within(Tolerance));
        }

        [Test]
        public void Remove_OnAKillingBlow_ReportsOnlyWhatWasLeft()
        {
            Health health = MakeHealth(100f, out _);

            float applied = health.Remove(400f);

            // The 300 difference is overkill; the damage pipeline reports it from this gap.
            Assert.That(applied, Is.EqualTo(100f).Within(Tolerance));
            Assert.That(health.Current, Is.Zero);
            Assert.That(health.IsAlive, Is.False);
        }

        [Test]
        public void Remove_NeverGoesBelowZero()
        {
            Health health = MakeHealth(100f, out _);

            health.Remove(100f);
            float applied = health.Remove(50f);

            Assert.That(applied, Is.Zero, "The dead take no further damage.");
            Assert.That(health.Current, Is.Zero);
        }

        [Test]
        public void Add_ReportsOnlyTheEffectiveHealing()
        {
            Health health = MakeHealth(1000f, out _);
            health.Remove(100f);

            float applied = health.Add(400f);

            // 300 of the 400 was overhealing.
            Assert.That(applied, Is.EqualTo(100f).Within(Tolerance));
            Assert.That(health.IsFull, Is.True);
        }

        [Test]
        public void Add_DoesNothingForTheDead()
        {
            Health health = MakeHealth(100f, out _);
            health.Empty();

            float applied = health.Add(50f);

            Assert.That(applied, Is.Zero, "Healing must not resurrect; that is an explicit operation.");
            Assert.That(health.IsAlive, Is.False);
        }

        [Test]
        public void Fraction_IsZero_WhenThePoolHasNoSize()
        {
            var stats = new StatBlock();
            var health = new Health(stats);

            Assert.That(health.Fraction, Is.Zero, "An unsized pool must not produce NaN in the UI.");
        }

        [Test]
        public void RaisingMaxHealth_DoesNotReadAsAHeal()
        {
            Health health = MakeHealth(1000f, out StatBlock stats);
            health.Remove(500f);

            stats.AddModifier(StatModifier.Flat(StatType.MaxHealth, 1000f));

            Assert.That(health.Max, Is.EqualTo(2000f).Within(Tolerance));
            Assert.That(health.Current, Is.EqualTo(500f).Within(Tolerance));
        }

        [Test]
        public void LoweringMaxHealth_ClampsCurrentHealth()
        {
            Health health = MakeHealth(1000f, out StatBlock stats);

            stats.AddModifier(StatModifier.Flat(StatType.MaxHealth, -600f));

            Assert.That(health.Current, Is.EqualTo(400f).Within(Tolerance));
            Assert.That(health.IsAlive, Is.True);
        }

        [Test]
        public void Set_ClampsIntoRange()
        {
            Health health = MakeHealth(100f, out _);

            health.Set(500f);
            Assert.That(health.Current, Is.EqualTo(100f).Within(Tolerance));

            health.Set(-10f);
            Assert.That(health.Current, Is.Zero);
        }
    }

    [TestFixture]
    public sealed class ResourcePoolTests
    {
        private const float Tolerance = 0.001f;

        private static ResourcePool MakePool(ResourceKind kind, float max, float regen = 0f)
        {
            var stats = new StatBlock(new StatSet()
                .With(StatType.MaxResource, max)
                .With(StatType.ResourceRegen, regen));
            return new ResourcePool(stats, kind);
        }

        [Test]
        public void ManaPool_StartsFull()
        {
            ResourcePool pool = MakePool(ResourceKind.Mana, 1000f);

            Assert.That(pool.Current, Is.EqualTo(1000f).Within(Tolerance));
        }

        [Test]
        public void RagePool_StartsEmpty()
        {
            ResourcePool pool = MakePool(ResourceKind.Rage, 100f);

            Assert.That(pool.Current, Is.Zero, "Builder resources are earned in combat, not granted.");
        }

        [Test]
        public void TrySpend_SucceedsAndDeducts_WhenAffordable()
        {
            ResourcePool pool = MakePool(ResourceKind.Mana, 1000f);

            Assert.That(pool.TrySpend(300f), Is.True);
            Assert.That(pool.Current, Is.EqualTo(700f).Within(Tolerance));
        }

        [Test]
        public void TrySpend_IsAllOrNothing_WhenTooExpensive()
        {
            ResourcePool pool = MakePool(ResourceKind.Mana, 100f);

            Assert.That(pool.TrySpend(150f), Is.False);
            Assert.That(pool.Current, Is.EqualTo(100f).Within(Tolerance),
                "A rejected cast must not partially drain the pool.");
        }

        [Test]
        public void TrySpend_SucceedsForAFreeAbility()
        {
            ResourcePool pool = MakePool(ResourceKind.None, 0f);

            Assert.That(pool.TrySpend(0f), Is.True);
        }

        [Test]
        public void Add_IsCappedAtMaximum()
        {
            ResourcePool pool = MakePool(ResourceKind.Rage, 100f);

            float applied = pool.Add(250f);

            Assert.That(applied, Is.EqualTo(100f).Within(Tolerance));
            Assert.That(pool.Current, Is.EqualTo(100f).Within(Tolerance));
        }

        [Test]
        public void Tick_AppliesRegenerationPerSecond()
        {
            ResourcePool pool = MakePool(ResourceKind.Mana, 1000f, regen: 50f);
            pool.Empty();

            pool.Tick(2f);

            Assert.That(pool.Current, Is.EqualTo(100f).Within(Tolerance));
        }

        [Test]
        public void Tick_WithoutRegeneration_ChangesNothing()
        {
            ResourcePool pool = MakePool(ResourceKind.Rage, 100f);

            pool.Tick(10f);

            Assert.That(pool.Current, Is.Zero);
        }

        [Test]
        public void ResetToEncounterStart_RespectsTheResourceKind()
        {
            ResourcePool mana = MakePool(ResourceKind.Mana, 500f);
            ResourcePool rage = MakePool(ResourceKind.Rage, 100f);
            mana.Empty();
            rage.Fill();

            mana.ResetToEncounterStart();
            rage.ResetToEncounterStart();

            Assert.That(mana.Current, Is.EqualTo(500f).Within(Tolerance));
            Assert.That(rage.Current, Is.Zero);
        }
    }
}
