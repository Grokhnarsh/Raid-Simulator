using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RaidSim.Game.Input
{
    /// <summary>
    /// <see cref="IPlayerInputSource"/> backed by an Input System action asset.
    /// </summary>
    /// <remarks>
    /// <para>Actions are resolved by name from a serialized <see cref="InputActionAsset"/> rather
    /// than through a generated wrapper class. That keeps the binding asset the single source of
    /// truth — rebinding a key, or adding a gamepad scheme, is an edit to the asset with no code
    /// regeneration step — and it keeps this component usable with any action map that offers the
    /// same seven actions.</para>
    /// <para>A missing action is reported once at startup with the name that was looked for, so a
    /// typo in the asset is a loud, immediate failure rather than a control that silently does
    /// nothing.</para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class InputSystemPlayerInput : MonoBehaviour, IPlayerInputSource
    {
        [Header("Bindings")]
        [Tooltip("Action asset containing the gameplay map. Required.")]
        [SerializeField]
        private InputActionAsset _actions;

        [Tooltip("Name of the action map to enable while playing.")]
        [SerializeField]
        private string _actionMapName = "Gameplay";

        [Header("Action names")]
        [Tooltip("Vector2 action driving movement.")]
        [SerializeField]
        private string _moveAction = "Move";

        [Tooltip("Axis action driving camera zoom.")]
        [SerializeField]
        private string _zoomAction = "Zoom";

        [Tooltip("Vector2 action reporting the pointer position in screen pixels.")]
        [SerializeField]
        private string _pointerAction = "Pointer";

        [Tooltip("Button action selecting whatever is under the pointer.")]
        [SerializeField]
        private string _selectTargetAction = "SelectTarget";

        [Tooltip("Button action cycling to the next hostile target.")]
        [SerializeField]
        private string _cycleTargetAction = "CycleTarget";

        [Tooltip("Button action dropping the current target.")]
        [SerializeField]
        private string _clearTargetAction = "ClearTarget";

        [Tooltip("Button action toggling pause.")]
        [SerializeField]
        private string _togglePauseAction = "TogglePause";

        private InputActionMap _map;
        private InputAction _move;
        private InputAction _zoom;
        private InputAction _pointer;
        private InputAction _select;
        private InputAction _cycle;
        private InputAction _clear;
        private InputAction _pause;

        /// <inheritdoc />
        public Vector2 MoveAxis => ReadVector(_move);

        /// <inheritdoc />
        public float ZoomAxis => _zoom?.ReadValue<float>() ?? 0f;

        /// <inheritdoc />
        public Vector2 PointerPosition => ReadVector(_pointer);

        /// <inheritdoc />
        public bool SelectTargetPressed => _select?.WasPressedThisFrame() ?? false;

        /// <inheritdoc />
        public bool CycleTargetPressed => _cycle?.WasPressedThisFrame() ?? false;

        /// <inheritdoc />
        public bool ClearTargetPressed => _clear?.WasPressedThisFrame() ?? false;

        /// <inheritdoc />
        public bool TogglePausePressed => _pause?.WasPressedThisFrame() ?? false;

        /// <summary>
        /// Supplies the action asset before the component awakens. Used when bootstrap adds this
        /// component at runtime instead of authoring it in a scene.
        /// </summary>
        public void Configure(InputActionAsset actions, string actionMapName = null)
        {
            _actions = actions;
            if (!string.IsNullOrWhiteSpace(actionMapName))
            {
                _actionMapName = actionMapName;
            }
        }

        private void Awake()
        {
            if (_actions == null)
            {
                Debug.LogError(
                    $"{nameof(InputSystemPlayerInput)} on '{name}' has no action asset assigned; the game will not respond to input.",
                    this);
                enabled = false;
                return;
            }

            _map = _actions.FindActionMap(_actionMapName, throwIfNotFound: false);
            if (_map == null)
            {
                Debug.LogError(
                    $"Action map '{_actionMapName}' was not found in '{_actions.name}'.", this);
                enabled = false;
                return;
            }

            _move = Resolve(_moveAction);
            _zoom = Resolve(_zoomAction);
            _pointer = Resolve(_pointerAction);
            _select = Resolve(_selectTargetAction);
            _cycle = Resolve(_cycleTargetAction);
            _clear = Resolve(_clearTargetAction);
            _pause = Resolve(_togglePauseAction);
        }

        private void OnEnable() => _map?.Enable();

        private void OnDisable() => _map?.Disable();

        private InputAction Resolve(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return null;
            }

            InputAction action = _map.FindAction(actionName, throwIfNotFound: false);
            if (action == null)
            {
                Debug.LogError(
                    $"Action '{actionName}' was not found in map '{_map.name}'. That control will do nothing.",
                    this);
            }

            return action;
        }

        private static Vector2 ReadVector(InputAction action) =>
            action?.ReadValue<Vector2>() ?? Vector2.zero;
    }
}
