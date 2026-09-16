using System.Collections.Generic;
using EmberDepths.Content;
using EmberDepths.Core.Grid;
using EmberDepths.Core.Sim;
using EmberDepths.Gameplay.Actors;
using EmberDepths.Gameplay.Presentation;
using UnityEngine;

namespace EmberDepths.Gameplay.World
{
    /// <summary>One live patch of dangerous ground.</summary>
    public sealed class HazardInstance
    {
        public HazardDefinition Definition;
        public GridCoord Centre;
        public int Radius;
        public int ExpiresAtTick;
        public int NextDamageTick;

        /// <summary>Who dropped it. Null for terrain hazards that belong to the biome.</summary>
        public Actor Owner;

        public Faction OwnerFaction = Faction.Neutral;

        public readonly List<GridCoord> Cells = new List<GridCoord>(16);
        public GameObject View;

        /// <summary>Terrain hazards never expire; ability-spawned ones do.</summary>
        public bool Permanent;
    }

    /// <summary>
    /// Every persistent ground effect in the dungeon.
    ///
    /// Hazards are the volcano's primary way of talking to the party: they say
    /// "not here, not any more". For that to be fair they must be visible, they
    /// must tick predictably, and the AI must respect them — all three live here.
    ///
    /// The field also feeds a pathing surcharge back into <see cref="GridMap"/>,
    /// so A* routes companions and enemies around fire without any of them
    /// needing hazard-specific code.
    /// </summary>
    public sealed class HazardField
    {
        private readonly DungeonWorld _world;
        private readonly List<HazardInstance> _hazards = new List<HazardInstance>(32);
        private readonly Dictionary<GridCoord, float> _penalty = new Dictionary<GridCoord, float>(256);
        private readonly List<HazardInstance> _expired = new List<HazardInstance>(8);

        private Transform _root;

        public IReadOnlyList<HazardInstance> All => _hazards;

        public HazardField(DungeonWorld world)
        {
            _world = world;
            _world.Map.ExtraCostProvider = PenaltyAt;
        }

        private Transform Root
        {
            get
            {
                if (_root != null) return _root;
                var go = new GameObject("Hazards");
                go.transform.SetParent(_world.SceneRoot, false);
                _root = go.transform;
                return _root;
            }
        }

        public float PenaltyAt(GridCoord cell) =>
            _penalty.TryGetValue(cell, out float v) ? v : 0f;

        public bool IsDangerous(GridCoord cell) => _penalty.ContainsKey(cell);

        /// <summary>True if this specific actor would actually be hurt standing here.</summary>
        public bool IsDangerousFor(Actor actor, GridCoord cell)
        {
            if (actor == null) return IsDangerous(cell);

            for (int i = 0; i < _hazards.Count; i++)
            {
                HazardInstance h = _hazards[i];
                if (!h.Cells.Contains(cell)) continue;
                if (!AffectsActor(h, actor)) continue;
                return true;
            }
            return false;
        }

        public HazardInstance Spawn(
            HazardDefinition definition,
            GridCoord centre,
            int radius,
            float durationSeconds,
            Actor owner)
        {
            if (definition == null) return null;

            int tick = _world.Clock.Tick;
            bool permanent = durationSeconds <= 0f;

            if (definition.MergeWithSameType)
            {
                HazardInstance existing = FindMergeable(definition, centre, radius);
                if (existing != null)
                {
                    // Refresh rather than stack. Two overlapping pools that both
                    // tick would roughly double the damage the player was shown.
                    if (!permanent)
                        existing.ExpiresAtTick = Mathf.Max(existing.ExpiresAtTick, tick + SimClock.SecondsToTicks(durationSeconds));
                    return existing;
                }
            }

            var hazard = new HazardInstance
            {
                Definition = definition,
                Centre = centre,
                Radius = Mathf.Max(0, radius),
                Owner = owner,
                OwnerFaction = owner != null ? owner.Faction : Faction.Neutral,
                Permanent = permanent,
                ExpiresAtTick = permanent ? int.MaxValue : tick + SimClock.SecondsToTicks(durationSeconds),
                NextDamageTick = tick + SimClock.SecondsToTicks(definition.TickInterval)
            };

            CollectCells(centre, hazard.Radius, hazard.Cells);
            _hazards.Add(hazard);
            ApplyPenalty(hazard, +1);
            BuildView(hazard);

            if (!string.IsNullOrEmpty(definition.SpawnVfxKey))
                _world.PlayVfx(definition.SpawnVfxKey, centre);

            return hazard;
        }

