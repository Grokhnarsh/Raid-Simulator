using System;
using System.Collections.Generic;
using RaidSim.Core.Diagnostics;
using RaidSim.Core.Events;
using RaidSim.Core.Simulation;

namespace RaidSim.Core.GameFlow
{
    /// <summary>
    /// Owns the current <see cref="GameState"/> and the legal transitions between states.
    /// </summary>
    /// <remarks>
    /// <para>The transition table is declared once, here, rather than implied by scattered
    /// assignments. An illegal transition is a logged rejection, not a silently corrupted state.</para>
    /// <para>Pausing is implemented by this machine driving the simulation clock. Because every
    /// duration in the kernel measures against that clock, pause needs no cooperation from any other
    /// system.</para>
    /// </remarks>
    public sealed class GameStateMachine
    {
        private static readonly Dictionary<GameState, GameState[]> AllowedTransitions =
            new Dictionary<GameState, GameState[]>
            {
                [GameState.None] = new[] { GameState.Booting },
                [GameState.Booting] = new[] { GameState.Playing },
                [GameState.Playing] = new[] { GameState.Paused, GameState.EncounterSummary, GameState.Booting },
                [GameState.Paused] = new[] { GameState.Playing, GameState.Booting },
                [GameState.EncounterSummary] = new[] { GameState.Booting, GameState.Playing },
            };

        private readonly IEventBus _eventBus;
        private readonly SimulationClock _clock;
        private readonly ISimLogSink _log;

        public GameStateMachine(IEventBus eventBus, SimulationClock clock, ISimLogSink log = null)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _log = log ?? NullLogSink.Instance;
            Current = GameState.None;
        }

        public GameState Current { get; private set; }

        public bool IsPlaying => Current == GameState.Playing;

        public bool CanTransitionTo(GameState next) =>
            next != Current &&
            AllowedTransitions.TryGetValue(Current, out GameState[] allowed) &&
            Array.IndexOf(allowed, next) >= 0;

        /// <summary>
        /// Attempts to enter <paramref name="next"/>. Returns false and logs when the transition is
        /// not permitted from the current state.
        /// </summary>
        public bool TransitionTo(GameState next)
        {
            if (next == Current)
            {
                return false;
            }

            if (!CanTransitionTo(next))
            {
                _log.Warn(LogChannel.Bootstrap, $"Rejected game-state transition {Current} -> {next}.");
                return false;
            }

            GameState previous = Current;
            Current = next;
            ApplyClockPolicy(next);
            _eventBus.Publish(new GameStateChangedEvent(previous, next));
            _log.Info(LogChannel.Bootstrap, $"Game state {previous} -> {next}.");
            return true;
        }

        /// <summary>Convenience for the pause key: toggles between playing and paused.</summary>
        public bool TogglePause()
        {
            switch (Current)
            {
                case GameState.Playing:
                    return TransitionTo(GameState.Paused);
                case GameState.Paused:
                    return TransitionTo(GameState.Playing);
                default:
                    return false;
            }
        }

        private void ApplyClockPolicy(GameState state)
        {
            // Only the Playing state advances simulation time. Everything else is frozen, which is
            // what makes pause correct for cooldowns, casts, effect durations and boss timers alike.
            _clock.SetPaused(state != GameState.Playing);
        }
    }
}
