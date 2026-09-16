using System;
using NUnit.Framework;
using RaidSim.Core.Combat;
using RaidSim.Core.Randomness;

namespace RaidSim.Tests.Core
{
    [TestFixture]
    public sealed class DeterministicRandomSourceTests
    {
        [Test]
        public void TheSameSeed_ProducesTheSameSequence()
        {
            var a = new DeterministicRandomSource(12345);
            var b = new DeterministicRandomSource(12345);

            for (int i = 0; i < 100; i++)
            {
                Assert.That(b.NextFloat(), Is.EqualTo(a.NextFloat()),
                    "Replaying an encounter must produce the same criticals, or its statistics cannot be compared.");
            }
        }

        [Test]
        public void DifferentSeeds_ProduceDifferentSequences()
        {
            var a = new DeterministicRandomSource(1);
            var b = new DeterministicRandomSource(2);

            bool anyDifference = false;
            for (int i = 0; i < 20 && !anyDifference; i++)
            {
                anyDifference = Math.Abs(a.NextFloat() - b.NextFloat()) > float.Epsilon;
            }

            Assert.That(anyDifference, Is.True);
        }

        [Test]
        public void SeedZero_StillProducesAUsefulStream()
        {
            var source = new DeterministicRandomSource(0);

            bool anyNonZero = false;
            for (int i = 0; i < 20 && !anyNonZero; i++)
            {
                anyNonZero = source.NextFloat() > 0f;
            }

            Assert.That(anyNonZero, Is.True,
                "An all-zero state is a fixed point for xorshift; seed 0 must not fall into it.");
        }

        [Test]
        public void Reset_ReturnsTheStreamToItsStart()
        {
            var source = new DeterministicRandomSource(7);
            float first = source.NextFloat();
            source.NextFloat();
            source.NextFloat();

            source.Reset();

            Assert.That(source.NextFloat(), Is.EqualTo(first));
            Assert.That(source.DrawCount, Is.EqualTo(1));
        }

        [Test]
        public void NextFloat_StaysInTheHalfOpenUnitRange()
        {
            var source = new DeterministicRandomSource(99);

            for (int i = 0; i < 10000; i++)
            {
                float value = source.NextFloat();
                Assert.That(value, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f));
            }
        }

        [Test]
        public void Chance_IsCertainAtTheExtremes_WhateverTheStream()
        {
            var source = new DeterministicRandomSource(3);

            for (int i = 0; i < 100; i++)
            {
                Assert.That(source.Chance(0f), Is.False, "A 0% chance must never fire.");
                Assert.That(source.Chance(1f), Is.True, "A 100% chance must always fire.");
            }
        }

        [Test]
        public void Chance_ApproximatesTheRequestedProbability()
        {
            var source = new DeterministicRandomSource(4242);
            const int trials = 20000;
            int hits = 0;

            for (int i = 0; i < trials; i++)
            {
                if (source.Chance(0.25f))
                {
                    hits++;
                }
            }

            // A generous band: this checks the generator is not badly biased, not that it is perfect.
            Assert.That(hits / (float)trials, Is.EqualTo(0.25f).Within(0.02f));
        }

        [Test]
        public void NextInt_StaysInsideTheRequestedRange()
        {
            var source = new DeterministicRandomSource(11);

            for (int i = 0; i < 1000; i++)
            {
                int value = source.NextInt(5, 10);
                Assert.That(value, Is.GreaterThanOrEqualTo(5).And.LessThan(10));
            }
        }

        [Test]
        public void NextInt_WithAnEmptyRange_ReturnsTheMinimum()
        {
            var source = new DeterministicRandomSource(1);

            Assert.That(source.NextInt(3, 3), Is.EqualTo(3));
            Assert.That(source.NextInt(3, 1), Is.EqualTo(3));
        }
    }

    [TestFixture]
    public sealed class CombatTuningTests
    {
        [Test]
        public void ANonPositiveMitigationConstant_IsRejected()
        {
            // Zero would make the mitigation curve divide by the rating alone, so any rating at all
            // would give total immunity.
            Assert.Throws<ArgumentOutOfRangeException>(() => new CombatTuning(0f, 100f, 0.75f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CombatTuning(100f, -1f, 0.75f));
        }

        [Test]
        public void TheMitigationCeiling_CannotReachTotalImmunity()
        {
            var tuning = new CombatTuning(100f, 100f, maximumMitigation: 5f);

            Assert.That(tuning.MaximumMitigation, Is.LessThan(1f),
                "Immunity is an effect, not something gear alone may reach.");
        }

        [Test]
        public void TheMitigationConstant_ScalesWithAttackerLevel()
        {
            var tuning = new CombatTuning(armorConstantPerLevel: 50f, resistanceConstantPerLevel: 70f, maximumMitigation: 0.75f);

            Assert.That(tuning.MitigationConstant(DamageType.Physical, 10), Is.EqualTo(500f));
            Assert.That(tuning.MitigationConstant(DamageType.Magic, 10), Is.EqualTo(700f));
        }

        [Test]
        public void ALevelBelowOne_IsTreatedAsLevelOne()
        {
            var tuning = new CombatTuning(50f, 50f, 0.75f);

            Assert.That(tuning.MitigationConstant(DamageType.Physical, 0), Is.EqualTo(50f));
            Assert.That(tuning.MitigationConstant(DamageType.Physical, -5), Is.EqualTo(50f));
        }
    }

    [TestFixture]
    public sealed class MitigationTests
    {
        private static readonly CombatTuning Tuning = new CombatTuning(100f, 100f, 0.75f);

        [Test]
        public void ZeroRating_MitigatesNothing()
        {
            Assert.That(Mitigation.Fraction(0f, DamageType.Physical, 1, Tuning), Is.Zero);
        }

        [Test]
        public void TrueDamage_IsNeverMitigated()
        {
            Assert.That(Mitigation.Fraction(100000f, DamageType.True, 1, Tuning), Is.Zero);
        }

        [Test]
        public void RatingEqualToTheConstant_RemovesExactlyHalf()
        {
            Assert.That(Mitigation.Fraction(100f, DamageType.Physical, 1, Tuning), Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void NegativeRating_MitigatesNothingRatherThanAmplifying()
        {
            Assert.That(Mitigation.Fraction(-500f, DamageType.Physical, 1, Tuning), Is.Zero);
        }
    }
}