        private HazardInstance FindMergeable(HazardDefinition def, GridCoord centre, int radius)
        {
            for (int i = 0; i < _hazards.Count; i++)
            {
                HazardInstance h = _hazards[i];
                if (h.Definition != def) continue;
                if (h.Radius != radius) continue;
                if (GridCoord.Chebyshev(h.Centre, centre) > 1) continue;
                return h;
            }
            return null;
        }

        private void CollectCells(GridCoord centre, int radius, List<GridCoord> outCells)
        {
            outCells.Clear();
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) > radius) continue;
                    var c = new GridCoord(centre.X + dx, centre.Y + dy);
                    if (!_world.Map.IsWalkable(c)) continue;
                    outCells.Add(c);
                }
            }
        }

        private void ApplyPenalty(HazardInstance hazard, int sign)
        {
            float delta = hazard.Definition.PathingPenalty * sign;
            for (int i = 0; i < hazard.Cells.Count; i++)
            {
                GridCoord c = hazard.Cells[i];
                _penalty.TryGetValue(c, out float v);
                v += delta;
                if (v <= 0.01f) _penalty.Remove(c);
                else _penalty[c] = v;
            }
        }

        public void Tick(int tick)
        {
            if (_hazards.Count == 0) return;

            _expired.Clear();

            for (int i = 0; i < _hazards.Count; i++)
            {
                HazardInstance h = _hazards[i];

                if (!h.Permanent && tick >= h.ExpiresAtTick)
                {
                    _expired.Add(h);
                    continue;
                }

                if (tick < h.NextDamageTick) continue;
                h.NextDamageTick = tick + SimClock.SecondsToTicks(h.Definition.TickInterval);
                DamageOccupants(h);
            }

            for (int i = 0; i < _expired.Count; i++) Despawn(_expired[i]);
        }

        private void DamageOccupants(HazardInstance hazard)
        {
            HazardDefinition def = hazard.Definition;

            for (int i = 0; i < hazard.Cells.Count; i++)
            {
                Actor actor = _world.Actors.AtCell(hazard.Cells[i], _world.Map);
                if (actor == null || !actor.IsAlive) continue;
                if (!AffectsActor(hazard, actor)) continue;

                // Hazard damage is authored as a final number, so it bypasses the
                // caster's Power entirely; the owner is only carried along so the
                // damage still credits threat to whoever dropped the pool.
                _world.Combat.DealFlatDamage(
                    hazard.Owner, actor, def.DamageFor(actor.MaxHealth), def.DamageType);

                if (def.AppliesStatus != null && actor.IsAlive)
                {
                    actor.Status.Apply(
                        def.AppliesStatus, def.StatusDuration, 1,
                        hazard.Owner,
                        hazard.Owner != null ? hazard.Owner.Stats.Power : 1f,
                        _world.Clock.Tick);
                }
            }
        }

        private static bool AffectsActor(HazardInstance hazard, Actor actor)
        {
            HazardDefinition def = hazard.Definition;

            if (def.LavaWalkersImmune && (actor.Flags & ActorFlags.LavaWalker) != 0) return false;

            if (!def.FriendlyFire && hazard.OwnerFaction != Faction.Neutral && actor.Faction == hazard.OwnerFaction)
                return false;

            return true;
        }

        public void Despawn(HazardInstance hazard)
        {
            if (hazard == null || !_hazards.Remove(hazard)) return;

            ApplyPenalty(hazard, -1);
            ViewObjects.Destroy(hazard.View);
        }

        /// <summary>Hook for the moment an actor steps onto a new cell. Currently presentation only.</summary>
        public void OnActorEnteredCell(Actor actor, GridCoord cell)
        {
            // Damage is applied on the hazard's own cadence rather than on entry,
            // so that walking across a pool costs a predictable amount rather than
            // punishing whoever happens to step on a tick boundary.
        }

        private void BuildView(HazardInstance hazard)
        {
            var go = new GameObject($"Hazard_{hazard.Definition.name}");
            go.transform.SetParent(Root, false);
            hazard.View = go;

            Sprite sprite = hazard.Definition.Decal != null ? hazard.Definition.Decal : PrimitiveSprites.CellDiamond;

            for (int i = 0; i < hazard.Cells.Count; i++)
            {
                var cellGo = new GameObject("cell");
                cellGo.transform.SetParent(go.transform, false);
                cellGo.transform.position = IsoGrid.CellToWorld(hazard.Cells[i]);

                var sr = cellGo.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.color = hazard.Definition.Tint;
                sr.sortingLayerName = SortingLayers.GroundDecal;
                sr.sortingOrder = 0;
            }
        }

        public void Clear()
        {
            for (int i = 0; i < _hazards.Count; i++)
                ViewObjects.Destroy(_hazards[i].View);

            _hazards.Clear();
            _penalty.Clear();
        }
    }
}
