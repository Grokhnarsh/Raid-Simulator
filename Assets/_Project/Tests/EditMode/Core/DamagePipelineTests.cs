using NUnit.Framework;
using RaidSim.Core.Combat;
using RaidSim.Core.Common;
using RaidSim.Core.Randomness;
using RaidSim.Core.Stats;

namespace RaidSim.Tests.Core
{
    [TestFixture]
    public sealed class DamagePipelineTests
    {
        private const float Tolerance = 0.01f;

        /// <summary>
        /// Tuning chosen so the numbers in these tests are easy to verify by hand: at level 1 the
        /// mitigation constant is 100, so 100 armour is exactly 50% reduction.
        /// </summary>
        private static CombatTuning Tuning(float maximumMitigation = 0.75f) =>
            new CombatTuning(
                armorConstantPerLevel: 100f,
                resistanceConstantPerLevel: 100f,
                maximumMitigation: maximumMitigation);

        private static DamagePipeline Pipeline(IRandomSource random = null, CombatTuning tuning = null) =>
            new DamagePipeline(tuning ?? Tuning(), random ?? FixedRandomSource.AlwaysFails());

        private static TestEntity Attacker(float attackPower = 0f, float spellPower = 0f) =>
            new TestEntity("attacker", Faction.Raid)
                .WithStat(StatType.AttackPower, attackPower)
                .WithStat(StatType.SpellPower, spellPower);

        private static TestEntity Victim(float maxHealth = 1000f, float armor = 0f, float resistance = 0f) =>
            new TestEntity("victim", Faction.Enemy, maxHealth: maxHealth)
                .WithStat(StatType.Armor, armor)
                .WithStat(StatType.Resistance, resistance);

        [Test]
        public void FlatDamage_IsAppliedUnchanged_WithNoModifiers()
        {
            DamageResult result = Pipeline().Resolve(
                new DamageRequest(Attacker(), Victim(), 100f, DamageType.Physical));

            Assert.That(result.Amount, Is.EqualTo(100f).Within(Tolerance));
            Assert.That(result.Applied, Is.EqualTo(100f).Within(Tolerance));
        }

        [Test]
        public void PowerCoefficient_AddsTheSourcesAttackPower_ForPhysicalDamage()
        {
            var request = new DamageRequest(
                Attacker(attackPower: 200f), Victim(), 100f, DamageType.Physical, powerCoefficient: 0.5f);

            // 100 + (200 * 0.5) = 200
            Assert.That(Pipeline().Resolve(request).Amount, Is.EqualTo(200f).Within(Tolerance));
        }

        [Test]
        public void PowerCoefficient_UsesSpellPower_ForMagicDamage()
        {
            var request = new DamageRequest(
                Attacker(attackPower: 999f, spellPower: 100f), Victim(), 50f, DamageType.Magic,
                powerCoefficient: 1f);

            // Attack power must not contribute to a spell: 50 + (100 * 1) = 150
            Assert.That(Pipeline().Resolve(request).Amount, Is.EqualTo(150f).Within(Tolerance));
        }

        [Test]
        public void PowerCoefficient_IsIgnored_ForTrueDamage()
        {
            var request = new DamageRequest(
                Attacker(attackPower: 500f, spellPower: 500f), Victim(), 100f, DamageType.True,
                powerCoefficient: 2f);

            Assert.That(Pipeline().Resolve(request).Amount, Is.EqualTo(100f).Within(Tolerance),
                "True damage must stay predictable so mechanics built on it cannot be out-geared.");
        }

        [Test]
        public void AbilityMultiplier_ScalesTheWholeHit()
        {
            var request = new DamageRequest(
                Attacker(attackPower: 100f), Victim(), 100f, DamageType.Physical,
                powerCoefficient: 1f, abilityMultiplier: 2f);

            // (100 + 100) * 2 = 400
            Assert.That(Pipeline().Resolve(request).Amount, Is.EqualTo(400f).Within(Tolerance));
        }

        [Test]
        public void Armor_ReducesPhysicalDamage_OnTheDocumentedCurve()
        {
            // rating / (rating + K), K = 100 at level 1, so 100 armour halves the hit.
            DamageResult result = Pipeline().Resolve(
                new DamageRequest(Attacker(), Victim(armor: 100f), 100f, DamageType.Physical));

            Assert.That(result.Amount, Is.EqualTo(50f).Within(Tolerance));
            Assert.That(result.Mitigated, Is.EqualTo(50f).Within(Tolerance));
        }

        [Test]
        public void Armor_DoesNotReduceMagicDamage()
        {
            DamageResult result = Pipeline().Resolve(
                new DamageRequest(Attacker(), Victim(armor: 10000f), 100f, DamageType.Magic));

            Assert.That(result.Amount, Is.EqualTo(100f).Within(Tolerance));
            Assert.That(result.Mitigated, Is.Zero);
        }

