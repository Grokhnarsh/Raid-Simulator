using RaidSim.CameraRig.Runtime;
using RaidSim.Characters.Runtime;
using RaidSim.Combat.Targeting;
using RaidSim.Core.Diagnostics;
using RaidSim.Core.GameFlow;
using RaidSim.Core.Simulation;
using RaidSim.Game.Input;
using RaidSim.Game.Interop;
using RaidSim.Game.Player;
using UnityEngine;

namespace RaidSim.Game.Bootstrap
{
    /// <summary>
    /// Composition root. Builds the simulation, assembles the scene, and drives the tick loop.
    /// </summary>
    /// <remarks>
    /// <para>This is the one place in the project allowed to know about many systems at once, and it
    /// is allowed precisely because it contains no rules — it constructs, wires and ticks, then gets
    /// out of the way. Everything it builds communicates through interfaces and events, never back
    /// through this class. That is the difference between a composition root and the
    /// <c>GameManager</c> the design forbids.</para>
    /// <para>The bootstrap scene holds only this component. Camera, floor, player and practice
    /// targets are all created from <see cref="BootstrapConfig"/> at load, which keeps the scene file
    /// small enough to merge and makes a different starting setup a different asset rather than a
    /// different scene.</para>
    /// <para><b>Pause and zoom are handled outside the simulation tick</b>, on unscaled time. Both
    /// have to keep working while the simulation clock is frozen — a pause you cannot leave, or a
    /// camera you cannot adjust while studying a frozen mechanic, would be a bug.</para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class GameBootstrap : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("Describes everything to build at load. Required.")]
        [SerializeField]
        private BootstrapConfig _config;

        [Header("Diagnostics")]
        [Tooltip("Which log channels reach the Unity console.")]
        [SerializeField]
        private LogChannel _logChannels = LogChannel.All;

        [Tooltip("Lowest severity that reaches the Unity console.")]
        [SerializeField]
        private LogSeverity _minimumLogSeverity = LogSeverity.Info;

        private SimulationContext _context;
        private RaidCameraRig _cameraRig;
        private PlayerController _playerController;
        private PlayerTargetingController _targetingController;
        private InputSystemPlayerInput _input;
        private GameObject _playerRig;
        private Transform _sceneRoot;

        /// <summary>The running simulation, or null before bootstrap completes.</summary>
        public SimulationContext Context => _context;

        /// <summary>The camera rig built during bootstrap.</summary>
        public RaidCameraRig CameraRig => _cameraRig;

        /// <summary>The actor the player is currently driving, or null.</summary>
        public CombatActor PlayerActor => _playerController != null ? _playerController.ControlledActor : null;

        private void Awake()
        {
            if (_config == null)
            {
                Debug.LogError($"{nameof(GameBootstrap)} on '{name}' has no config assigned; nothing will be built.", this);
                enabled = false;
                return;
            }

            string problem = _config.Validate();
            if (problem != null)
            {
                Debug.LogError($"Bootstrap configuration is incomplete: {problem}", this);
                enabled = false;
                return;
            }

            _context = new SimulationContext(new UnityLogSink(_logChannels, _minimumLogSeverity));
            _context.State.TransitionTo(GameState.Booting);

            _sceneRoot = new GameObject("~Encounter").transform;
            _sceneRoot.SetParent(transform, worldPositionStays: false);

            BuildArena();
            BuildCamera();
            BuildPlayerRig();
            SpawnRosterAndBind();

            if (_config.AutoStart)
            {
                _context.State.TransitionTo(GameState.Playing);
            }

            _context.Log.Info(LogChannel.Bootstrap,
                $"Bootstrap complete: {_context.Entities.Count} entities, {_context.Systems.Count} systems.");
        }

        private void Update()
        {
            if (_context == null)
            {
                return;
            }

            // Unscaled and outside the tick: these must work while the simulation is frozen.
            if (_input != null)
            {
                if (_input.TogglePausePressed)
                {
                    _context.State.TogglePause();
                }

                if (_cameraRig != null)
                {
                    _cameraRig.Zoom(_input.ZoomAxis);
                }
            }

            _context.Tick(Time.deltaTime);
        }

