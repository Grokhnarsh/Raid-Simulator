using RaidSim.CameraRig.Data;
using RaidSim.Combat.Data;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RaidSim.Game.Bootstrap
{
    /// <summary>
    /// Everything <see cref="GameBootstrap"/> needs to assemble a playable scene.
    /// </summary>
    /// <remarks>
    /// <para>The bootstrap scene contains one GameObject. Everything else — the camera, the ground,
    /// the player, the practice targets — is built from this asset at load. That keeps the scene file
    /// tiny and mergeable, and it means a different starting setup is a different asset rather than a
    /// different scene.</para>
    /// <para>No tuning number appears in bootstrap code; they all live here.</para>
    /// </remarks>
    [CreateAssetMenu(
        fileName = "BootstrapConfig",
        menuName = "Raid Simulator/Game/Bootstrap Config",
        order = 0)]
    public sealed class BootstrapConfig : ScriptableObject
    {
        [Header("Camera")]
        [Tooltip("Configuration for the 2.5D raid camera. Required.")]
        [SerializeField]
        private CameraRigSettings _cameraSettings;

        [Header("Combat")]
        [Tooltip("Balance numbers for the damage and healing pipelines. Required.")]
        [SerializeField]
        private CombatTuningAsset _combatTuning;

        [Header("Input")]
        [Tooltip("Input System action asset containing the gameplay map. Required for player control.")]
        [SerializeField]
        private InputActionAsset _controls;

        [Header("Roster")]
        [Tooltip("The character the player controls.")]
        [SerializeField]
        private SpawnEntry _playerSpawn;

        [Tooltip("Everything else placed at load: practice targets now, group members and trash later.")]
        [SerializeField]
        private SpawnEntry[] _additionalSpawns = new SpawnEntry[0];

        [Header("Presentation")]
        [Tooltip("Body prefab used when a character definition supplies none. Optional: a placeholder is generated if empty.")]
        [SerializeField]
        private GameObject _defaultActorPrefab;

        [Tooltip("Arena geometry. Optional: a flat placeholder floor is generated if empty.")]
        [SerializeField]
        private GameObject _arenaPrefab;

        [Tooltip("Width and depth of the generated placeholder floor, in metres.")]
        [SerializeField]
        private Vector2 _placeholderFloorSize = new Vector2(80f, 80f);

        [Tooltip("Height of a generated placeholder body, in metres.")]
        [Min(0.2f)]
        [SerializeField]
        private float _placeholderActorHeight = 1.9f;

        [Header("Startup")]
        [Tooltip("Whether to enter the playing state as soon as bootstrap finishes.")]
        [SerializeField]
        private bool _autoStart = true;

        public CameraRigSettings CameraSettings => _cameraSettings;

        public CombatTuningAsset CombatTuning => _combatTuning;

        public InputActionAsset Controls => _controls;

        public SpawnEntry PlayerSpawn => _playerSpawn;

        public SpawnEntry[] AdditionalSpawns => _additionalSpawns;

        public GameObject DefaultActorPrefab => _defaultActorPrefab;

        public GameObject ArenaPrefab => _arenaPrefab;

        public Vector2 PlaceholderFloorSize => _placeholderFloorSize;

        public float PlaceholderActorHeight => _placeholderActorHeight;

        public bool AutoStart => _autoStart;

        /// <summary>
        /// Reports why this configuration cannot produce a playable scene, or null when it is
        /// complete. Bootstrap calls this first so a mis-authored asset fails at load with a message
        /// naming the field, rather than as a null reference three systems later.
        /// </summary>
        public string Validate()
        {
            if (_cameraSettings == null)
            {
                return $"'{name}' has no camera settings assigned.";
            }

            if (_combatTuning == null)
            {
                return $"'{name}' has no combat tuning assigned.";
            }

            string tuningProblem = _combatTuning.Validate();
            if (tuningProblem != null)
            {
                return tuningProblem;
            }

            if (!_playerSpawn.IsValid)
            {
                return $"'{name}' has no player character assigned.";
            }

            string playerProblem = _playerSpawn.Character.Validate();
            if (playerProblem != null)
            {
                return playerProblem;
            }

            for (int i = 0; i < _additionalSpawns.Length; i++)
            {
                if (!_additionalSpawns[i].IsValid)
                {
                    continue;
                }

                string problem = _additionalSpawns[i].Character.Validate();
                if (problem != null)
                {
                    return problem;
                }
            }

            return null;
        }
    }
}