        [Test]
        public void Resistance_ReducesMagicDamage()
        {
            DamageResult result = Pipeline().Resolve(
                new DamageRequest(Attacker(), Victim(resistance: 300f), 100f, DamageType.Magic));

            // 300 / (300 + 100) = 75% reduction
            Assert.That(result.Amount, Is.EqualTo(25f).Within(Tolerance));
        }

        [Test]
        public void Resistance_DoesNotReducePhysicalDamage()
        {
            DamageResult result = Pipeline().Resolve(
                new DamageRequest(Attacker(), Victim(resistance: 10000f), 100f, DamageType.Physical));

            Assert.That(result.Amount, Is.EqualTo(100f).Within(Tolerance));
        }

        [Test]
        public void TrueDamage_IgnoresBothMitigationStats()
        {
            DamageResult result = Pipeline().Resolve(new DamageRequest(
                Attacker(), Victim(armor: 10000f, resistance: 10000f), 100f, DamageType.True));

            Assert.That(result.Amount, Is.EqualTo(100f).Within(Tolerance));
            Assert.That(result.Mitigated, Is.Zero);
        }

        [Test]
        public void Mitigation_HasDiminishingReturns()
        {
            DamagePipeline pipeline = Pipeline();

            float low = pipeline.Calculate(
                new DamageRequest(Attacker(), Victim(armor: 100f), 100f, DamageType.Physical),
                isCritical: false, out _);
            float high = pipeline.Calculate(
                new DamageRequest(Attacker(), Victim(armor: 200f), 100f, DamageType.Physical),
                isCritical: false, out _);

            // Doubling armour from 100 to 200 must not double the reduction: 50% becomes 66.7%,
            // not 100%. This is why armour can never reach immunity.
            Assert.That(low, Is.EqualTo(50f).Within(Tolerance));
            Assert.That(high, Is.EqualTo(33.33f).Within(0.1f));
        }

        [Test]
        public void Mitigation_IsCappedByTheAuthoredCeiling()
        {
            DamageResult result = new DamagePipeline(Tuning(maximumMitigation: 0.75f), FixedRandomSource.AlwaysFails())
                .Resolve(new DamageRequest(Attacker(), Victim(armor: 1000000f), 100f, DamageType.Physical));

            Assert.That(result.Amount, Is.EqualTo(25f).Within(Tolerance),
                "No amount of rating may exceed the authored mitigation ceiling.");
        }

        [Test]
        public void Mitigation_IsWorthLessAgainstAHigherLevelAttacker()
        {
            var lowLevel = new TestEntity("low", Faction.Raid, level: 1);
            var highLevel = new TestEntity("high", Faction.Raid, level: 10);
            TestEntity victim = Victim(armor: 100f);
            DamagePipeline pipeline = Pipeline();

            float fromLow = pipeline.Calculate(
                new DamageRequest(lowLevel, victim, 100f, DamageType.Physical), false, out _);
            float fromHigh = pipeline.Calculate(
                new DamageRequest(highLevel, victim, 100f, DamageType.Physical), false, out _);

            Assert.That(fromHigh, Is.GreaterThan(fromLow),
                "The same armour must protect less against a stronger attacker, with no special case.");
        }

        [Test]
        public void CriticalStrike_MultipliesByTheSourcesCriticalMultiplier()
        {
            TestEntity attacker = Attacker()
                .WithStat(StatType.CriticalChance, 1f)
                .WithStat(StatType.CriticalMultiplier, 2f);

            DamageResult result = Pipeline(FixedRandomSource.AlwaysSucceeds())
                .Resolve(new DamageRequest(attacker, Victim(), 100f, DamageType.Physical));

            Assert.That(result.WasCritical, Is.True);
            Assert.That(result.Amount, Is.EqualTo(200f).Within(Tolerance));
        }

        [Test]
        public void CriticalStrike_DoesNotHappen_WhenTheRollFails()
        {
            TestEntity attacker = Attacker()
                .WithStat(StatType.CriticalChance, 0.5f)
                .WithStat(StatType.CriticalMultiplier, 2f);

            DamageResult result = Pipeline(FixedRandomSource.AlwaysFails())
                .Resolve(new DamageRequest(attacker, Victim(), 100f, DamageType.Physical));

            Assert.That(result.WasCritical, Is.False);
            Assert.That(result.Amount, Is.EqualTo(100f).Within(Tolerance));
        }

        [Test]
        public void CriticalStrike_IsSuppressed_WhenTheRequestForbidsIt()
        {
            TestEntity attacker = Attacker()
                .WithStat(StatType.CriticalChance, 1f)
                .WithStat(StatType.CriticalMultiplier, 3f);

            DamageResult result = Pipeline(FixedRandomSource.AlwaysSucceeds()).Resolve(
                new DamageRequest(attacker, Victim(), 100f, DamageType.Physical, canCritical: false));

            Assert.That(result.WasCritical, Is.False);
            Assert.That(result.Amount, Is.EqualTo(100f).Within(Tolerance));
        }

