using RaidSim.Core.Mathematics;

namespace RaidSim.Core.Locomotion
{
    /// <summary>How a mover wants to be moved this tick.</summary>
    public enum MovementMode
    {
        /// <summary>Stand still.</summary>
        Idle = 0,

        /// <summary>Move along <see cref="MovementIntent.Direction"/> at full speed.</summary>
        Directional = 1,

        /// <summary>Move toward <see cref="MovementIntent.Destination"/> and stop on arrival.</summary>
        Destination = 2,
    }

    /// <summary>
    /// A request to move, produced by whoever is driving an entity this tick.
    /// </summary>
    /// <remarks>
    /// <para>Player input and AI both produce this same struct, and the locomotion layer is the only
    /// thing that turns it into motion. That single boundary is why a raid member can be swapped
    /// between "player controlled" and "AI controlled" at runtime without either side knowing, and
    /// why the headless simulator can run the same movement code with no input device present.</para>
    /// <para><see cref="FaceDirection"/> is separate from the direction of travel: strafing away from
    /// a danger zone while still facing the boss is a normal and important thing to do.</para>
    /// </remarks>
    public readonly struct MovementIntent
    {
        public readonly MovementMode Mode;

        /// <summary>Desired direction of travel. Normalised horizontally; ignored unless the mode is
        /// <see cref="MovementMode.Directional"/>.</summary>
        public readonly Vec3 Direction;

        /// <summary>Target position; ignored unless the mode is <see cref="MovementMode.Destination"/>.</summary>
        public readonly Vec3 Destination;

        /// <summary>Distance from the destination at which the mover stops, in metres.</summary>
        public readonly float StoppingDistance;

        /// <summary>Direction to face. <see cref="Vec3.Zero"/> means "keep facing where you travel".</summary>
        public readonly Vec3 FaceDirection;

        /// <summary>Fraction of movement speed to use, 0..1. Lets AI approach cautiously.</summary>
        public readonly float SpeedScale;

        private MovementIntent(
            MovementMode mode,
            Vec3 direction,
            Vec3 destination,
            float stoppingDistance,
            Vec3 faceDirection,
            float speedScale)
        {
            Mode = mode;
            Direction = direction;
            Destination = destination;
            StoppingDistance = stoppingDistance;
            FaceDirection = faceDirection;
            SpeedScale = SimMath.Clamp01(speedScale);
        }

        public static MovementIntent Idle => new MovementIntent(
            MovementMode.Idle, Vec3.Zero, Vec3.Zero, 0f, Vec3.Zero, 0f);

        /// <summary>Stand still but turn to face <paramref name="faceDirection"/>.</summary>
        public static MovementIntent FaceOnly(Vec3 faceDirection) => new MovementIntent(
            MovementMode.Idle, Vec3.Zero, Vec3.Zero, 0f, faceDirection.Flat.Normalized, 0f);

        public static MovementIntent Move(Vec3 direction, float speedScale = 1f) => new MovementIntent(
            MovementMode.Directional, direction.Flat.Normalized, Vec3.Zero, 0f, Vec3.Zero, speedScale);

        /// <summary>Move along <paramref name="direction"/> while facing somewhere else.</summary>
        public static MovementIntent MoveFacing(Vec3 direction, Vec3 faceDirection, float speedScale = 1f) =>
            new MovementIntent(
                MovementMode.Directional,
                direction.Flat.Normalized,
                Vec3.Zero,
                0f,
                faceDirection.Flat.Normalized,
                speedScale);

        public static MovementIntent MoveTo(Vec3 destination, float stoppingDistance = 0f, float speedScale = 1f) =>
            new MovementIntent(
                MovementMode.Destination,
                Vec3.Zero,
                destination,
                stoppingDistance < 0f ? 0f : stoppingDistance,
                Vec3.Zero,
                speedScale);

        /// <summary>Whether this intent asks for any translation at all.</summary>
        public bool WantsToMove => Mode != MovementMode.Idle && SpeedScale > 0f;
    }
}
