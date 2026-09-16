using System.Collections.Generic;
using EmberDepths.Content;
using EmberDepths.Core.Grid;
using EmberDepths.Core.Sim;
using EmberDepths.Gameplay.Actors;
using EmberDepths.Gameplay.Presentation;
using EmberDepths.Gameplay.World;
using UnityEngine;

namespace EmberDepths.Gameplay.Combat
{
    /// <summary>A danger zone currently being shown to the player.</summary>
    public sealed class Telegraph
    {
        public Actor Owner;
        public int StartTick;
        public int EndTick;
        public Color Colour;
        public readonly List<GridCoord> Cells = new List<GridCoord>(32);
        public GameObject View;
        public SpriteRenderer[] Fills;
    }

    /// <summary>
    /// Draws where an enemy ability is about to land, and how long the party has
    /// to get out.
    ///
    /// This is the single most important fairness system in the game. An
    /// untelegraphed area effect is indistinguishable from a bug: the player sees
    /// health vanish with no cause. Every enemy ability with a shape should carry
    /// a lead time, and the boss's biggest hits should carry the longest.
    ///
    /// The warning fills from nothing to full over the lead time, so the player
    /// reads "how long do I have" from the fill rather than from a number.
    /// </summary>
    public sealed class TelegraphSystem
    {
        private readonly DungeonWorld _world;
        private readonly List<Telegraph> _active = new List<Telegraph>(16);
        private readonly List<GridCoord> _scratch = new List<GridCoord>(64);

        private Transform _root;

        public IReadOnlyList<Telegraph> Active => _active;

        /// <summary>Warning colour. Sampled from the biome's hot end so it reads as heat.</summary>
        public Color DangerColour = new Color(1f, 0.32f, 0.10f, 0.55f);

        public TelegraphSystem(DungeonWorld world)
        {
            _world = world;
        }

        private Transform Root
        {
            get
            {
                if (_root != null) return _root;
                var go = new GameObject("Telegraphs");
                go.transform.SetParent(_world.SceneRoot, false);
                _root = go.transform;
                return _root;
            }
        }

        public Telegraph Show(Actor owner, TargetShape shape, GridCoord origin, GridCoord target, float leadSeconds)
        {
            shape.CollectCells(origin, target, _scratch);
            if (_scratch.Count == 0 && shape.Kind != ShapeKind.Global) return null;

            var tele = new Telegraph
            {
                Owner = owner,
                StartTick = _world.Clock.Tick,
                EndTick = _world.Clock.Tick + SimClock.SecondsToTicks(Mathf.Max(0.05f, leadSeconds)),
                Colour = DangerColour
            };

            for (int i = 0; i < _scratch.Count; i++)
            {
                // Warnings are drawn only on ground the party could actually be
                // standing on. Painting them over walls makes the shape unreadable.
                if (!_world.Map.IsWalkable(_scratch[i])) continue;
                tele.Cells.Add(_scratch[i]);
            }

            if (tele.Cells.Count == 0) return null;

            BuildView(tele);
            _active.Add(tele);
            return tele;
        }

        public void CancelFor(Actor owner)
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                if (_active[i].Owner != owner) continue;
                Destroy(_active[i]);
                _active.RemoveAt(i);
            }
        }

        public void Tick(int tick)
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                Telegraph t = _active[i];

                if (tick >= t.EndTick)
                {
                    Destroy(t);
                    _active.RemoveAt(i);
                    continue;
                }

                int span = Mathf.Max(1, t.EndTick - t.StartTick);
                float progress = Mathf.Clamp01((tick - t.StartTick) / (float)span);

                // Alpha ramps up, and the last 20% pulses, so "about to land" is
                // legible at a glance even in a room full of fire.
                float alpha = Mathf.Lerp(0.18f, 0.62f, progress);
                if (progress > 0.8f)
                    alpha *= 0.7f + 0.3f * Mathf.Abs(Mathf.Sin(progress * 40f));

                var c = t.Colour;
                c.a = alpha;

                for (int f = 0; f < t.Fills.Length; f++)
                    if (t.Fills[f] != null) t.Fills[f].color = c;
            }
        }

        /// <summary>True if the cell is inside any warning right now. The AI dodges on this.</summary>
        public bool IsThreatened(GridCoord cell)
        {
            for (int i = 0; i < _active.Count; i++)
                if (_active[i].Cells.Contains(cell)) return true;
            return false;
        }

        private void BuildView(Telegraph tele)
        {
            var go = new GameObject("Telegraph");
            go.transform.SetParent(Root, false);
            tele.View = go;
            tele.Fills = new SpriteRenderer[tele.Cells.Count];

            for (int i = 0; i < tele.Cells.Count; i++)
            {
                var cellGo = new GameObject("cell");
                cellGo.transform.SetParent(go.transform, false);
                cellGo.transform.position = IsoGrid.CellToWorld(tele.Cells[i]);

                var sr = cellGo.AddComponent<SpriteRenderer>();
                sr.sprite = PrimitiveSprites.CellDiamond;
                sr.color = tele.Colour;
                sr.sortingLayerName = SortingLayers.GroundDecal;
                // Above hazard decals: a warning must never be hidden by the pool
                // that is already burning on the same tile.
                sr.sortingOrder = 10;
                tele.Fills[i] = sr;
            }
        }

        private static void Destroy(Telegraph tele)
        {
            ViewObjects.Destroy(tele.View);
        }

        public void Clear()
        {
            for (int i = 0; i < _active.Count; i++) Destroy(_active[i]);
            _active.Clear();
        }
    }
}
