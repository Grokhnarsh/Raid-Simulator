using NUnit.Framework;
using RaidSim.Core.Stats;

namespace RaidSim.Tests.Core
{
    [TestFixture]
    public sealed class StatBlockTests
    {
        private const float Tolerance = 0.001f;

        [Test]
        public void Get_WithNoData_ReturnsTheNeutralValueForThatStat()
        {
            var block = new StatBlock();

            Assert.That(block.Get(StatType.AttackPower), Is.EqualTo(0f).Within(Tolerance));
            Assert.That(block.Get(StatType.DamageTakenModifier), Is.EqualTo(1f).Within(Tolerance),
                "Multiplier stats must default to 1 so an unauthored stat changes nothing.");
        }

        [Test]
        public void Get_ReturnsBaseValue_WhenNoModifiersApply()
        {
            var block = new StatBlock(new StatSet().With(StatType.MaxHealth, 2500f));

            Assert.That(block.Get(StatType.MaxHealth), Is.EqualTo(2500f).Within(Tolerance));
        }

        [Test]
        public void FlatModifier_AddsToTheBase()
        {
            var block = new StatBlock(new StatSet().With(StatType.AttackPower, 100f));

            block.AddModifier(StatModifier.Flat(StatType.AttackPower, 25f));

            Assert.That(block.Get(StatType.AttackPower), Is.EqualTo(125f).Within(Tolerance));
        }

        [Test]
        public void AdditivePercentModifiers_SumBeforeMultiplying()
        {
            var block = new StatBlock(new StatSet().With(StatType.AttackPower, 100f));

            block.AddModifier(StatModifier.PercentAdditive(StatType.AttackPower, 0.20f));
            block.AddModifier(StatModifier.PercentAdditive(StatType.AttackPower, 0.30f));

            // +20% and +30% is +50%, not +56%.
            Assert.That(block.Get(StatType.AttackPower), Is.EqualTo(150f).Within(Tolerance));
        }

        [Test]
        public void MultiplicativePercentModifiers_Compound()
        {
            var block = new StatBlock(new StatSet().With(StatType.DamageTakenModifier, 1f));

            block.AddModifier(StatModifier.PercentMultiplicative(StatType.DamageTakenModifier, -0.50f));
            block.AddModifier(StatModifier.PercentMultiplicative(StatType.DamageTakenModifier, -0.50f));

            // Two 50% reductions leave 25% damage taken, not 0%. This is why damage-reduction
            // cooldowns must be multiplicative.
            Assert.That(block.Get(StatType.DamageTakenModifier), Is.EqualTo(0.25f).Within(Tolerance));
        }

        [Test]
        public void ModifierOrder_IsFlatThenAdditiveThenMultiplicative()
        {
            var block = new StatBlock(new StatSet().With(StatType.SpellPower, 100f));

            block.AddModifier(StatModifier.Flat(StatType.SpellPower, 100f));
            block.AddModifier(StatModifier.PercentAdditive(StatType.SpellPower, 0.50f));
            block.AddModifier(StatModifier.PercentMultiplicative(StatType.SpellPower, 1.0f));

            // (100 + 100) * 1.5 * 2 = 600
            Assert.That(block.Get(StatType.SpellPower), Is.EqualTo(600f).Within(Tolerance));
        }

        [Test]
        public void RemoveModifier_RestoresThePreviousValue()
        {
            var block = new StatBlock(new StatSet().With(StatType.Armor, 500f));
            var buff = StatModifier.Flat(StatType.Armor, 250f);

            block.AddModifier(buff);
            Assert.That(block.Get(StatType.Armor), Is.EqualTo(750f).Within(Tolerance));

            Assert.That(block.RemoveModifier(buff), Is.True);
            Assert.That(block.Get(StatType.Armor), Is.EqualTo(500f).Within(Tolerance));
        }

        [Test]
        public void RemoveModifier_ReturnsFalse_WhenItWasNeverApplied()
        {
            var block = new StatBlock();

            Assert.That(block.RemoveModifier(StatModifier.Flat(StatType.Armor, 10f)), Is.False);
        }

        [Test]
        public void RemoveModifiersFromSource_RemovesOnlyThatSource()
        {
            var block = new StatBlock(new StatSet().With(StatType.AttackPower, 100f));
            const int expiringEffect = 77;
            const int otherEffect = 88;

            block.AddModifier(StatModifier.Flat(StatType.AttackPower, 50f, sourceKey: expiringEffect));
            block.AddModifier(StatModifier.Flat(StatType.AttackPower, 10f, sourceKey: otherEffect));

            int removed = block.RemoveModifiersFromSource(expiringEffect);

            Assert.That(removed, Is.EqualTo(1));
            Assert.That(block.Get(StatType.AttackPower), Is.EqualTo(110f).Within(Tolerance));
        }

