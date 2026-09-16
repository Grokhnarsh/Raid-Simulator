using RaidSim.Core.Mathematics;

namespace RaidSim.Core.Locomotion
{
    /// <summary>
    /// Applies a <see cref="MovementIntent"/> to an entity.
    /// </summary>
    /// <remarks>
    /// <para>Implementations decide <i>how</i> movement happens. Unity supplies one backed by
    /// <c>CharacterController</c> (and later one backed by a navigation mesh for AI that must path
    /// around pillars); the headless simulator supplies a kinematic one that simply integrates the
    /// intent. Neither the AI nor the player controller can tell the difference.</para>
    /// <para>This is the seam that keeps the encounter simulator honest: statistics produced without
    /// a renderer must come from the same movement rules the player sees.</para>
    /// </remarks>
    public interface ILocomotor
    {
        Vec3 Position { get; }

        /// <summary>Normalised horizontal facing.</summary>
        Vec3 Facing { get; }

        /// <summary>Metres per second at full speed. Sourced from the entity's movement-speed stat.</summary>
        float Speed { get; }

        /// <summary>Degrees per second the entity can turn.</summary>
        float TurnRateDegrees { get; }

        /// <summary>Whether the entity actually moved during the last tick. Drives animation and
        /// casting rules that break on movement.</summary>
        bool IsMoving { get; }

        /// <summary>Applies <paramref name="intent"/> for <paramref name="deltaSeconds"/>.</summary>
        void Move(in MovementIntent intent, float deltaSeconds);

        /// <summary>Places the entity immediately. For spawning, resets and knock-back mechanics.</summary>
        void Teleport(Vec3 position);

        /// <summary>Sets facing immediately, without turning over time.</summary>
        void SetFacing(Vec3 facing);
    }
}
