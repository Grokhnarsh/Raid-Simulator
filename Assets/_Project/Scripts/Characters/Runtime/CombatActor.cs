using RaidSim.Characters.Data;
using RaidSim.Core.Common;
using RaidSim.Core.Diagnostics;
using RaidSim.Core.Entities;
using RaidSim.Core.Locomotion;
using RaidSim.Core.Mathematics;
using RaidSim.Core.Simulation;
using RaidSim.Core.Stats;
using RaidSim.Core.Targeting;
using RaidSim.Core.Vitals;
using RaidSim.Game.Interop;
using UnityEngine;

namespace RaidSim.Characters.Runtime
{
    /// <summary>
    /// The Unity-side body of a combat participant: the bridge between a GameObject and the
    /// simulation kernel's <see cref="ICombatEntity"/>.
    /// </summary>
    /// <remarks>
    /// <para>There is exactly one of these types for every fighting thing in the game. The player's
    /// character, an AI group member, a trash mob and the boss are all this component with different
    /// data and a different driver. Nothing downstream — targeting, damage, threat, the combat log —
    /// can tell them apart, which is what makes "let the AI take over the player's character" and
    /// "run the encounter with no player at all" work without a second code path.</para>
    /// <para>The component owns no rules. It builds runtime state from authored data, registers with
    /// the entity registry, and exposes a <see cref="ILocomotor"/>. Deciding what to do is the job of
    /// a driver: player input in Phase 1, an AI profile from Phase 5.</para>
    /// <para>It has no <c>Update</c>. Movement is applied from the encounter tick loop so that the
    /// order of operations stays explicit and a forty-player raid does not cost forty
    /// <c>Update</c> callbacks.</para>
    /// </remarks>
    [RequireComponent(typeof(CharacterController))]
    [DisallowMultipleComponent]
    public sealed class CombatActor : MonoBehaviour, ICombatEntity
    {
        [Header("Data")]
        [Tooltip("Authored character this actor is built from. Assigned by bootstrap when spawned from a roster.")]
        [SerializeField]
        private CharacterDefinition _definition;

        [Tooltip("Which side this actor fights for.")]
        [SerializeField]
        private Faction _faction = Faction.Raid;

        [Header("Movement")]
        [Tooltip("Constant downward push, in metres per second, that keeps the controller on the ground.")]
        [Min(0f)]
        [SerializeField]
        private float _groundingForce = 9.81f;

        private SimulationContext _context;
        private CharacterControllerLocomotor _locomotor;
        private bool _isRegistered;

        /// <inheritdoc />
        public EntityId Id { get; private set; } = EntityId.None;

        /// <inheritdoc />
        public string DisplayName { get; private set; } = string.Empty;

        /// <inheritdoc />
        public Faction Faction => _faction;

        /// <inheritdoc />
        public Vec3 Position => transform.position.ToSim();

        /// <inheritdoc />
        public Vec3 Facing => transform.forward.ToSim();

        /// <inheritdoc />
        public float Radius { get; private set; } = 0.5f;

        /// <inheritdoc />
        public bool IsAlive => Health != null && Health.IsAlive;

        /// <inheritdoc />
        public StatBlock Stats { get; private set; }

        /// <inheritdoc />
        public Health Health { get; private set; }

        /// <inheritdoc />
        public ResourcePool Resource { get; private set; }

        /// <inheritdoc />
        public CombatRole Role { get; private set; } = CombatRole.None;

        /// <inheritdoc />
        public int Level { get; private set; } = 1;

        /// <summary>The authored data this actor was built from.</summary>
        public CharacterDefinition Definition => _definition;

        /// <summary>Movement interface. Drivers push a <see cref="MovementIntent"/> through this.</summary>
        public ILocomotor Locomotor => _locomotor;

        /// <summary>This actor's current target. Never null once initialised.</summary>
        public TargetSelection Target { get; private set; }

        /// <summary>Distance in metres at which this actor prefers to fight.</summary>
        public float PreferredCombatRange { get; private set; }

        public bool IsInitialised => _context != null;

        /// <summary>
        /// Builds runtime state from authored data and joins the simulation.
        /// </summary>
        /// <remarks>
        /// Called by bootstrap rather than from <c>Awake</c>, because an actor is only meaningful
        /// once there is a context to join, and because the spawner decides the faction and the data.
        /// </remarks>
        public void Initialise(SimulationContext context, CharacterDefinition definition, Faction faction)
        {
            if (IsInitialised)
            {
                context?.Log.Warn(LogChannel.Bootstrap,
                    $"CombatActor on '{name}' was initialised twice; ignoring the second call.");
                return;
            }

            _context = context != null ? context : throw new System.ArgumentNullException(nameof(context));
            _definition = definition != null ? definition : _definition;
            _faction = faction;

            string problem = _definition == null ? $"'{name}' has no character definition." : _definition.Validate();
            if (problem != null)
            {
                _context.Log.Error(LogChannel.Bootstrap, problem);
                enabled = false;
                return;
            }

            ClassDefinition classDefinition = _definition.Class;
            Id = EntityId.Next();
            DisplayName = _definition.DisplayName;
            Level = _definition.Level;
            Role = classDefinition.Role;
            Radius = classDefinition.Radius;
            PreferredCombatRange = classDefinition.PreferredCombatRange;

            Stats = new StatBlock(_definition.BuildBaseStats());
            Health = new Health(Stats);
            Resource = new ResourcePool(Stats, classDefinition.ResourceKind);

            var controller = GetComponent<CharacterController>();
            controller.radius = Radius;
            _locomotor = new CharacterControllerLocomotor(
                controller,
                () => Stats.Get(StatType.MovementSpeed),
                classDefinition.TurnRateDegrees,
                _groundingForce);

            Target = new TargetSelection(Id, _context.Entities, _context.Events);
            _isRegistered = _context.Entities.Register(this);
        }

        /// <summary>
        /// Applies a movement intent for this tick. Called by the encounter loop after the actor's
        /// driver has decided what to do.
        /// </summary>
        public void ApplyMovement(in MovementIntent intent, float deltaSeconds)
        {
            if (_locomotor == null || !IsAlive)
            {
                return;
            }

            _locomotor.Move(intent, deltaSeconds);
        }

        /// <summary>Places the actor without interpolation. For spawning and encounter resets.</summary>
        public void PlaceAt(Vector3 position, Vector3 facing)
        {
            _locomotor?.Teleport(position.ToSim());
            _locomotor?.SetFacing(facing.ToSim());
        }

        /// <summary>
        /// Returns the actor to full health and its encounter-start resource. Phase 2 will extend
        /// this to clear effects and threat.
        /// </summary>
        public void ResetForEncounter()
        {
            Health?.Fill();
            Resource?.ResetToEncounterStart();
            Target?.Clear();
        }

        private void OnDestroy()
        {
            Target?.Dispose();
            Health?.Dispose();
            Resource?.Dispose();

            if (_isRegistered && _context != null)
            {
                _context.Entities.Unregister(Id);
                _isRegistered = false;
            }
        }
    }
}
