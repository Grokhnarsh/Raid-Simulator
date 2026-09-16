using System;
using RaidSim.Core.Locomotion;
using RaidSim.Core.Mathematics;
using RaidSim.Game.Interop;
using UnityEngine;

namespace RaidSim.Characters.Runtime
{
    /// <summary>
    /// <see cref="ILocomotor"/> backed by Unity's <see cref="CharacterController"/>.
    /// </summary>
    /// <remarks>
    /// <para>Same contract as <see cref="KinematicLocomotor"/>, but the motion goes through Unity so
    /// it collides with arena geometry and follows the ground. Neither player input nor AI knows
    /// which of the two it is driving — that is what lets an encounter be replayed headlessly.</para>
    /// <para>Gravity is applied as a constant downward push rather than integrated acceleration. The
    /// game is a ground-based raid simulator: characters walk up ramps and down steps, they do not
    /// fall for long enough for real ballistics to matter.</para>
    /// </remarks>
    public sealed class CharacterControllerLocomotor : ILocomotor
    {
        private readonly CharacterController _controller;
        private readonly Transform _transform;
        private readonly Func<float> _speedProvider;
        private readonly float _groundingForce;

        public CharacterControllerLocomotor(
            CharacterController controller,
            Func<float> speedProvider,
            float turnRateDegrees,
            float groundingForce)
        {
            _controller = controller != null ? controller : throw new ArgumentNullException(nameof(controller));
            _transform = controller.transform;
            _speedProvider = speedProvider ?? throw new ArgumentNullException(nameof(speedProvider));
            TurnRateDegrees = Mathf.Max(0f, turnRateDegrees);
            _groundingForce = Mathf.Max(0f, groundingForce);
        }

        public Vec3 Position => _transform.position.ToSim();

        public Vec3 Facing => _transform.forward.ToSim();

        public float Speed => Mathf.Max(0f, _speedProvider());

        public float TurnRateDegrees { get; }

        public bool IsMoving { get; private set; }

        public void Move(in MovementIntent intent, float deltaSeconds)
        {
            IsMoving = false;
            if (deltaSeconds <= 0f)
            {
                return;
            }

            Vec3 travelDirection = ResolveTravelDirection(intent, out float maxDistance);
            Vector3 motion = Vector3.zero;

            if (travelDirection != Vec3.Zero && maxDistance > 0f)
            {
                float step = Mathf.Min(Speed * intent.SpeedScale * deltaSeconds, maxDistance);
                if (step > 0f)
                {
                    motion = travelDirection.ToUnity() * step;
                    IsMoving = true;
                }
            }

            // Keep the controller pinned to the ground so it walks down steps and ramps instead of
            // stepping off them.
            motion.y -= _groundingForce * deltaSeconds;
            _controller.Move(motion);

            ApplyFacing(intent, travelDirection, deltaSeconds);
        }

        public void Teleport(Vec3 position)
        {
            // The controller must be disabled or it will overwrite the transform on the same frame.
            bool wasEnabled = _controller.enabled;
            _controller.enabled = false;
            _transform.position = position.ToUnity();
            _controller.enabled = wasEnabled;
            IsMoving = false;
        }

        public void SetFacing(Vec3 facing)
        {
            Vector3 flat = facing.Flat.ToUnity();
            if (flat.sqrMagnitude > Mathf.Epsilon)
            {
                _transform.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);
            }
        }

        private Vec3 ResolveTravelDirection(in MovementIntent intent, out float maxDistance)
        {
            maxDistance = float.MaxValue;
            if (!intent.WantsToMove)
            {
                return Vec3.Zero;
            }

            switch (intent.Mode)
            {
                case MovementMode.Directional:
                    return intent.Direction.Flat.Normalized;

                case MovementMode.Destination:
                {
                    Vec3 toDestination = (intent.Destination - Position).Flat;
                    maxDistance = toDestination.Magnitude - intent.StoppingDistance;
                    return maxDistance <= SimMath.Epsilon ? Vec3.Zero : toDestination.Normalized;
                }

                default:
                    return Vec3.Zero;
            }
        }

        private void ApplyFacing(in MovementIntent intent, Vec3 travelDirection, float deltaSeconds)
        {
            Vec3 desired = intent.FaceDirection != Vec3.Zero ? intent.FaceDirection : travelDirection;
            if (desired == Vec3.Zero)
            {
                return;
            }

            Vector3 target = desired.Flat.ToUnity();
            if (target.sqrMagnitude <= Mathf.Epsilon)
            {
                return;
            }

            Quaternion desiredRotation = Quaternion.LookRotation(target.normalized, Vector3.up);
            _transform.rotation = TurnRateDegrees <= 0f
                ? desiredRotation
                : Quaternion.RotateTowards(_transform.rotation, desiredRotation, TurnRateDegrees * deltaSeconds);
        }
    }
}