        [Test]
        public void RemoveModifiersFromSource_RemovesEveryStatThatSourceTouched()
        {
            var block = new StatBlock();
            const int sourceKey = 99;

            block.AddModifier(StatModifier.Flat(StatType.AttackPower, 40f, sourceKey: sourceKey));
            block.AddModifier(StatModifier.Flat(StatType.Armor, 60f, sourceKey: sourceKey));

            Assert.That(block.RemoveModifiersFromSource(sourceKey), Is.EqualTo(2));
            Assert.That(block.Get(StatType.AttackPower), Is.Zero);
            Assert.That(block.Get(StatType.Armor), Is.Zero);
        }

        [Test]
        public void StatChanged_FiresWhenAModifierIsAdded()
        {
            var block = new StatBlock();
            StatType changed = StatType.None;
            block.StatChanged += stat => changed = stat;

            block.AddModifier(StatModifier.Flat(StatType.MaxHealth, 100f));

            Assert.That(changed, Is.EqualTo(StatType.MaxHealth));
        }

        [Test]
        public void SetBase_InvalidatesTheCachedValue()
        {
            var block = new StatBlock(new StatSet().With(StatType.MovementSpeed, 5f));
            Assert.That(block.Get(StatType.MovementSpeed), Is.EqualTo(5f).Within(Tolerance));

            block.SetBase(StatType.MovementSpeed, 7f);

            Assert.That(block.Get(StatType.MovementSpeed), Is.EqualTo(7f).Within(Tolerance));
        }

        [Test]
        public void CriticalChance_IsClampedToAProbability()
        {
            var block = new StatBlock(new StatSet().With(StatType.CriticalChance, 0.9f));

            block.AddModifier(StatModifier.Flat(StatType.CriticalChance, 0.5f));

            Assert.That(block.Get(StatType.CriticalChance), Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void Multipliers_CannotGoNegative()
        {
            var block = new StatBlock(new StatSet().With(StatType.DamageTakenModifier, 1f));

            // Over-stacked reductions must bottom out at immunity, never flip damage into healing.
            block.AddModifier(StatModifier.PercentAdditive(StatType.DamageTakenModifier, -2.0f));

            Assert.That(block.Get(StatType.DamageTakenModifier), Is.Zero);
        }

        [Test]
        public void Pools_CannotGoNegative()
        {
            var block = new StatBlock(new StatSet().With(StatType.MaxHealth, 100f));

            block.AddModifier(StatModifier.Flat(StatType.MaxHealth, -500f));

            Assert.That(block.Get(StatType.MaxHealth), Is.Zero);
        }

        [Test]
        public void ClearModifiers_ReturnsEveryStatToItsBase()
        {
            var block = new StatBlock(new StatSet().With(StatType.AttackPower, 100f));
            block.AddModifier(StatModifier.Flat(StatType.AttackPower, 100f));
            block.AddModifier(StatModifier.PercentAdditive(StatType.AttackPower, 1f));

            block.ClearModifiers();

            Assert.That(block.ModifierCount, Is.Zero);
            Assert.That(block.Get(StatType.AttackPower), Is.EqualTo(100f).Within(Tolerance));
        }

        [Test]
        public void GetInt_RoundsAwayFromZero()
        {
            var block = new StatBlock(new StatSet().With(StatType.MaxHealth, 100.5f));

            Assert.That(block.GetInt(StatType.MaxHealth), Is.EqualTo(101));
        }

        [Test]
        public void StatSetAdd_CombinesContributionsComponentWise()
        {
            var classStats = new StatSet().With(StatType.MaxHealth, 1000f).With(StatType.Armor, 100f);
            var equipment = new StatSet().With(StatType.Armor, 250f).With(StatType.AttackPower, 40f);

            StatSet total = StatSet.Add(classStats, equipment);

            Assert.That(total[StatType.MaxHealth], Is.EqualTo(1000f).Within(Tolerance));
            Assert.That(total[StatType.Armor], Is.EqualTo(350f).Within(Tolerance));
            Assert.That(total[StatType.AttackPower], Is.EqualTo(40f).Within(Tolerance));
        }
    }
}
