using System.Collections.Generic;
using EmberDepths.Core.Grid;
using EmberDepths.Core.Sim;
using UnityEngine;

namespace EmberDepths.Gameplay.Actors
{
    /// <summary>
    /// Moves an actor from cell to cell along a path.
    ///
    /// Movement advances on simulation ticks, not frames. Between ticks the actor
    /// sits at a fractional position that <see cref="ActorView"/> interpolates, so
    /// a 20 Hz simulation still reads as smooth walking. The actor "owns" a cell
    /// the moment it starts stepping into it, which prevents two actors from
    /// converging on the same tile and ending up stacked.
    /// </summary>
    public sealed class ActorMotor
    {
        private readonly Actor _owner;
        private readonly List<GridCoord> _path = new List<GridCoord>(32);

        private GridCoord _from;
        private GridCoord _to;
        private float _t;
        private int _pathIndex;

        public bool IsMoving { get; private set; }

        /// <summary>Where the actor is right now, in fractional cell coordinates.</summary>
        public Vector2 FractionalCell =>
            IsMoving
                ? Vector2.Lerp(new Vector2(_from.X, _from.Y), new Vector2(_to.X, _to.Y), _t)
                : new Vector2(_owner.Cell.X, _owner.Cell.Y);

        /// <summary>The cell the actor is currently stepping into, or its current cell when still.</summary>
        public GridCoord TargetCell => IsMoving ? _to : _owner.Cell;

        public int RemainingSteps => IsMoving ? _path.Count - _pathIndex + 1 : 0;

        public ActorMotor(Actor owner)
        {
            _owner = owner;
            _from = _to = owner.Cell;
        }

        /// <summary>Replaces the current path. Does not interrupt the step in progress.</summary>
        public void SetPath(List<GridCoord> path)
        {
            _path.Clear();
            if (path != null) _path.AddRange(path);
            _pathIndex = 0;

            // Finish the current step first, then pick up the new path. Cancelling
            // mid-step would teleport the actor back onto its previous cell.
            if (!IsMoving) BeginNextStep();
        }

        public void Stop()
        {
            _path.Clear();
            _pathIndex = 0;
        }

        /// <summary>Cancels everything, including the step in progress. Used by stuns and knockback.</summary>
        public void HardStop()
        {
            Stop();
            if (IsMoving)
            {
                // Snap to whichever end of the step is closer, so the actor never
                // ends up standing between two cells.
                GridCoord land = _t >= 0.5f ? _to : _from;
                _owner.TeleportTo(land);
            }
            IsMoving = false;
            _t = 0f;
        }

        public void Tick()
        {
            if (!_owner.IsAlive) { HardStop(); return; }

            if (!_owner.Stats.CanMove)
            {
                // Rooted or stunned: hold position but keep the queued path so the
                // actor resumes where it left off once control returns.
                return;
            }

            if (!IsMoving && !BeginNextStep()) return;

            float speed = Mathf.Max(0.01f, _owner.Stats.MoveSpeed);
            bool diagonal = _from.X != _to.X && _from.Y != _to.Y;
            float stepLength = diagonal ? 1.41421356f : 1f;

            _t += speed * SimClock.TickDuration / stepLength;

            if (_t < 1f) return;

            _t = 0f;
            IsMoving = false;
            _owner.ArriveAt(_to);
            BeginNextStep();
        }

        private bool BeginNextStep()
        {
            if (_pathIndex >= _path.Count)
            {
                _path.Clear();
                _pathIndex = 0;
                return false;
            }

            GridCoord next = _path[_pathIndex++];

            // The world moves between the moment a path is built and the moment it
            // is walked. Re-check, and abandon the path rather than walking into a
            // wall or through another actor.
            if (!_owner.World.Map.IsWalkable(next) || !_owner.World.Map.TryOccupy(next, _owner.Id))
            {
                Stop();
                return false;
            }

            _from = _owner.Cell;
            _to = next;
            _t = 0f;
            IsMoving = true;
            _owner.SetFacing(IsoDirectionExtensions.FromGridStep(_from, _to, _owner.Facing));
            return true;
        }

        /// <summary>Called by the owner after a teleport so the motor's endpoints stay consistent.</summary>
        public void ResetTo(GridCoord cell)
        {
            _from = _to = cell;
            _t = 0f;
            IsMoving = false;
        }
    }
}
