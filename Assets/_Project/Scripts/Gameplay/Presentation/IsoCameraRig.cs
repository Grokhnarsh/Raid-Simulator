using EmberDepths.Core.Grid;
using EmberDepths.Gameplay.Run;
using UnityEngine;

namespace EmberDepths.Gameplay.Presentation
{
    /// <summary>
    /// Configures and drives the isometric camera.
    ///
    /// Two things here are not optional for a 2D isometric game and are easy to
    /// get wrong by hand, so they are set in code:
    ///
    ///   * <c>transparencySortMode = CustomAxis</c> with axis (0,1,0). This is
    ///     what makes sprites sort by how far up the screen they are, which is
    ///     the whole illusion. Without it, actors pop in front of walls.
    ///   * An orthographic size derived from pixels-per-unit, so one source pixel
    ///     maps to a whole number of screen pixels and the art stays crisp.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    public sealed class IsoCameraRig : MonoBehaviour
    {
        [Tooltip("Followed automatically; leave empty to track the runner's local player.")]
        public Transform Target;

        public DungeonRunner Runner;

        [Header("Framing")]
        [Tooltip("Vertical resolution the game is composed for. 360 shows roughly 11 tiles top to bottom.")]
        [Min(64)] public int VerticalPixels = 360;

        [Tooltip("Seconds for the camera to catch up. 0 locks it rigidly to the target.")]
        [Min(0f)] public float SmoothTime = 0.12f;

        [Tooltip("The target can move this far from the centre before the camera follows, in world units.")]
        public Vector2 DeadZone = new Vector2(0.6f, 0.35f);

        private Camera _camera;
        private Vector3 _velocity;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            Configure(_camera, VerticalPixels);
        }

        /// <summary>
        /// Applies the isometric camera setup. Public and static so the editor's
        /// scene builder can configure a camera without entering play mode.
        /// </summary>
        public static void Configure(Camera camera, int verticalPixels)
        {
            camera.orthographic = true;
            camera.orthographicSize = verticalPixels / (2f * IsoGrid.PixelsPerUnit);
            camera.transparencySortMode = TransparencySortMode.CustomAxis;
            camera.transparencySortAxis = new Vector3(0f, 1f, 0f);

            // Sprites live on z = 0; the camera must sit behind them and see far
            // enough to cover a whole floor without clipping.
            Vector3 p = camera.transform.position;
            camera.transform.position = new Vector3(p.x, p.y, -20f);
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 100f;
        }

        private void LateUpdate()
        {
            Transform target = ResolveTarget();
            if (target == null) return;

            Vector3 current = transform.position;
            Vector3 desired = new Vector3(target.position.x, target.position.y, current.z);

            // The dead zone stops the camera drifting with every step of a
            // grid-based walk cycle, which otherwise reads as a persistent judder.
            Vector2 offset = desired - current;
            if (Mathf.Abs(offset.x) < DeadZone.x) desired.x = current.x;
            if (Mathf.Abs(offset.y) < DeadZone.y) desired.y = current.y;

            transform.position = SmoothTime <= 0f
                ? desired
                : Vector3.SmoothDamp(current, desired, ref _velocity, SmoothTime);
        }

        private Transform ResolveTarget()
        {
            if (Target != null) return Target;
            if (Runner == null || Runner.LocalPlayer == null) return null;
            return Runner.LocalPlayer.transform;
        }

        /// <summary>Snaps to the target without easing. Called on spawn and after a teleport.</summary>
        public void SnapToTarget()
        {
            Transform target = ResolveTarget();
            if (target == null) return;

            _velocity = Vector3.zero;
            transform.position = new Vector3(target.position.x, target.position.y, transform.position.z);
        }
    }
}
