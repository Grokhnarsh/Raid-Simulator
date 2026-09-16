using System;
using RaidSim.Core.Mathematics;

namespace RaidSim.Core.Locomotion
{
    /// <summary>
    /// Engine-free locomotor: integrates a <see cref="MovementIntent"/> directly, with no collision.
    /// </summary>
    /// <remarks>
    /// <para>This is the reference implementation of movement. The headless encounter simulator uses
    /// it as-is, and it is what the movement unit tests exercise, so turn rates, stopping distances
    /// and speed scaling are verified without an engine in the loop.</para>
    /// <para>In the running game Unity substitutes a locomotor that respects collision and ground
    /// height; the arithmetic below stays the definition of what the simulation <i>intends</i>.</para>
    /// </remarks>
    public sealed class KinematicLocomotor : ILocomotor
    {
        private readonly Func<float> _speedProvider;

        public KinematicLocomotor(Func<float> speedProvider, float turnRateDegrees, Vec3 startPosition = default)
        {
            _speedProvider = speedProvider ?? throw new ArgumentNullException(nameof(speedProvider));
            TurnRateDegrees = Math.Max(0f, turnRateDegrees);
            Position = startPosition;
            Facing = Vec3.Forward;
        }

        public Vec3 Position { get; private set; }

        public Vec3 Facing { get; private set; }

        public float Speed => Math.Max(0f, _speedProvider());

        public float TurnRateDegrees { get; }

        public bool IsMoving { get; private set; }

        public void Move(in MovementIntent intent, float deltaSeconds)
        {
            IsMoving = false;
            if (deltaSeconds <= 0f)
            {
                return;
            }

            Vec3 travel = ResolveTravel(intent, deltaSeconds, out Vec3 travelDirection);
            if (travel != Vec3.Zero)
            {
                Position += travel;
                IsMoving = true;
            }

            Vec3 desiredFacing = intent.FaceDirection != Vec3.Zero ? intent.FaceDirection : travelDirection;
            if (desiredFacing != Vec3.Zero)
            {
                Facing = TurnToward(Facing, desiredFacing, TurnRateDegrees * deltaSeconds);
            }
        }

        public void Teleport(Vec3 position)
        {
            Position = position;
            IsMoving = false;
        }

        public void SetFacing(Vec3 facing)
        {
            Vec3 flat = facing.Flat.Normalized;
            if (flat != Vec3.Zero)
            {
                Facing = flat;
            }
        }

        private Vec3 ResolveTravel(in MovementIntent intent, float deltaSeconds, out Vec3 travelDirection)
        {
            travelDirection = Vec3.Zero;
            if (!intent.WantsToMove)
            {
                return Vec3.Zero;
            }

            float step = Speed * intent.SpeedScale * deltaSeconds;
            if (step <= 0f)
            {
                return Vec3.Zero;
            }

            switch (intent.Mode)
            {
                case MovementMode.Directional:
                {
                    Vec3 direction = intent.Direction.Flat.Normalized;
                    if (direction == Vec3.Zero)
                    {
                        return Vec3.Zero;
                    }

                    travelDirection = direction;
                    return direction * step;
                }

                case MovementMode.Destination:
                {
                    Vec3 toDestination = (intent.Destination - Position).Flat;
                    float remaining = toDestination.Magnitude - intent.StoppingDistance;
                    if (remaining <= SimMath.Epsilon)
                    {
                        // Already there: still report the direction so the entity keeps facing it.
                        travelDirection = toDestination.Normalized;
                        return Vec3.Zero;
                    }

                    Vec3 direction = toDestination.Normalized;
                    travelDirection = direction;
                    return direction * Math.Min(step, remaining);
                }

                default:
                    return Vec3.Zero;
            }
        }

        /// <summary>
        /// Rotates <paramref name="current"/> toward <paramref name="desired"/> by at most
        /// <paramref name="maxDegrees"/>. A zero or negative turn rate means instant facing.
        /// </summary>
        public static Vec3 TurnToward(Vec3 current, Vec3 desired, float maxDegrees)
        {
            Vec3 from = current.Flat.Normalized;
            Vec3 to = desired.Flat.Normalized;
            if (to == Vec3.Zero)
            {
                return from;
            }

            if (from == Vec3.Zero || maxDegrees <= 0f)
            {
                return to;
            }

            float angle = SimMath.AngleBetween(from, to);
            if (angle <= maxDegrees || angle <= SimMath.Epsilon)
            {
                return to;
            }

            // Rotate about the world up axis by the allowed amount, toward the shorter side.
            float sign = Vec3.Cross(from, to).Y >= 0f ? 1f : -1f;
            float radians = maxDegrees * SimMath.Deg2Rad * sign;
            float cos = (float)Math.Cos(radians);
            float sin = (float)Math.Sin(radians);
            return new Vec3(
                (from.X * cos) + (from.Z * sin),
                0f,
                (-from.X * sin) + (from.Z * cos)).Normalized;
        }
    }
}
