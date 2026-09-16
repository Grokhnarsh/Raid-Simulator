using System;
using System.Collections.Generic;
using EmberDepths.Core.Grid;
using UnityEngine;

namespace EmberDepths.Content
{
    public enum ActorAnimState
    {
        Idle = 0,
        Walk = 1,
        Attack = 2,
        Cast = 3,
        Hurt = 4,
        Die = 5
    }

    /// <summary>Frames for one facing. A wrapper class because Unity cannot serialise Sprite[][].</summary>
    [Serializable]
    public sealed class DirectionalFrames
    {
        public Sprite[] Frames = Array.Empty<Sprite>();
        public bool HasFrames => Frames != null && Frames.Length > 0;
    }

    [Serializable]
    public sealed class ActorAnimation
    {
        public ActorAnimState State = ActorAnimState.Idle;

        [Min(1)] public int Fps = 8;

        public bool Loop = true;

        /// <summary>Indexed by <see cref="IsoDirection"/>. Always length 8.</summary>
        public DirectionalFrames[] ByDirection = NewDirectionArray();

        public static DirectionalFrames[] NewDirectionArray()
        {
            var arr = new DirectionalFrames[8];
            for (int i = 0; i < 8; i++) arr[i] = new DirectionalFrames();
            return arr;
        }

        public float Duration(IsoDirection dir)
        {
            DirectionalFrames d = Get(dir);
            return d == null || !d.HasFrames ? 0f : d.Frames.Length / (float)Mathf.Max(1, Fps);
        }

        public DirectionalFrames Get(IsoDirection dir)
        {
            if (ByDirection == null || ByDirection.Length == 0) return null;
            int i = (int)dir;
            return i >= 0 && i < ByDirection.Length ? ByDirection[i] : ByDirection[0];
        }
    }

    /// <summary>
    /// Everything needed to draw one actor: eight facings of idle art plus optional
    /// animation clips.
    ///
    /// PixelLab exports exactly this shape — eight rotations, then animations laid
    /// out per rotation — so <c>EmberDepths/Art/Build Visual Set From Folder</c>
    /// can fill this asset in automatically from a downloaded sprite folder.
    /// Nothing here is required except the idle frames; a set with only those
    /// renders as a static sprite that still turns to face its target.
    /// </summary>
    [CreateAssetMenu(menuName = "EmberDepths/Actor Visual Set", fileName = "VS_NewActor", order = 40)]
    public sealed class ActorVisualSet : ScriptableObject
    {
        [Header("Idle (required)")]
        [Tooltip("One sprite per facing, indexed by IsoDirection: South, SouthEast, East, ...")]
        public Sprite[] IdleByDirection = new Sprite[8];

        [Header("Animations (optional)")]
        public List<ActorAnimation> Animations = new List<ActorAnimation>();

        [Header("Layout")]
        [Tooltip("Where the health bar and status icons sit, in world units above the actor's feet.")]
        public float OverheadOffset = 1.2f;

        [Tooltip("Sprite drawn under the actor. Sells the isometric grounding more than any other single asset.")]
        public Sprite Shadow;

        [Min(0.1f)] public float ShadowScale = 1f;

        public Vector2 ShadowOffset = new Vector2(0f, -0.05f);

        [Header("UI")]
        public Sprite Portrait;

        public Sprite GetIdle(IsoDirection dir)
        {
            if (IdleByDirection == null || IdleByDirection.Length == 0) return null;
            int i = (int)dir;
            Sprite s = i >= 0 && i < IdleByDirection.Length ? IdleByDirection[i] : null;
            // Fall back to south so a half-imported set still renders something.
            return s != null ? s : IdleByDirection[0];
        }

        public ActorAnimation GetAnimation(ActorAnimState state)
        {
            for (int i = 0; i < Animations.Count; i++)
                if (Animations[i] != null && Animations[i].State == state)
                    return Animations[i];
            return null;
        }

        /// <summary>
        /// Frame for a state at a point in time, falling back to the idle sprite
        /// whenever the requested animation is missing. Callers never have to
        /// null-check the animation itself.
        /// </summary>
        public Sprite Sample(ActorAnimState state, IsoDirection dir, float elapsedSeconds, out bool finished)
        {
            finished = true;

            ActorAnimation anim = GetAnimation(state);
            DirectionalFrames frames = anim?.Get(dir);
            if (frames == null || !frames.HasFrames) return GetIdle(dir);

            int count = frames.Frames.Length;
            float frameTime = 1f / Mathf.Max(1, anim.Fps);
            int index = Mathf.FloorToInt(elapsedSeconds / frameTime);

            if (anim.Loop)
            {
                finished = false;
                index = ((index % count) + count) % count;
            }
            else
            {
                finished = index >= count - 1;
                index = Mathf.Clamp(index, 0, count - 1);
            }

            Sprite s = frames.Frames[index];
            return s != null ? s : GetIdle(dir);
        }

        private void OnValidate()
        {
            if (IdleByDirection == null || IdleByDirection.Length != 8)
                Array.Resize(ref IdleByDirection, 8);

            for (int i = 0; i < Animations.Count; i++)
            {
                ActorAnimation a = Animations[i];
                if (a == null) continue;
                if (a.ByDirection == null || a.ByDirection.Length != 8)
                    a.ByDirection = ActorAnimation.NewDirectionArray();
            }
        }
    }
}
