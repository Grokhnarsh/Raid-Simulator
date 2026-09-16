using NUnit.Framework;
using RaidSim.Core.Combat;
using RaidSim.Core.Common;
using RaidSim.Core.Randomness;
using RaidSim.Core.Stats;

namespace RaidSim.Tests.Core
{
    [TestFixture]
    public sealed class HealingPipelineTests
    {
        private const float Tolerance = 0.01f;

        private static HealingPipeline Pipeline(IRandomSource random = null) =>
            new HealingPipeline(random ?? FixedRandomSource.AlwaysFails());

        private static TestEntity Healer(float spellPower = 0f) =>
            new TestEntity("healer", Faction.Raid, CombatRole.Healer)
                .WithStat(StatType.SpellPower, spellPower);

        private static TestEntity Wounded(float maxHealth = 1000f, float missing = 500f)
        {
            var entity = new TestEntity("wounded", Faction.Raid, maxHealth: maxHealth);
            entity.Health.Remove(missing);
            return entity;
        }

        [Test]
        public void FlatHealing_IsAppliedUnchanged()
        {
            HealResult result = Pipeline().Resolve(new HealRequest(Healer(), Wounded(), 200f));

            Assert.That(result.Applied, Is.EqualTo(200f).Within(Tolerance));
            Assert.That(result.Overhealing, Is.Zero);
        }

        [Test]
        public void PowerCoefficient_AddsSpellPower()
        {
            var request = new HealRequest(Healer(spellPower: 300f), Wounded(), 100f, powerCoefficient: 0.5f);

            // 100 + (300 * 0.5) = 250
            Assert.That(Pipeline().Resolve(request).Amount, Is.EqualTo(250f).Within(Tolerance));
        }

        [Test]
        public void HealingScalesFromSpellPower_EvenForAPhysicalClass()
        {
            TestEntity tank = new TestEntity("tank", Faction.Raid, CombatRole.Tank)
                .WithStat(StatType.AttackPower, 500f)
                .WithStat(StatType.SpellPower, 100f);

            var request = new HealRequest(tank, Wounded(), 0f, powerCoefficient: 1f);

            Assert.That(Pipeline().Resolve(request).Amount, Is.EqualTo(100f).Within(Tolerance),
                "Attack power must never contribute to healing.");
        }

        [Test]
        public void AbilityMultiplier_ScalesTheHeal()
        {
            var request = new HealRequest(Healer(), Wounded(), 100f, abilityMultiplier: 2.5f);

            Assert.That(Pipeline().Resolve(request).Amount, Is.EqualTo(250f).Within(Tolerance));
        }

        [Test]
        public void CriticalHeal_MultipliesByTheCriticalMultiplier()
        {
            TestEntity healer = Healer()
                .WithStat(StatType.CriticalChance, 1f)
                .WithStat(StatType.CriticalMultiplier, 2f);

            HealResult result = Pipeline(FixedRandomSource.AlwaysSucceeds())
                .Resolve(new HealRequest(healer, Wounded(), 100f));

            Assert.That(result.WasCritical, Is.True);
            Assert.That(result.Amount, Is.EqualTo(200f).Within(Tolerance));
        }

        [Test]
        public void HealingDoneModifier_ScalesOutgoingHealing()
        {
            TestEntity healer = Healer();
            healer.Stats.AddModifier(StatModifier.PercentAdditive(StatType.HealingDoneModifier, 0.2f));

            Assert.That(Pipeline().Resolve(new HealRequest(healer, Wounded(), 100f)).Amount,
                Is.EqualTo(120f).Within(Tolerance));
        }

        [Test]
        public void Overhealing_IsThePartThatLandedOnAFullPool()
        {
            HealResult result = Pipeline().Resolve(
                new HealRequest(Healer(), Wounded(maxHealth: 1000f, missing: 100f), 400f));

            Assert.That(result.Applied, Is.EqualTo(100f).Within(Tolerance));
            Assert.That(result.Overhealing, Is.EqualTo(300f).Within(Tolerance),
                "Healers are judged on effective healing, so the wasted part has to be reported.");
        }

        [Test]
        public void HealingAFullTarget_IsEntirelyOverhealing()
        {
            var healthy = new TestEntity("healthy", Faction.Raid, maxHealth: 1000f);

            HealResult result = Pipeline().Resolve(new HealRequest(Healer(), healthy, 250f));

            Assert.That(result.Applied, Is.Zero);
            Assert.That(result.Overhealing, Is.EqualTo(250f).Within(Tolerance));
        }

        [Test]
        public void HealingDoesNotResurrect()
        {
            var corpse = new TestEntity("corpse", Faction.Raid, maxHealth: 1000f);
            corpse.Kill();

            HealResult result = Pipeline().Resolve(new HealRequest(Healer(), corpse, 500f));

            Assert.That(corpse.IsAlive, Is.False);
            Assert.That(result.Applied, Is.Zero);
            Assert.That(result.Overhealing, Is.EqualTo(500f).Within(Tolerance),
                "The whole heal was wasted, and the healer's statistics should say so.");
        }

        [Test]
        public void Calculate_DoesNotTouchTheTarget()
        {
            TestEntity wounded = Wounded();
            float before = wounded.Health.Current;

            Pipeline().Calculate(new HealRequest(Healer(), wounded, 500f), isCritical: false);

            Assert.That(wounded.Health.Current, Is.EqualTo(before).Within(Tolerance));
        }

        [Test]
        public void ARequestWithNoTarget_ResolvesToNothing()
        {
            Assert.That(Pipeline().Resolve(new HealRequest(Healer(), null, 100f)).DidAnything, Is.False);
        }
    }
}