        [Test]
        public void CriticalStrike_IsMitigatedLikeAnyOtherHit()
        {
            TestEntity attacker = Attacker()
                .WithStat(StatType.CriticalChance, 1f)
                .WithStat(StatType.CriticalMultiplier, 2f);

            DamageResult result = Pipeline(FixedRandomSource.AlwaysSucceeds()).Resolve(
                new DamageRequest(attacker, Victim(armor: 100f), 100f, DamageType.Physical));

            // Armour applies to the whole hit including the critical bonus: 200 halved is 100.
            Assert.That(result.Amount, Is.EqualTo(100f).Within(Tolerance));
        }

        [Test]
        public void DamageDoneModifier_ScalesOutgoingDamage()
        {
            TestEntity attacker = Attacker();
            attacker.Stats.AddModifier(StatModifier.PercentAdditive(StatType.DamageDoneModifier, 0.25f));

            Assert.That(Pipeline().Resolve(new DamageRequest(attacker, Victim(), 100f, DamageType.Physical)).Amount,
                Is.EqualTo(125f).Within(Tolerance));
        }

        [Test]
        public void DamageTakenModifier_AppliesAfterMitigation()
        {
            TestEntity victim = Victim(armor: 100f);
            victim.Stats.AddModifier(StatModifier.PercentMultiplicative(StatType.DamageTakenModifier, -0.5f));

            DamageResult result = Pipeline().Resolve(
                new DamageRequest(Attacker(), victim, 100f, DamageType.Physical));

            // 100 halved by armour is 50, halved again by the cooldown is 25. A reduction cooldown
            // must be worth the same proportion whatever the target's armour happens to be.
            Assert.That(result.Amount, Is.EqualTo(25f).Within(Tolerance));
        }

        [Test]
        public void FullImmunity_LeavesTheTargetUnharmed()
        {
            TestEntity victim = Victim();
            victim.Stats.AddModifier(StatModifier.PercentAdditive(StatType.DamageTakenModifier, -1f));

            DamageResult result = Pipeline().Resolve(
                new DamageRequest(Attacker(), victim, 500f, DamageType.True));

            Assert.That(result.Amount, Is.Zero);
            Assert.That(victim.Health.IsFull, Is.True);
        }

        [Test]
        public void Overkill_IsThePartOfTheBlowThatExceededRemainingHealth()
        {
            TestEntity victim = Victim(maxHealth: 100f);

            DamageResult result = Pipeline().Resolve(
                new DamageRequest(Attacker(), victim, 400f, DamageType.True));

            Assert.That(result.Amount, Is.EqualTo(400f).Within(Tolerance));
            Assert.That(result.Applied, Is.EqualTo(100f).Within(Tolerance));
            Assert.That(result.Overkill, Is.EqualTo(300f).Within(Tolerance));
            Assert.That(result.WasLethal, Is.True);
        }

        [Test]
        public void NonLethalDamage_ReportsNoOverkill()
        {
            DamageResult result = Pipeline().Resolve(
                new DamageRequest(Attacker(), Victim(maxHealth: 1000f), 100f, DamageType.True));

            Assert.That(result.Overkill, Is.Zero);
            Assert.That(result.WasLethal, Is.False);
        }

        [Test]
        public void DamageAgainstACorpse_DoesNothing()
        {
            TestEntity victim = Victim();
            victim.Kill();

            DamageResult result = Pipeline().Resolve(
                new DamageRequest(Attacker(), victim, 100f, DamageType.True));

            Assert.That(result.DidAnything, Is.False);
            Assert.That(result.WasLethal, Is.False, "A corpse must not be reported as freshly killed.");
        }

        [Test]
        public void ARequestWithNoTarget_ResolvesToNothing()
        {
            Assert.That(
                Pipeline().Resolve(new DamageRequest(Attacker(), null, 100f, DamageType.Physical)).DidAnything,
                Is.False);
        }

        [Test]
        public void DamageWithNoSource_StillLands()
        {
            // Environmental damage and unattributed mechanics have no attacker.
            DamageResult result = Pipeline().Resolve(
                new DamageRequest(null, Victim(), 100f, DamageType.True));

            Assert.That(result.Applied, Is.EqualTo(100f).Within(Tolerance));
            Assert.That(result.Source, Is.EqualTo(EntityId.None));
        }

        [Test]
        public void Calculate_DoesNotTouchTheTarget()
        {
            TestEntity victim = Victim();

            Pipeline().Calculate(
                new DamageRequest(Attacker(), victim, 100f, DamageType.True), isCritical: false, out _);

            Assert.That(victim.Health.IsFull, Is.True,
                "A preview must never be able to hurt anything.");
        }
    }
}
