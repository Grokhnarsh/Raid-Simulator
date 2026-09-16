using NUnit.Framework;
using RaidSim.Core.Events;
using RaidSim.Core.GameFlow;
using RaidSim.Core.Simulation;

namespace RaidSim.Tests.Core
{
    [TestFixture]
    public sealed class GameStateMachineTests
    {
        private EventBus _bus;
        private SimulationClock _clock;
        private GameStateMachine _machine;

        [SetUp]
        public void SetUp()
        {
            _bus = new EventBus();
            _clock = new SimulationClock();
            _machine = new GameStateMachine(_bus, _clock);
        }

        private void EnterPlaying()
        {
            _machine.TransitionTo(GameState.Booting);
            _machine.TransitionTo(GameState.Playing);
        }

        [Test]
        public void StartsInNone()
        {
            Assert.That(_machine.Current, Is.EqualTo(GameState.None));
        }

        [Test]
        public void LegalTransition_IsApplied()
        {
            Assert.That(_machine.TransitionTo(GameState.Booting), Is.True);
            Assert.That(_machine.Current, Is.EqualTo(GameState.Booting));
        }

        [Test]
        public void IllegalTransition_IsRejectedAndLeavesTheStateAlone()
        {
            Assert.That(_machine.TransitionTo(GameState.Playing), Is.False,
                "Play must not be reachable without booting first.");
            Assert.That(_machine.Current, Is.EqualTo(GameState.None));
        }

        [Test]
        public void TransitionToTheCurrentState_IsANoOp()
        {
            _machine.TransitionTo(GameState.Booting);

            Assert.That(_machine.TransitionTo(GameState.Booting), Is.False);
        }

        [Test]
        public void TransitionPublishesTheChange()
        {
            GameStateChangedEvent? received = null;
            _bus.Subscribe<GameStateChangedEvent>(evt => received = evt);

            _machine.TransitionTo(GameState.Booting);

            Assert.That(received.HasValue, Is.True);
            Assert.That(received.Value.Previous, Is.EqualTo(GameState.None));
            Assert.That(received.Value.Current, Is.EqualTo(GameState.Booting));
        }

        [Test]
        public void OnlyThePlayingState_LetsTheClockRun()
        {
            _machine.TransitionTo(GameState.Booting);
            Assert.That(_clock.IsPaused, Is.True);

            _machine.TransitionTo(GameState.Playing);
            Assert.That(_clock.IsPaused, Is.False);
        }

        [Test]
        public void EnteringPaused_FreezesTheSimulationClock()
        {
            EnterPlaying();

            _machine.TransitionTo(GameState.Paused);

            Assert.That(_clock.IsPaused, Is.True);
            Assert.That(_clock.Advance(1f), Is.Zero);
        }

        [Test]
        public void TogglePause_MovesBetweenPlayingAndPaused()
        {
            EnterPlaying();

            Assert.That(_machine.TogglePause(), Is.True);
            Assert.That(_machine.Current, Is.EqualTo(GameState.Paused));

            Assert.That(_machine.TogglePause(), Is.True);
            Assert.That(_machine.Current, Is.EqualTo(GameState.Playing));
        }

        [Test]
        public void TogglePause_DoesNothingOutsideTheEncounter()
        {
            _machine.TransitionTo(GameState.Booting);

            Assert.That(_machine.TogglePause(), Is.False);
            Assert.That(_machine.Current, Is.EqualTo(GameState.Booting));
        }

        [Test]
        public void EncounterSummary_IsReachableFromPlayAndLeadsBackToBoot()
        {
            EnterPlaying();

            Assert.That(_machine.TransitionTo(GameState.EncounterSummary), Is.True);
            Assert.That(_machine.TransitionTo(GameState.Booting), Is.True,
                "Resetting an encounter re-enters bootstrap.");
        }
    }
}
