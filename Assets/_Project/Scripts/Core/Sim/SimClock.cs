using System;
using UnityEngine;

namespace EmberDepths.Core.Sim
{
    /// <summary>
    /// Fixed-step heartbeat for everything that is *simulation* rather than
    /// *presentation*: AI decisions, ability timers, damage-over-time, hazard
    /// pulses, threat decay.
    ///
    /// Why not just use Update()? Three reasons, and they are the reasons this
    /// project is called a simulator:
    ///   1. Balance numbers become frame-rate independent and comparable.
    ///   2. Seeded runs replay identically, which makes encounter bugs reproducible.
    ///   3. It is the seam a future networking layer needs — ticks are what you
    ///      send over the wire, frames are not.
    ///
    /// Rendering reads <see cref="Alpha"/> to interpolate between the previous and
    /// current tick, so a 20 Hz simulation still looks smooth at 144 fps.
    /// </summary>
    public sealed class SimClock
    {
        public const int TicksPerSecond = 20;
        public const float TickDuration = 1f / TicksPerSecond;

        /// <summary>Never advance more than this many ticks in one frame.</summary>
        private const int MaxCatchUpTicks = 5;

        private float _accumulator;

        /// <summary>Ticks elapsed since the run started. Monotonic, never reset mid-run.</summary>
        public int Tick { get; private set; }

        /// <summary>Fraction of the way into the next tick, 0..1. For visual interpolation only.</summary>
        public float Alpha { get; private set; }

        /// <summary>Simulated seconds since the run started.</summary>
        public float Time => Tick * TickDuration;

        /// <summary>When false, <see cref="Advance"/> is a no-op (pause menus, cutscenes).</summary>
        public bool Paused { get; set; }

        /// <summary>Debug/replay knob. 0.25 for slow motion, 2 for fast-forward.</summary>
        public float TimeScale { get; set; } = 1f;

        /// <summary>Raised once per fixed step, in order, with the new tick index.</summary>
        public event Action<int> Ticked;

        public void Advance(float unscaledDeltaTime)
        {
            if (Paused)
            {
                Alpha = 0f;
                return;
            }

            _accumulator += unscaledDeltaTime * Mathf.Max(0f, TimeScale);

            int steps = 0;
            while (_accumulator >= TickDuration)
            {
                _accumulator -= TickDuration;
                Tick++;
                Ticked?.Invoke(Tick);

                if (++steps < MaxCatchUpTicks) continue;

                // A long hitch (asset load, editor breakpoint) must not turn into a
                // burst of simulation that teleports every enemy across the room.
                // Drop the backlog instead and carry on from now.
                _accumulator = 0f;
                break;
            }

            Alpha = _accumulator / TickDuration;
        }

        public void Reset()
        {
            Tick = 0;
            Alpha = 0f;
            _accumulator = 0f;
        }

        /// <summary>Converts seconds to whole ticks, rounding up so short timers never become zero.</summary>
        public static int SecondsToTicks(float seconds) =>
            Mathf.Max(seconds > 0f ? 1 : 0, Mathf.CeilToInt(seconds * TicksPerSecond));

        public static float TicksToSeconds(int ticks) => ticks * TickDuration;
    }
}
