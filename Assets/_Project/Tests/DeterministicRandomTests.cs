using System.Collections.Generic;
using EmberDepths.Core.Sim;
using NUnit.Framework;

namespace EmberDepths.Tests
{
    public sealed class DeterministicRandomTests
    {
        [Test]
        public void SameSeedProducesTheSameSequence()
        {
            var a = new DeterministicRandom(9001);
            var b = new DeterministicRandom(9001);

            for (int i = 0; i < 500; i++)
                Assert.AreEqual(a.NextUInt(), b.NextUInt(), $"diverged at draw {i}");
        }

        [Test]
        public void AdjacentSeedsDiverge()
        {
            // Seeds often come from a loop counter or a tick count, so
            // neighbouring seeds must not produce near-identical dungeons.
            var a = new DeterministicRandom(1000);
            var b = new DeterministicRandom(1001);

            int identical = 0;
            for (int i = 0; i < 200; i++)
                if (a.NextUInt() == b.NextUInt()) identical++;

            Assert.Less(identical, 3, "adjacent seeds produced suspiciously similar streams");
        }

        [Test]
        public void ForkedStreamsAreIndependentAndReproducible()
        {
            // Re-rolling loot must not be able to shift a layout already
            // generated from the same seed.
            var layoutA = new DeterministicRandom(77).Fork("layout");
            var layoutB = new DeterministicRandom(77).Fork("layout");
            var loot = new DeterministicRandom(77).Fork("loot");

            var fromA = new List<uint>();
            for (int i = 0; i < 100; i++) fromA.Add(layoutA.NextUInt());

            // Draw from an unrelated stream in between; it must change nothing.
            for (int i = 0; i < 50; i++) loot.NextUInt();

            for (int i = 0; i < 100; i++)
                Assert.AreEqual(fromA[i], layoutB.NextUInt(), $"fork diverged at draw {i}");
        }

        [Test]
        public void RangeStaysInBounds()
        {
            var rng = new DeterministicRandom(5);

            for (int i = 0; i < 10000; i++)
            {
                int v = rng.Range(3, 9);
                Assert.GreaterOrEqual(v, 3);
                Assert.Less(v, 9);
            }
        }

        [Test]
        public void ValueStaysInUnitInterval()
        {
            var rng = new DeterministicRandom(11);

            for (int i = 0; i < 10000; i++)
            {
                float v = rng.Value;
                Assert.GreaterOrEqual(v, 0f);
                Assert.Less(v, 1f);
            }
        }

        [Test]
        public void PickWeighted_ReturnsMinusOneWhenNothingIsPickable()
        {
            var rng = new DeterministicRandom(3);

            // Callers rely on this rather than silently getting index 0, which
            // would make an all-zero loot table drop its first entry every time.
            Assert.AreEqual(-1, rng.PickWeighted(new float[] { 0f, 0f, 0f }));
            Assert.AreEqual(-1, rng.PickWeighted(new float[0]));
        }

        [Test]
        public void PickWeighted_NeverReturnsAZeroWeightedIndex()
        {
            var rng = new DeterministicRandom(13);
            var weights = new float[] { 0f, 5f, 0f, 1f };

            for (int i = 0; i < 2000; i++)
            {
                int index = rng.PickWeighted(weights);
                Assert.IsTrue(index == 1 || index == 3, $"picked zero-weighted index {index}");
            }
        }

        [Test]
        public void SecondsToTicks_NeverRoundsAShortTimerToZero()
        {
            // A 0.01s stun that rounds to zero ticks simply never applies.
            Assert.AreEqual(1, SimClock.SecondsToTicks(0.001f));
            Assert.AreEqual(0, SimClock.SecondsToTicks(0f));
            Assert.AreEqual(SimClock.TicksPerSecond, SimClock.SecondsToTicks(1f));
        }
    }
}
