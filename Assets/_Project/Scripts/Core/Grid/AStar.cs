using System.Collections.Generic;
using UnityEngine;

namespace EmberDepths.Core.Grid
{
    /// <summary>
    /// A* over <see cref="GridMap"/> with eight-way movement.
    ///
    /// One instance per consumer, reused across searches — the working buffers are
    /// kept alive on purpose so that twenty enemies re-pathing on the same
    /// simulation tick do not generate garbage.
    /// </summary>
    public sealed class AStar
    {
        private const float DiagonalCost = 1.41421356f;

        private readonly Dictionary<GridCoord, float> _gScore = new Dictionary<GridCoord, float>(512);
        private readonly Dictionary<GridCoord, GridCoord> _cameFrom = new Dictionary<GridCoord, GridCoord>(512);
        private readonly HashSet<GridCoord> _closed = new HashSet<GridCoord>();
        private readonly MinHeap _open = new MinHeap(512);
        private readonly List<GridCoord> _scratch = new List<GridCoord>(128);

        /// <summary>Safety valve so a pathological map cannot stall a frame.</summary>
        public int MaxExpandedNodes = 4000;

        /// <summary>
        /// Finds a path from <paramref name="start"/> to <paramref name="goal"/>.
        /// The returned list excludes <paramref name="start"/> and ends at the
        /// reached cell.
        /// </summary>
        /// <param name="stopAdjacent">
        /// When true the search succeeds as soon as it touches a cell adjacent to
        /// the goal. This is how melee attackers path to an occupied cell: the goal
        /// itself is never free, so requiring arrival would always fail.
        /// </param>
        /// <param name="occupancyBlocks">
        /// When true, cells held by another actor are treated as impassable. Enemies
        /// use false with a small penalty instead, so a crowded corridor degrades
        /// into shuffling rather than "no path found".
        /// </param>
        public bool TryFindPath(
            GridMap map,
            GridCoord start,
            GridCoord goal,
            List<GridCoord> outPath,
            bool stopAdjacent = false,
            bool occupancyBlocks = false)
        {
            outPath.Clear();
            if (map == null) return false;
            if (start == goal) return true;

            _gScore.Clear();
            _cameFrom.Clear();
            _closed.Clear();
            _open.Clear();

            _gScore[start] = 0f;
            _open.Push(start, Heuristic(start, goal));

            int expanded = 0;
            bool found = false;
            GridCoord reached = start;

            while (_open.Count > 0 && expanded < MaxExpandedNodes)
            {
                GridCoord current = _open.Pop();
                if (!_closed.Add(current)) continue;
                expanded++;

                if (current == goal || (stopAdjacent && current.IsAdjacentTo(goal)))
                {
                    reached = current;
                    found = true;
                    break;
                }

                float currentG = _gScore[current];

                for (int i = 0; i < GridCoord.AllNeighbours.Length; i++)
                {
                    GridCoord step = GridCoord.AllNeighbours[i];
                    GridCoord next = current + step;

                    if (_closed.Contains(next)) continue;
                    if (!map.IsWalkable(next)) continue;

                    bool diagonal = step.X != 0 && step.Y != 0;
                    if (diagonal && !CanCutCorner(map, current, step)) continue;

                    // The goal cell is allowed to be occupied — that is the target.
                    if (occupancyBlocks && next != goal && map.IsOccupied(next)) continue;

                    float stepCost = map.CostToEnter(next) * (diagonal ? DiagonalCost : 1f);
                    if (!occupancyBlocks && map.IsOccupied(next)) stepCost += 4f;

                    float tentative = currentG + stepCost;
                    if (_gScore.TryGetValue(next, out float known) && tentative >= known) continue;

                    _gScore[next] = tentative;
                    _cameFrom[next] = current;
                    _open.Push(next, tentative + Heuristic(next, goal));
                }
            }

            if (!found) return false;

            // Walk the parent chain back and reverse in place.
            _scratch.Clear();
            GridCoord node = reached;
            while (node != start)
            {
                _scratch.Add(node);
                if (!_cameFrom.TryGetValue(node, out node)) return false;
            }

            for (int i = _scratch.Count - 1; i >= 0; i--) outPath.Add(_scratch[i]);
            return true;
        }

        /// <summary>
        /// Refuses diagonal moves that would clip the corner of a wall. Without this
        /// actors visibly slide through solid basalt at room corners.
        /// </summary>
        private static bool CanCutCorner(GridMap map, GridCoord from, GridCoord step)
        {
            return map.IsWalkable(new GridCoord(from.X + step.X, from.Y))
                && map.IsWalkable(new GridCoord(from.X, from.Y + step.Y));
        }

        /// <summary>Octile distance — admissible for eight-way movement.</summary>
        private static float Heuristic(GridCoord a, GridCoord b)
        {
            int dx = Mathf.Abs(a.X - b.X);
            int dy = Mathf.Abs(a.Y - b.Y);
            int min = Mathf.Min(dx, dy);
            return (dx + dy) - (2f - DiagonalCost) * min;
        }

        /// <summary>Binary min-heap keyed on f-score. Duplicates are tolerated and skipped on pop.</summary>
        private sealed class MinHeap
        {
            private GridCoord[] _items;
            private float[] _priorities;
            public int Count { get; private set; }

            public MinHeap(int capacity)
            {
                _items = new GridCoord[capacity];
                _priorities = new float[capacity];
            }

            public void Clear() => Count = 0;

            public void Push(GridCoord item, float priority)
            {
                if (Count == _items.Length) Grow();

                int i = Count++;
                _items[i] = item;
                _priorities[i] = priority;

                while (i > 0)
                {
                    int parent = (i - 1) >> 1;
                    if (_priorities[parent] <= _priorities[i]) break;
                    Swap(parent, i);
                    i = parent;
                }
            }

            public GridCoord Pop()
            {
                GridCoord result = _items[0];
                Count--;
                if (Count > 0)
                {
                    _items[0] = _items[Count];
                    _priorities[0] = _priorities[Count];

                    int i = 0;
                    while (true)
                    {
                        int l = i * 2 + 1, r = l + 1, smallest = i;
                        if (l < Count && _priorities[l] < _priorities[smallest]) smallest = l;
                        if (r < Count && _priorities[r] < _priorities[smallest]) smallest = r;
                        if (smallest == i) break;
                        Swap(i, smallest);
                        i = smallest;
                    }
                }

                return result;
            }

            private void Grow()
            {
                int n = _items.Length * 2;
                System.Array.Resize(ref _items, n);
                System.Array.Resize(ref _priorities, n);
            }

            private void Swap(int a, int b)
            {
                (_items[a], _items[b]) = (_items[b], _items[a]);
                (_priorities[a], _priorities[b]) = (_priorities[b], _priorities[a]);
            }
        }
    }
}
