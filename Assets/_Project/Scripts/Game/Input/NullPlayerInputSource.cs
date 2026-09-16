using UnityEngine;

namespace RaidSim.Game.Input
{
    /// <summary>
    /// An input source that never reports anything.
    /// </summary>
    /// <remarks>
    /// Used when a character is under AI control, and by headless runs where no device exists. It
    /// removes the need for a null check in the controller's hot path.
    /// </remarks>
    public sealed class NullPlayerInputSource : IPlayerInputSource
    {
        public static readonly NullPlayerInputSource Instance = new NullPlayerInputSource();

        private NullPlayerInputSource()
        {
        }

        public Vector2 MoveAxis => Vector2.zero;

        public float ZoomAxis => 0f;

        public Vector2 PointerPosition => Vector2.zero;

        public bool SelectTargetPressed => false;

        public bool CycleTargetPressed => false;

        public bool ClearTargetPressed => false;

        public bool TogglePausePressed => false;
    }
}