        private void OnDestroy() => _context?.Dispose();

        private void BuildArena()
        {
            if (_config.ArenaPrefab != null)
            {
                // Real arena geometry is expected to bring its own lighting.
                Instantiate(_config.ArenaPrefab, _sceneRoot);
                return;
            }

            BuildPlaceholderLighting();

            // Placeholder floor so the project is walkable before any environment art exists.
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "PlaceholderFloor";
            floor.transform.SetParent(_sceneRoot, worldPositionStays: false);

            // Unity's plane primitive is ten metres across at unit scale.
            Vector2 size = _config.PlaceholderFloorSize;
            floor.transform.localScale = new Vector3(
                Mathf.Max(0.1f, size.x) / 10f,
                1f,
                Mathf.Max(0.1f, size.y) / 10f);
        }

        /// <summary>
        /// Adds a single directional light so the placeholder arena is readable.
        /// </summary>
        /// <remarks>
        /// Lighting is a property of the environment, so this exists only for the generated
        /// placeholder floor. Once real arena geometry is assigned it carries its own lights and
        /// this is skipped entirely — see <c>ART_STYLE_GUIDE.md</c>.
        /// </remarks>
        private void BuildPlaceholderLighting()
        {
            var lightObject = new GameObject("PlaceholderKeyLight");
            lightObject.transform.SetParent(_sceneRoot, worldPositionStays: false);

            // Angled to match the camera's yaw so characters are lit from the viewer's side and
            // silhouettes stay legible against the floor.
            lightObject.transform.rotation = Quaternion.Euler(50f, _config.CameraSettings.YawDegrees + 30f, 0f);

            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.Soft;
            light.intensity = 1.1f;
        }

        private void BuildCamera()
        {
            var cameraObject = new GameObject("RaidCamera", typeof(Camera), typeof(AudioListener));
            cameraObject.transform.SetParent(_sceneRoot, worldPositionStays: false);
            cameraObject.tag = "MainCamera";

            // Created inactive so the rig's Awake does not run before it has been configured.
            cameraObject.SetActive(false);
            _cameraRig = cameraObject.AddComponent<RaidCameraRig>();
            _cameraRig.Configure(_config.CameraSettings);
            cameraObject.SetActive(true);
        }

        /// <summary>
        /// Creates the object that holds the player's input source and drivers, still inactive so
        /// that nothing awakens before it has been wired to an actor.
        /// </summary>
        private void BuildPlayerRig()
        {
            _playerRig = new GameObject("~PlayerRig");
            _playerRig.transform.SetParent(transform, worldPositionStays: false);
            _playerRig.SetActive(false);

            _playerController = _playerRig.AddComponent<PlayerController>();
            _targetingController = _playerRig.AddComponent<PlayerTargetingController>();

            if (_config.Controls == null)
            {
                _context.Log.Warn(LogChannel.Bootstrap,
                    "No controls asset assigned; the character will not respond to input.");
                return;
            }

            _input = _playerRig.AddComponent<InputSystemPlayerInput>();
            _input.Configure(_config.Controls);
        }

        private void SpawnRosterAndBind()
        {
            var spawner = new ActorSpawner(
                _context,
                _config.DefaultActorPrefab,
                _config.PlaceholderActorHeight,
                _sceneRoot);

            CombatActor player = spawner.Spawn(_config.PlayerSpawn);
            if (player == null)
            {
                _context.Log.Error(LogChannel.Bootstrap, "The player character could not be spawned.");
                return;
            }

            spawner.SpawnAll(_config.AdditionalSpawns);

            // Unity's overloaded equality means a destroyed component is not C# null, so compare with
            // != null rather than using ?? here.
            IPlayerInputSource inputSource = _input != null
                ? (IPlayerInputSource)_input
                : NullPlayerInputSource.Instance;

            _playerController.Bind(player, inputSource, _cameraRig);
            _targetingController.Bind(_context, player, inputSource, _cameraRig);
            _playerRig.SetActive(true);

            _cameraRig.SetFocus(player.transform);
            _context.AddSystem(new PlayerDriverSystem(_playerController, _targetingController));
        }
    }
}
