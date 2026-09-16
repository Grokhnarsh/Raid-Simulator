using NUnit.Framework;
using RaidSim.Core.Simulation;

namespace RaidSim.Tests.Core
{
    [TestFixture]
    public sealed class SimulationClockTests
    {
        private const float Tolerance = 0.0001f;

        private SimulationClock _clock;

        [SetUp]
        public void SetUp() => _clock = new SimulationClock();

        [Test]
        public void Advance_AccumulatesTime()
        {
            _clock.Advance(0.1f);
            _clock.Advance(0.1f);

            Assert.That(_clock.Now, Is.EqualTo(0.2f).Within(Tolerance));
            Assert.That(_clock.TickCount, Is.EqualTo(2));
        }

        [Test]
        public void Advance_ReturnsTheDeltaThatWasApplied()
        {
            Assert.That(_clock.Advance(0.016f), Is.EqualTo(0.016f).Within(Tolerance));
        }

        [Test]
        public void Advance_ClampsAnOversizedFrame()
        {
            // An editor stall must not expire a whole rotation of cooldowns in one tick.
            float applied = _clock.Advance(5f);

            Assert.That(applied, Is.EqualTo(SimulationClock.MaxDeltaTime).Within(Tolerance));
            Assert.That(_clock.Now, Is.EqualTo(SimulationClock.MaxDeltaTime).Within(Tolerance));
        }

        [Test]
        public void Advance_IgnoresNonPositiveInput()
        {
            Assert.That(_clock.Advance(0f), Is.Zero);
            Assert.That(_clock.Advance(-1f), Is.Zero);
            Assert.That(_clock.TickCount, Is.Zero);
        }

        [Test]
        public void PausedClock_DoesNotAdvance()
        {
            _clock.Advance(0.5f);
            _clock.Pause();

            float applied = _clock.Advance(0.5f);

            Assert.That(applied, Is.Zero);
            Assert.That(_clock.DeltaTime, Is.Zero, "A zero delta is what freezes every duration in the game.");
            Assert.That(_clock.Now, Is.EqualTo(0.25f).Within(Tolerance));
        }

        [Test]
        public void Resume_RestartsTime_WithoutSkippingAhead()
        {
            _clock.Pause();
            _clock.Advance(10f);
            _clock.Resume();

            _clock.Advance(0.1f);

            Assert.That(_clock.Now, Is.EqualTo(0.1f).Within(Tolerance),
                "Time spent paused must not be credited on resume.");
        }

        [Test]
        public void TimeScale_ScalesTheAppliedDelta()
        {
            _clock.TimeScale = 4f;

            float applied = _clock.Advance(0.1f);

            Assert.That(applied, Is.EqualTo(0.4f).Within(Tolerance));
        }

        [Test]
        public void TimeScale_CannotGoNegative()
        {
            _clock.TimeScale = -2f;

            Assert.That(_clock.TimeScale, Is.Zero);
        }

        [Test]
        public void Ticked_FiresWithTheScaledDelta()
        {
            float observed = -1f;
            _clock.TimeScale = 2f;
            _clock.Ticked += delta => observed = delta;

            _clock.Advance(0.1f);

            Assert.That(observed, Is.EqualTo(0.2f).Within(Tolerance));
        }

        [Test]
        public void Ticked_DoesNotFireWhilePaused()
        {
            int calls = 0;
            _clock.Pause();
            _clock.Ticked += _ => calls++;

            _clock.Advance(0.1f);

            Assert.That(calls, Is.Zero);
        }

        [Test]
        public void Reset_ReturnsTheClockToZeroAndUnpauses()
        {
            _clock.Advance(1f);
            _clock.Pause();

            _clock.Reset();

            Assert.That(_clock.Now, Is.Zero);
            Assert.That(_clock.TickCount, Is.Zero);
            Assert.That(_clock.IsPaused, Is.False);
        }

        [Test]
        public void HasReached_IsTolerantOfFloatDrift()
        {
            for (int i = 0; i < 10; i++)
            {
                _clock.Advance(0.1f);
            }

            Assert.That(_clock.HasReached(1f), Is.True,
                "Accumulated drift must not leave a cooldown permanently one tick from ready.");
        }

        [Test]
        public void HasReached_IsFalseForFutureTimestamps()
        {
            _clock.Advance(0.1f);

            Assert.That(_clock.HasReached(5f), Is.False);
        }
    }
}
