using RaidSim.CameraRig.Data;
using UnityEngine;

namespace RaidSim.CameraRig.Runtime
{
    /// <summary>
    /// The 2.5D raid camera: a fixed angle that follows a focus point, with zoom and arena bounds.
    /// </summary>
    /// <remarks>
    /// <para>The camera never rotates during play. A fixed viewing angle is what makes the floor
    /// readable: a player learns once where a danger zone appears relative to their character and the
    /// knowledge keeps working. Free rotation would undo that, so yaw is an authored setting rather
    /// than an input.</para>
    /// <para>The focus point is deliberately not "the player transform". It is a
    /// <see cref="Transform"/> reference that bootstrap points at whoever is being controlled, so
    /// switching control to another group member, or later framing the whole raid, needs no change
    /// here.</para>
    /// <para>The rig runs in <c>LateUpdate</c> so it sees the final position of everything it
    /// follows, and it reads unscaled time so the camera still settles smoothly while the simulation
    /// is paused.</para>
    /// </remarks>
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    public sealed class RaidCameraRig : MonoBehaviour
    {
        [Tooltip("Authored camera configuration. Required.")]
        [SerializeField]
        private CameraRigSettings _settings;

        [Tooltip("What the camera follows. Bootstrap assigns the controlled character.")]
        [SerializeField]
        private Transform _focus;

        private Camera _camera;
        private Vector3 _focusPosition;
        private Vector3 _focusVelocity;
        private float _distance;
        private float _targetDistance;
        private float _zoomVelocity;

        /// <summary>The camera this rig drives.</summary>
        public Camera Camera => _camera;

        /// <summary>Current zoom distance, in metres.</summary>
        public float Distance => _distance;

        /// <summary>
        /// Supplies the rig's settings before it awakens.
        /// </summary>
        /// <remarks>
        /// The rig normally receives its settings through the inspector. Bootstrap builds it in code,
        /// so the reference is pushed in through this entry point rather than by making the field
        /// public, which would invite writes from anywhere at any time.
        /// </remarks>
        public void Configure(CameraRigSettings settings)
        {
            _settings = settings;
            if (_camera == null)
            {
                return;
            }

            // Already awake: re-apply immediately rather than waiting for a reload.
            _targetDistance = _settings != null ? _settings.DefaultDistance : _targetDistance;
            ApplyProjection();
            SnapToFocus();
        }

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            if (_settings == null)
            {
                Debug.LogError($"{nameof(RaidCameraRig)} on '{name}' has no settings asset; the camera will not run.", this);
                enabled = false;
                return;
            }

            _targetDistance = _settings.DefaultDistance;
            _distance = _targetDistance;
            ApplyProjection();
            SnapToFocus();
        }

        /// <summary>Points the camera at a new transform and snaps to it without a glide.</summary>
        public void SetFocus(Transform focus)
        {
            _focus = focus;
            SnapToFocus();
        }

        /// <summary>Applies zoom input. Positive values zoom in.</summary>
        public void Zoom(float amount)
        {
            if (_settings == null || Mathf.Approximately(amount, 0f))
            {
                return;
            }

            _targetDistance = Mathf.Clamp(
                _targetDistance - (amount * _settings.ZoomSpeed),
                _settings.MinDistance,
                _settings.MaxDistance);
        }

        /// <summary>Jumps to the current focus, cancelling any in-flight smoothing.</summary>
        public void SnapToFocus()
        {
            if (_settings == null)
            {
                return;
            }

            _focusPosition = ClampToBounds(DesiredFocusPosition());
            _focusVelocity = Vector3.zero;
            _zoomVelocity = 0f;
            _distance = _targetDistance;
            ApplyTransform();
        }

        private void LateUpdate()
        {
            if (_settings == null)
            {
                return;
            }

            // Unscaled time: the camera keeps settling while the simulation clock is paused.
            float delta = Time.unscaledDeltaTime;

            _focusPosition = Vector3.SmoothDamp(
                _focusPosition,
                ClampToBounds(DesiredFocusPosition()),
                ref _focusVelocity,
                _settings.FollowSmoothTime,
                Mathf.Infinity,
                delta);

            _distance = _settings.ZoomSmoothTime > 0f
                ? Mathf.SmoothDamp(_distance, _targetDistance, ref _zoomVelocity, _settings.ZoomSmoothTime, Mathf.Infinity, delta)
                : _targetDistance;

            ApplyTransform();
        }

        private Vector3 DesiredFocusPosition() =>
            (_focus != null ? _focus.position : _focusPosition) + _settings.FocusOffset;

        private Vector3 ClampToBounds(Vector3 position)
        {
            if (!_settings.UseBounds)
            {
                return position;
            }

            Bounds bounds = _settings.FocusBounds;
            return new Vector3(
                Mathf.Clamp(position.x, bounds.min.x, bounds.max.x),
                position.y,
                Mathf.Clamp(position.z, bounds.min.z, bounds.max.z));
        }

        private void ApplyTransform()
        {
            Quaternion rotation = _settings.Rotation;
            transform.SetPositionAndRotation(
                _focusPosition - (rotation * Vector3.forward * _distance),
                rotation);

            if (_camera != null && _camera.orthographic)
            {
                // Keep the framed area constant as the zoom distance changes.
                _camera.orthographicSize = _settings.OrthographicSizeFor(_distance);
            }
        }

        private void ApplyProjection()
        {
            bool orthographic = _settings.Projection == CameraProjection.Orthographic;
            _camera.orthographic = orthographic;
            if (orthographic)
            {
                _camera.orthographicSize = _settings.OrthographicSizeFor(_distance);
            }
            else
            {
                _camera.fieldOfView = _settings.FieldOfView;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_settings == null || !Application.isPlaying)
            {
                return;
            }

            _camera = _camera != null ? _camera : GetComponent<Camera>();
            ApplyProjection();
        }

        private void OnDrawGizmosSelected()
        {
            if (_settings == null || !_settings.UseBounds)
            {
                return;
            }

            Bounds bounds = _settings.FocusBounds;
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.6f);
            Gizmos.DrawWireCube(bounds.center, new Vector3(bounds.size.x, 0.1f, bounds.size.z));
        }
#endif
    }
}
