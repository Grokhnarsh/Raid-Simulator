using System;
using RaidSim.Core.Mathematics;

namespace RaidSim.Core.Simulation
{
    /// <summary>
    /// Default clock. Driven by whoever owns the tick loop: Unity's <c>Update</c> in play mode, a
    /// <c>while</c> loop in the headless simulator.
    /// </summary>
    /// <remarks>
    /// <para><b>Pause</b> is a property of the clock, not a flag each system checks. A paused clock
    /// reports a zero delta and stops advancing <see cref="Now"/>, so every duration in the game
    /// freezes without a single <c>if (paused)</c> in gameplay code.</para>
    /// <para><b>Time scale</b> lets the simulator run an encounter faster than real time, and lets
    /// debug tooling slow a mechanic down to inspect it.</para>
    /// <para><b>The delta clamp</b> keeps one long editor stall from teleporting entities across the
    /// arena or expiring a whole rotation of cooldowns in a single tick.</para>
    /// </remarks>
    public sealed class SimulationClock : ISimulationClock
    {
        /// <summary>
        /// Upper bound on a single tick, in seconds. A stall longer than this is spread over the
        /// following ticks instead of being applied at once.
        /// </summary>
        public const float MaxDeltaTime = 0.25f;

        private float _timeScale = 1f;

        public float Now { get; private set; }

        public float DeltaTime { get; private set; }

        public long TickCount { get; private set; }

        public bool IsPaused { get; private set; }

        /// <summary>Multiplier on incoming real time. Clamped to zero or above.</summary>
        public float TimeScale
        {
            get => _timeScale;
            set => _timeScale = Math.Max(0f, value);
        }

        /// <summary>Raised after each advancing tick with the scaled delta.</summary>
        public event Action<float> Ticked;

        /// <summary>
        /// Advances the clock by <paramref name="unscaledDeltaSeconds"/> of real time.
        /// </summary>
        /// <returns>The scaled, clamped delta that was applied. Zero while paused.</returns>
        public float Advance(float unscaledDeltaSeconds)
        {
            if (IsPaused || unscaledDeltaSeconds <= 0f)
            {
                DeltaTime = 0f;
                return 0f;
            }

            float delta = Math.Min(unscaledDeltaSeconds, MaxDeltaTime) * _timeScale;
            DeltaTime = delta;
            Now += delta;
            TickCount++;
            Ticked?.Invoke(delta);
            return delta;
        }

        public void Pause()
        {
            IsPaused = true;
            DeltaTime = 0f;
        }

        public void Resume() => IsPaused = false;

        public void SetPaused(bool paused)
        {
            if (paused)
            {
                Pause();
            }
            else
            {
                Resume();
            }
        }

        /// <summary>Returns the clock to zero. Called when an encounter is reset.</summary>
        public void Reset()
        {
            Now = 0f;
            DeltaTime = 0f;
            TickCount = 0;
            IsPaused = false;
        }

        /// <summary>
        /// Whether <paramref name="timestamp"/> is at or before now, tolerant of float drift. The
        /// standard way to ask "has this cooldown, cast or effect finished?".
        /// </summary>
        public bool HasReached(float timestamp) => Now >= timestamp - SimMath.Epsilon;
    }
}
