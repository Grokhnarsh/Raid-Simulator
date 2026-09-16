using RaidSim.CameraRig.Runtime;
using RaidSim.Characters.Runtime;
using RaidSim.Core.Entities;
using RaidSim.Core.Simulation;
using RaidSim.Core.Targeting;
using RaidSim.Game.Input;
using UnityEngine;

namespace RaidSim.Combat.Targeting
{
    /// <summary>
    /// Turns targeting input into calls on the controlled actor's <see cref="TargetSelection"/>.
    /// </summary>
    /// <remarks>
    /// <para>Three ways to pick a target, all resolving to the same selection object the AI and the
    /// ability system read:</para>
    /// <list type="bullet">
    /// <item><description><b>Click</b> — a ray from the pointer, hit-tested against actor colliders.</description></item>
    /// <item><description><b>Cycle</b> — the next hostile by distance, wrapping around.</description></item>
    /// <item><description><b>Clear</b> — drop the current target.</description></item>
    /// </list>
    /// <para>Clicking uses a physics ray because that is the only way to honour what the player can
    /// actually see; cycling uses the simulation's own <see cref="TargetQuery"/> because it must obey
    /// the same legality rules an ability does. Both end in <c>TargetSelection.Set</c>, so the target
    /// frame and every downstream system see one consistent answer.</para>
    /// <para>The click filter deliberately allows friendly targets: inspecting a group member's
    /// health is a normal thing to do, and healing abilities in Phase 3 will need exactly this
    /// selection.</para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class PlayerTargetingController : MonoBehaviour
    {
        [Header("Click targeting")]
        [Tooltip("Layers a targeting click may hit. Set to the layer actor colliders live on.")]
        [SerializeField]
        private LayerMask _targetableLayers = ~0;

        [Tooltip("Maximum length of the targeting ray, in metres.")]
        [Min(1f)]
        [SerializeField]
        private float _maxPickDistance = 500f;

        private SimulationContext _context;
        private CombatActor _actor;
        private IPlayerInputSource _input = NullPlayerInputSource.Instance;
        private Camera _camera;

        /// <summary>The selection this controller drives, or null when unbound.</summary>
        public TargetSelection Selection => _actor != null ? _actor.Target : null;

        public void Bind(SimulationContext context, CombatActor actor, IPlayerInputSource input, RaidCameraRig cameraRig)
        {
            _context = context;
            _actor = actor;
            _input = input ?? NullPlayerInputSource.Instance;
            _camera = cameraRig != null ? cameraRig.Camera : null;
        }

        public void Unbind()
        {
            _actor = null;
            _input = NullPlayerInputSource.Instance;
        }

        /// <summary>
        /// Processes this frame's targeting input. Called from the encounter loop.
        /// </summary>
        public void ProcessInput()
        {
            if (_actor == null || _context == null || Selection == null)
            {
                return;
            }

            if (_input.ClearTargetPressed)
            {
                Selection.Clear();
            }

            if (_input.CycleTargetPressed)
            {
                CycleToNextHostile();
            }

            if (_input.SelectTargetPressed)
            {
                SelectUnderPointer(_input.PointerPosition);
            }
        }

        /// <summary>Selects the next living hostile by distance, wrapping around.</summary>
        public void CycleToNextHostile()
        {
            ISimEntity next = _context.Targets.SelectNextCycling(
                _actor,
                TargetFilter.AnyHostile,
                Selection.Target);

            if (next != null)
            {
                Selection.Set(next);
            }
        }

        /// <summary>
        /// Selects whatever actor lies under <paramref name="screenPosition"/>. A click on empty
        /// space is ignored rather than clearing the target, which is what players expect from a
        /// mis-click during a fight.
        /// </summary>
        public void SelectUnderPointer(Vector2 screenPosition)
        {
            if (_camera == null)
            {
                return;
            }

            Ray ray = _camera.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, _maxPickDistance, _targetableLayers, QueryTriggerInteraction.Ignore))
            {
                return;
            }

            var actor = hit.collider.GetComponentInParent<CombatActor>();
            if (actor == null || !actor.IsInitialised)
            {
                return;
            }

            // Anything alive may be inspected: enemies to attack, allies to heal.
            if (TargetFilter.AnyHostile.Matches(_actor, actor) ||
                TargetFilter.AnyFriendly.Matches(_actor, actor))
            {
                Selection.Set(actor);
            }
        }
    }
}
