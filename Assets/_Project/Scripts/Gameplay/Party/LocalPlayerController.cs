using System.Collections.Generic;
using EmberDepths.Content;
using EmberDepths.Core.Grid;
using EmberDepths.Gameplay.Actors;
using EmberDepths.Gameplay.Combat;
using EmberDepths.Gameplay.Presentation;
using EmberDepths.Gameplay.Run;
using UnityEngine;

namespace EmberDepths.Gameplay.Party
{
    /// <summary>
    /// Translates keyboard and mouse into intents for whichever party slot the
    /// human is driving.
    ///
    /// Input is read here and nowhere else, and everything it does goes through
    /// the same <see cref="AbilityBook.TryCast"/> and <see cref="ActorMotor"/>
    /// calls the AI uses. Swapping this for the Input System package, a gamepad,
    /// or a network message is a change to this one file.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LocalPlayerController : MonoBehaviour
    {
        [Tooltip("Left as a direct reference so the controller works in a scene with several runners.")]
        public DungeonRunner Runner;

        public Camera WorldCamera;

        [Header("Feel")]
        [Tooltip("Holding a movement key re-issues a one-step path this often, in seconds.")]
        [Min(0.02f)] public float MoveRepeatInterval = 0.08f;

        /// <summary>The enemy the action bar casts at. Null means "nearest".</summary>
        public Actor CurrentTarget { get; private set; }

        public event System.Action<Actor> TargetChanged;

        /// <summary>Set when the last cast attempt was refused, for the HUD to explain why.</summary>
        public CastRefusal LastRefusal { get; private set; }

        private readonly List<GridCoord> _path = new List<GridCoord>(32);
        private readonly List<Actor> _cycleBuffer = new List<Actor>(16);

        private float _moveCooldown;
        private int _cycleIndex = -1;
        private GameObject _targetRing;

        private static readonly KeyCode[] AbilityKeys =
        {
            KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3,
            KeyCode.Alpha4, KeyCode.Alpha5, KeyCode.Alpha6
        };

        private Actor Player => Runner != null ? Runner.LocalPlayer : null;

        private void Awake()
        {
            if (WorldCamera == null) WorldCamera = Camera.main;
        }

        private void Update()
        {
            Actor player = Player;
            if (player == null || !player.IsAlive) return;

            PruneTarget();
            HandleTargeting(player);
            HandleAbilities(player);
            HandleMovement(player);
            UpdateTargetRing();
        }

        // --- targeting -------------------------------------------------------------

        private void PruneTarget()
        {
            if (CurrentTarget == null) return;
            if (CurrentTarget.IsAlive) return;

            CurrentTarget = null;
            TargetChanged?.Invoke(null);
        }

        private void HandleTargeting(Actor player)
        {
            if (Input.GetKeyDown(KeyCode.Tab)) CycleTarget(player);
            if (Input.GetKeyDown(KeyCode.Escape)) SetTarget(null);

            if (!Input.GetMouseButtonDown(0)) return;

            GridCoord cell = MouseCell();
            Actor clicked = player.World.Actors.AtCell(cell, player.World.Map);

            // Clicking an enemy targets it; clicking anywhere else is a move order.
            // Two verbs on one button is the isometric convention and keeps the
            // left hand free for abilities.
            if (clicked != null && player.IsHostileTo(clicked))
            {
                SetTarget(clicked);
                return;
            }

            IssueMoveOrder(player, cell);
        }

        private void CycleTarget(Actor player)
        {
            _cycleBuffer.Clear();

            IReadOnlyList<Actor> hostiles = player.World.Actors.Hostiles;
            for (int i = 0; i < hostiles.Count; i++)
            {
                Actor h = hostiles[i];
                if (h == null || !h.IsAlive || h.Stats.Untargetable) continue;
                if (GridCoord.Chebyshev(player.Cell, h.Cell) > 14) continue;
                _cycleBuffer.Add(h);
            }

            if (_cycleBuffer.Count == 0)
            {
                SetTarget(null);
                return;
            }

            _cycleBuffer.Sort((a, b) =>
                GridCoord.Chebyshev(player.Cell, a.Cell).CompareTo(GridCoord.Chebyshev(player.Cell, b.Cell)));

            _cycleIndex = (_cycleIndex + 1) % _cycleBuffer.Count;
            SetTarget(_cycleBuffer[_cycleIndex]);
        }

        public void SetTarget(Actor target)
        {
            if (CurrentTarget == target) return;
            CurrentTarget = target;
            TargetChanged?.Invoke(target);
        }

        // --- abilities ---------------------------------------------------------------

        private void HandleAbilities(Actor player)
        {
            IReadOnlyList<AbilityDefinition> abilities = player.Abilities.Abilities;

            for (int i = 0; i < AbilityKeys.Length && i < abilities.Count; i++)
            {
                if (!Input.GetKeyDown(AbilityKeys[i])) continue;
                Cast(player, abilities[i]);
                return;
            }

            // Space is the basic attack, so a player who has spent everything
            // still has something to press.
            if (Input.GetKeyDown(KeyCode.Space)) Cast(player, player.Abilities.BasicAttack);
        }

        private void Cast(Actor player, AbilityDefinition ability)
        {
            if (ability == null) return;

            Actor target = ability.TargetKind switch
            {
                TargetKind.Self => player,
                TargetKind.Ally => PickAllyTarget(player),
                _ => CurrentTarget ?? player.World.Actors.NearestEnemyOf(player, ability.Range)
            };

            GridCoord cell = ability.TargetKind == TargetKind.Ground
                ? MouseCell()
                : target != null ? target.Cell : MouseCell();

            LastRefusal = player.Abilities.TryCast(ability, target, cell, player.World.Clock.Tick);
        }

        /// <summary>
        /// Friendly abilities default to the most hurt party member. It is what
        /// the player almost always wants, and it keeps healing playable without
        /// a click-to-select-ally step.
        /// </summary>
        private Actor PickAllyTarget(Actor player)
        {
            Actor lowest = player.World.Actors.LowestHealthAlly(player, 14);
            return lowest != null ? lowest : player;
        }

        // --- movement -----------------------------------------------------------------

        private void HandleMovement(Actor player)
        {
            _moveCooldown -= Time.deltaTime;

            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            if (Mathf.Abs(h) < 0.1f && Mathf.Abs(v) < 0.1f) return;
            if (_moveCooldown > 0f) return;

            _moveCooldown = MoveRepeatInterval;

            // Keys are screen-relative: pressing up walks up the screen, whatever
            // that means in grid terms. Anything else feels wrong on an iso grid.
            IsoDirection dir = IsoDirectionExtensions.FromWorldVector(new Vector2(h, v), player.Facing);
            GridCoord step = GridCoord.Step(dir);
            GridCoord destination = player.Cell + step;

            if (!player.World.Map.IsWalkable(destination)) return;

            _path.Clear();
            _path.Add(destination);
            player.Motor.SetPath(_path);
        }

        private void IssueMoveOrder(Actor player, GridCoord destination)
        {
            if (!player.World.Map.IsWalkable(destination)) return;

            if (player.World.Pathfinder.TryFindPath(player.World.Map, player.Cell, destination, _path))
                player.Motor.SetPath(_path);
        }

        public GridCoord MouseCell()
        {
            if (WorldCamera == null) return Player != null ? Player.Cell : GridCoord.Zero;

            Vector3 world = WorldCamera.ScreenToWorldPoint(Input.mousePosition);
            world.z = 0f;
            return IsoGrid.WorldToCell(world);
        }

        // --- selection ring -------------------------------------------------------------

        private void UpdateTargetRing()
        {
            if (CurrentTarget == null || !CurrentTarget.IsAlive)
            {
                if (_targetRing != null) _targetRing.SetActive(false);
                return;
            }

            if (_targetRing == null)
            {
                _targetRing = new GameObject("TargetRing");
                var sr = _targetRing.AddComponent<SpriteRenderer>();
                sr.sprite = PrimitiveSprites.CellDiamondOutline;
                sr.color = new Color(1f, 0.85f, 0.3f, 0.9f);
                sr.sortingLayerName = SortingLayers.GroundDecal;
                sr.sortingOrder = 20;
            }

            _targetRing.SetActive(true);
            _targetRing.transform.position = CurrentTarget.transform.position;
        }
    }
}
