using System.Collections.Generic;
using EmberDepths.Content;
using EmberDepths.Core.Grid;
using EmberDepths.Gameplay.Actors;

namespace EmberDepths.Gameplay.World
{
    /// <summary>
    /// Every actor currently in the dungeon, indexed the three ways the rest of
    /// the game asks for them: by id (occupancy lookups), by faction (targeting),
    /// and as one flat list (the tick loop).
    ///
    /// The faction lists are maintained rather than filtered on demand because
    /// targeting queries run several times per tick per actor.
    /// </summary>
    public sealed class ActorRegistry
    {
        private readonly List<Actor> _all = new List<Actor>(64);
        private readonly List<Actor> _party = new List<Actor>(8);
        private readonly List<Actor> _hostiles = new List<Actor>(48);
        private readonly Dictionary<int, Actor> _byId = new Dictionary<int, Actor>(64);

        private int _nextId = 1;

        public IReadOnlyList<Actor> All => _all;
        public IReadOnlyList<Actor> Party => _party;
        public IReadOnlyList<Actor> Hostiles => _hostiles;

        public int AllocateId() => _nextId++;

        public void Register(Actor actor)
        {
            if (actor == null || _byId.ContainsKey(actor.Id)) return;

            _all.Add(actor);
            _byId[actor.Id] = actor;

            if (actor.Faction == Faction.Party) _party.Add(actor);
            else if (actor.Faction == Faction.Hostile) _hostiles.Add(actor);
        }

        public void Unregister(Actor actor)
        {
            if (actor == null) return;

            _all.Remove(actor);
            _party.Remove(actor);
            _hostiles.Remove(actor);
            _byId.Remove(actor.Id);

            // Drop the departing actor from every threat table, or dead actors
            // keep holding aggro and enemies stand around attacking nothing.
            for (int i = 0; i < _hostiles.Count; i++)
                _hostiles[i].Threat?.Remove(actor);
        }

        public Actor ById(int id) => _byId.TryGetValue(id, out Actor a) ? a : null;

        public Actor AtCell(GridCoord cell, GridMap map)
        {
            return map.TryGetOccupant(cell, out int id) ? ById(id) : null;
        }

        public bool AnyPartyAlive()
        {
            for (int i = 0; i < _party.Count; i++)
                if (_party[i].IsAlive) return true;
            return false;
        }

        public bool AnyHostileAlive()
        {
            for (int i = 0; i < _hostiles.Count; i++)
                if (_hostiles[i].IsAlive) return true;
            return false;
        }

        /// <summary>Closest living actor of the opposing faction, or null beyond <paramref name="maxRange"/>.</summary>
        public Actor NearestEnemyOf(Actor actor, int maxRange)
        {
            IReadOnlyList<Actor> candidates = actor.Faction == Faction.Party ? _hostiles : _party;

            Actor best = null;
            int bestDistance = int.MaxValue;

            for (int i = 0; i < candidates.Count; i++)
            {
                Actor c = candidates[i];
                if (c == null || !c.IsAlive || c.Stats.Untargetable) continue;

                int d = GridCoord.Chebyshev(actor.Cell, c.Cell);
                if (d > maxRange || d >= bestDistance) continue;

                best = c;
                bestDistance = d;
            }

            return best;
        }

        /// <summary>Living ally with the lowest health fraction. The healer's target picker.</summary>
        public Actor LowestHealthAlly(Actor actor, int maxRange, float belowFraction = 1f)
        {
            IReadOnlyList<Actor> candidates = actor.Faction == Faction.Party ? _party : _hostiles;

            Actor best = null;
            float bestFraction = belowFraction;

            for (int i = 0; i < candidates.Count; i++)
            {
                Actor c = candidates[i];
                if (c == null || !c.IsAlive) continue;
                if (GridCoord.Chebyshev(actor.Cell, c.Cell) > maxRange) continue;
                if (c.HealthFraction >= bestFraction) continue;

                best = c;
                bestFraction = c.HealthFraction;
            }

            return best;
        }

        /// <summary>The living boss, if one is present. Drives the boss health bar.</summary>
        public Actor FindBoss()
        {
            for (int i = 0; i < _hostiles.Count; i++)
                if (_hostiles[i].IsAlive && _hostiles[i].Rank == EnemyRank.Boss) return _hostiles[i];
            return null;
        }

        public void Clear()
        {
            _all.Clear();
            _party.Clear();
            _hostiles.Clear();
            _byId.Clear();
            _nextId = 1;
        }
    }
}
