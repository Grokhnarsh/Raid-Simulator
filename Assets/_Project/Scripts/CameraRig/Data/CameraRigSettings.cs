using UnityEngine;

namespace RaidSim.CameraRig.Data
{
    /// <summary>How the raid camera projects the world.</summary>
    public enum CameraProjection
    {
        /// <summary>
        /// True isometric: no perspective foreshortening, so a circle on the ground is the same size
        /// wherever it is. Area-of-effect telegraphs read identically across the arena.
        /// </summary>
        Orthographic = 0,

        /// <summary>
        /// Perspective with a narrow field of view. Keeps some depth cue on tall geometry at the cost
        /// of area-of-effect shapes changing apparent size with distance.
        /// </summary>
        Perspective = 1,
    }

    /// <summary>
    /// Authored configuration for the 2.5D camera.
    /// </summary>
    /// <remarks>
    /// <para>The camera is the reason this is a 2.5D game rather than a top-down one: the world is
    /// genuinely 3D, and a fixed, controlled viewing angle is what turns it into a readable tactical
    /// picture. Every value that defines that angle lives here so it can be tuned without touching
    /// code.</para>
    /// <para>The project ships with orthographic selected. Readability of danger zones outranks depth
    /// cueing in a game whose core skill is reading the floor — see <c>ART_STYLE_GUIDE.md</c>.
    /// The alternative is one field change away because nothing in gameplay depends on it.</para>
    /// </remarks>
    [CreateAssetMenu(
        fileName = "CameraRigSettings",
        menuName = "Raid Simulator/Camera/Camera Rig Settings",
        order = 30)]
    public sealed class CameraRigSettings : ScriptableObject
    {
        [Header("Projection")]
        [Tooltip("Orthographic keeps area-of-effect shapes a constant size; perspective keeps depth cues.")]
        [SerializeField]
        private CameraProjection _projection = CameraProjection.Orthographic;

        [Tooltip("Vertical field of view used when the projection is perspective.")]
        [Range(10f, 80f)]
        [SerializeField]
        private float _fieldOfView = 35f;

        [Header("Angle")]
        [Tooltip("Downward tilt in degrees. Higher sees more floor; lower sees more of the characters.")]
        [Range(20f, 89f)]
        [SerializeField]
        private float _pitchDegrees = 50f;

        [Tooltip("Rotation around the vertical axis, in degrees. The classic isometric look is 45.")]
        [Range(0f, 360f)]
        [SerializeField]
        private float _yawDegrees = 45f;

        [Header("Zoom")]
        [Tooltip("Distance from the focus point at the closest zoom, in metres.")]
        [Min(1f)]
        [SerializeField]
        private float _minDistance = 12f;

        [Tooltip("Distance from the focus point at the widest zoom, in metres.")]
        [Min(1f)]
        [SerializeField]
        private float _maxDistance = 38f;

        [Tooltip("Distance the camera starts at, in metres.")]
        [Min(1f)]
        [SerializeField]
        private float _defaultDistance = 24f;

        [Tooltip("Metres of zoom per unit of scroll input.")]
        [Min(0.1f)]
        [SerializeField]
        private float _zoomSpeed = 8f;

        [Tooltip("Seconds for the zoom to settle after an input. Zero snaps instantly.")]
        [Min(0f)]
        [SerializeField]
        private float _zoomSmoothTime = 0.12f;

        [Header("Follow")]
        [Tooltip("Seconds for the camera to catch up with its focus. Higher is calmer but laggier.")]
        [Min(0f)]
        [SerializeField]
        private float _followSmoothTime = 0.15f;

        [Tooltip("Offset applied to the focus point, in metres. Raise it to aim at the chest rather than the feet.")]
        [SerializeField]
        private Vector3 _focusOffset = new Vector3(0f, 1.2f, 0f);

        [Header("Bounds")]
        [Tooltip("Whether the focus point is confined to a rectangle. Keeps the camera inside the arena.")]
        [SerializeField]
        private bool _useBounds = true;

        [Tooltip("Centre of the allowed focus rectangle, in world space.")]
        [SerializeField]
        private Vector3 _boundsCenter = Vector3.zero;

        [Tooltip("Full width and depth of the allowed focus rectangle, in metres. Height is ignored.")]
        [SerializeField]
        private Vector3 _boundsSize = new Vector3(120f, 0f, 120f);

        public CameraProjection Projection => _projection;

        public float FieldOfView => _fieldOfView;

        public float PitchDegrees => _pitchDegrees;

        public float YawDegrees => _yawDegrees;

        public float MinDistance => Mathf.Min(_minDistance, _maxDistance);

        public float MaxDistance => Mathf.Max(_minDistance, _maxDistance);

        public float DefaultDistance => Mathf.Clamp(_defaultDistance, MinDistance, MaxDistance);

        public float ZoomSpeed => _zoomSpeed;

        public float ZoomSmoothTime => _zoomSmoothTime;

        public float FollowSmoothTime => _followSmoothTime;

        public Vector3 FocusOffset => _focusOffset;

        public bool UseBounds => _useBounds;

        /// <summary>The rectangle the focus point is confined to. Flattened to the horizontal plane.</summary>
        public Bounds FocusBounds => new Bounds(_boundsCenter, new Vector3(
            Mathf.Abs(_boundsSize.x),
            0f,
            Mathf.Abs(_boundsSize.z)));

        /// <summary>
        /// Orthographic half-height that shows the same amount of ground as a perspective camera at
        /// <paramref name="distance"/> would. Keeps zoom feeling identical under both projections.
        /// </summary>
        public float OrthographicSizeFor(float distance) =>
            distance * Mathf.Tan(_fieldOfView * 0.5f * Mathf.Deg2Rad);

        /// <summary>The rotation the camera is locked to.</summary>
        public Quaternion Rotation => Quaternion.Euler(_pitchDegrees, _yawDegrees, 0f);
    }
}
