using RaidSim.CameraRig.Runtime;
using RaidSim.Characters.Runtime;
using RaidSim.Core.Locomotion;
using RaidSim.Core.Mathematics;
using RaidSim.Game.Input;
using RaidSim.Game.Interop;
using UnityEngine;

namespace RaidSim.Game.Player
{
    /// <summary>
    /// Turns player input into a <see cref="MovementIntent"/> for the character being controlled.
    /// </summary>
    /// <remarks>
    /// <para>This is a <i>driver</i>, and drivers are interchangeable. It produces exactly the same
    /// struct an AI profile will produce in Phase 5, so control of a group member can change hands
    /// at runtime by pointing this at a different actor, and an encounter can run with every member
    /// AI-driven and no player at all.</para>
    /// <para><b>Camera-relative movement.</b> On a fixed isometric angle, pressing "up" must move the
    /// character up the screen, not along world +Z. Input is therefore projected onto the camera's
    /// flattened basis. This is also why the controller holds a camera reference rather than the
    /// camera holding a player reference.</para>
    /// <para>The intent is produced here but applied by the encounter loop, so movement lands in the
    /// tick order declared in <c>SystemOrder</c> rather than whenever a MonoBehaviour happened to
    /// update.</para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class PlayerController : MonoBehaviour
    {
        [Tooltip("Dead zone below which movement input is treated as zero.")]
        [Range(0f, 0.5f)]
        [SerializeField]
        private float _moveDeadZone = 0.15f;

        private IPlayerInputSource _input = NullPlayerInputSource.Instance;
        private CombatActor _actor;
        private Transform _cameraTransform;

        /// <summary>The actor currently being driven, or null.</summary>
        public CombatActor ControlledActor => _actor;

        /// <summary>The intent produced by the most recent call to <see cref="ReadIntent"/>.</summary>
        public MovementIntent CurrentIntent { get; private set; } = MovementIntent.Idle;

        /// <summary>
        /// Points the controller at an actor, an input source and the camera that defines
        /// "forward" for the player.
        /// </summary>
        public void Bind(CombatActor actor, IPlayerInputSource input, RaidCameraRig cameraRig)
        {
            _actor = actor;
            _input = input ?? NullPlayerInputSource.Instance;
            _cameraTransform = cameraRig != null ? cameraRig.transform : null;
        }

        /// <summary>Stops driving whatever it was driving. The actor stands still.</summary>
        public void Unbind()
        {
            _actor = null;
            _input = NullPlayerInputSource.Instance;
            CurrentIntent = MovementIntent.Idle;
        }

        /// <summary>
        /// Reads input and produces this tick's movement intent. Called by the encounter loop.
        /// </summary>
        public MovementIntent ReadIntent()
        {
            if (_actor == null || !_actor.IsAlive)
            {
                CurrentIntent = MovementIntent.Idle;
                return CurrentIntent;
            }

            Vector2 axis = _input.MoveAxis;
            if (axis.sqrMagnitude < _moveDeadZone * _moveDeadZone)
            {
                CurrentIntent = MovementIntent.Idle;
                return CurrentIntent;
            }

            Vec3 direction = ToWorldDirection(axis);
            CurrentIntent = direction == Vec3.Zero
                ? MovementIntent.Idle
                : MovementIntent.Move(direction, Mathf.Clamp01(axis.magnitude));

            return CurrentIntent;
        }

        /// <summary>
        /// Projects a screen-space input axis onto the ground plane using the camera's orientation,
        /// so "forward" always means "away from the viewer".
        /// </summary>
        public Vec3 ToWorldDirection(Vector2 axis)
        {
            if (_cameraTransform == null)
            {
                return new Vec3(axis.x, 0f, axis.y).Normalized;
            }

            Vector3 forward = Vector3.ProjectOnPlane(_cameraTransform.forward, Vector3.up);
            Vector3 right = Vector3.ProjectOnPlane(_cameraTransform.right, Vector3.up);

            if (forward.sqrMagnitude < Mathf.Epsilon)
            {
                // Looking straight down: fall back to the camera's up vector for "away from viewer".
                forward = Vector3.ProjectOnPlane(_cameraTransform.up, Vector3.up);
            }

            Vector3 world = (forward.normalized * axis.y) + (right.normalized * axis.x);
            return world.ToSim().Normalized;
        }
    }
}
