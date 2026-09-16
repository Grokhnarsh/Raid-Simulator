using System.Text;
using RaidSim.Characters.Runtime;
using RaidSim.Core.Entities;
using RaidSim.Core.Simulation;
using RaidSim.Core.Stats;
using RaidSim.Game.Bootstrap;
using UnityEngine;

namespace RaidSim.DebugTools.Runtime
{
    /// <summary>
    /// On-screen readout of simulation state, for development builds and the editor only.
    /// </summary>
    /// <remarks>
    /// <para>A raid simulator is mostly invisible: threat tables, cooldowns, target choices and
    /// phase timers all decide the fight without drawing anything. Being able to see that state is a
    /// development requirement, not a nicety, so the tooling starts in Phase 1 and grows with the
    /// systems it inspects.</para>
    /// <para>Everything here is inside <c>UNITY_EDITOR || DEVELOPMENT_BUILD</c>. A release build
    /// contains no overlay, no key handler and no string building — the class compiles down to an
    /// empty component, which is what section 27 of the design brief requires.</para>
    /// <para>This shows only what Phase 1 produces. Threat, AI state, cast bars and mechanic
    /// telegraphs join it as their systems land.</para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class DevelopmentOverlay : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [Tooltip("Bootstrap whose simulation this overlay inspects. Found on the same object when empty.")]
        [SerializeField]
        private GameBootstrap _bootstrap;

        [Tooltip("Whether the overlay starts visible.")]
        [SerializeField]
        private bool _visible = true;

        [Tooltip("Size of the overlay text, in points.")]
        [Min(8)]
        [SerializeField]
        private int _fontSize = 13;

        private readonly StringBuilder _builder = new StringBuilder(512);
        private GUIStyle _style;

        private void Awake()
        {
            if (_bootstrap == null)
            {
                _bootstrap = GetComponent<GameBootstrap>();
            }
        }

        /// <summary>Shows or hides the overlay. Bound to a debug key by the debug console later.</summary>
        public void SetVisible(bool visible) => _visible = visible;

        private void OnGUI()
        {
            if (!_visible || _bootstrap == null || _bootstrap.Context == null)
            {
                return;
            }

            _style ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = _fontSize,
                richText = false,
                wordWrap = false,
            };

            GUI.Label(new Rect(12f, 12f, 520f, 260f), BuildReadout(_bootstrap.Context), _style);
        }

        private string BuildReadout(SimulationContext context)
        {
            _builder.Clear();
            _builder.Append("state: ").Append(context.State.Current)
                .Append("   t=").Append(context.Clock.Now.ToString("0.00")).Append('s')
                .Append("   tick ").Append(context.Clock.TickCount)
                .Append(context.Clock.IsPaused ? "   [PAUSED]" : string.Empty)
                .AppendLine();

            _builder.Append("entities: ").Append(context.Entities.Count)
                .Append("   systems: ").Append(context.Systems.Count)
                .AppendLine();

            AppendActor(_bootstrap.PlayerActor);
            return _builder.ToString();
        }

        private void AppendActor(CombatActor actor)
        {
            if (actor == null || !actor.IsInitialised)
            {
                _builder.AppendLine("player: none");
                return;
            }

            _builder.AppendLine()
                .Append("player: ").Append(actor.DisplayName)
                .Append("  (").Append(actor.Role).Append(", level ").Append(actor.Level).Append(')')
                .AppendLine();

            _builder.Append("  hp ").Append(Mathf.RoundToInt(actor.Health.Current))
                .Append('/').Append(Mathf.RoundToInt(actor.Health.Max))
                .Append("   resource ").Append(Mathf.RoundToInt(actor.Resource.Current))
                .Append('/').Append(Mathf.RoundToInt(actor.Resource.Max))
                .Append(" (").Append(actor.Resource.Kind).Append(')')
                .AppendLine();

            _builder.Append("  pos ").Append(actor.Position)
                .Append("   speed ").Append(actor.Stats.Get(StatType.MovementSpeed).ToString("0.0"))
                .Append("   moving ").Append(actor.Locomotor != null && actor.Locomotor.IsMoving)
                .AppendLine();

            ISimEntity target = actor.Target?.Target;
            if (target == null)
            {
                _builder.AppendLine("  target: none");
                return;
            }

            _builder.Append("  target: ").Append(target.DisplayName)
                .Append("   distance ")
                .Append(Core.Targeting.TargetFilter.EdgeDistance(actor, target).ToString("0.0")).Append('m');

            if (target is ICombatEntity combatant)
            {
                _builder.Append("   hp ").Append(Mathf.RoundToInt(combatant.Health.Current))
                    .Append('/').Append(Mathf.RoundToInt(combatant.Health.Max));
            }

            _builder.AppendLine();
        }
#endif
    }
}
