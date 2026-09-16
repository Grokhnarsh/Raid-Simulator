using System;
using System.Collections.Generic;
using RaidSim.Core.Entities;
using RaidSim.Core.Mathematics;

namespace RaidSim.Core.Targeting
{
    /// <summary>
    /// Runs target searches against the entity registry.
    /// </summary>
    /// <remarks>
    /// <para>A query is a <see cref="TargetFilter"/> (who is legal) plus a score function (who is
    /// best). Every rule the design calls for — highest threat, lowest health, "the healer", a random
    /// raider, everyone inside an area — is one of those two parts, never a new code path. When the
    /// AI becomes data-driven in Phase 5 the authored profile simply names a scorer.</para>
    /// <para>The instance owns a scratch buffer so per-tick queries do not allocate. It is therefore
    /// not reentrant and not thread safe, which matches the single-threaded tick loop.</para>
    /// </remarks>
    public sealed class TargetQuery
    {
        private readonly EntityRegistry _registry;
        private readonly List<ISimEntity> _buffer = new List<ISimEntity>(32);

        public TargetQuery(EntityRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        /// <summary>
        /// Appends every entity matching <paramref name="filter"/> into <paramref name="results"/>.
        /// </summary>
        public void Collect(ISimEntity observer, in TargetFilter filter, List<ISimEntity> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            results.Clear();
            IReadOnlyList<ISimEntity> all = _registry.All;
            for (int i = 0; i < all.Count; i++)
            {
                if (filter.Matches(observer, all[i]))
                {
                    results.Add(all[i]);
                }
            }
        }

        /// <summary>
        /// Returns the matching entity with the highest score, or null when nothing matches.
        /// </summary>
        /// <param name="score">
        /// Ranks candidates; higher wins. Ties resolve to the lowest entity id so repeated queries
        /// are stable — an AI that flickers between two equally valid targets looks broken.
        /// </param>
        public ISimEntity SelectBest(ISimEntity observer, in TargetFilter filter, Func<ISimEntity, float> score)
        {
            if (score == null)
            {
                throw new ArgumentNullException(nameof(score));
            }

            Collect(observer, filter, _buffer);

            ISimEntity best = null;
            float bestScore = float.NegativeInfinity;
            for (int i = 0; i < _buffer.Count; i++)
            {
                ISimEntity candidate = _buffer[i];
                float value = score(candidate);
                if (value > bestScore || (SimMath.Approximately(value, bestScore) && best != null && candidate.Id.Value < best.Id.Value))
                {
                    best = candidate;
                    bestScore = value;
                }
            }

            _buffer.Clear();
            return best;
        }

        /// <summary>Nearest matching entity, measured between footprints.</summary>
        public ISimEntity SelectNearest(ISimEntity observer, in TargetFilter filter)
        {
            if (observer == null)
            {
                throw new ArgumentNullException(nameof(observer));
            }

            return SelectBest(observer, filter, candidate => -TargetFilter.EdgeDistance(observer, candidate));
        }

        /// <summary>
        /// Nearest matching entity that also lies inside a cone of <paramref name="halfAngleDegrees"/>
        /// around the observer's facing. This is what click-free "target what I am looking at" uses,
        /// and later what cone abilities use to find their victims.
        /// </summary>
        public ISimEntity SelectNearestInCone(ISimEntity observer, in TargetFilter filter, float halfAngleDegrees)
        {
            if (observer == null)
            {
                throw new ArgumentNullException(nameof(observer));
            }

            Vec3 facing = observer.Facing.Flat.Normalized;
            if (facing == Vec3.Zero)
            {
                return SelectNearest(observer, filter);
            }

            ISimEntity observerRef = observer;
            return SelectBest(observer, filter, candidate =>
            {
                Vec3 toCandidate = (candidate.Position - observerRef.Position).Flat;
                if (toCandidate == Vec3.Zero)
                {
                    return 0f;
                }

                float angle = SimMath.AngleBetween(facing, toCandidate);
                if (angle > halfAngleDegrees)
                {
                    return float.NegativeInfinity;
                }

                return -TargetFilter.EdgeDistance(observerRef, candidate);
            });
        }

        /// <summary>
        /// Cycles to the next matching entity after <paramref name="current"/>, ordered by distance.
        /// Wraps around, and starts from the nearest when <paramref name="current"/> no longer matches.
        /// </summary>
        /// <remarks>
        /// This backs tab-targeting. Ordering by distance rather than registration order means the
        /// cycle stays predictable as adds spawn and die mid-pull.
        /// </remarks>
        public ISimEntity SelectNextCycling(ISimEntity observer, in TargetFilter filter, ISimEntity current)
        {
            if (observer == null)
            {
                throw new ArgumentNullException(nameof(observer));
            }

            var ordered = new List<ISimEntity>();
            Collect(observer, filter, ordered);
            if (ordered.Count == 0)
            {
                return null;
            }

            ISimEntity observerRef = observer;
            ordered.Sort((a, b) =>
            {
                int byDistance = TargetFilter.EdgeDistance(observerRef, a)
                    .CompareTo(TargetFilter.EdgeDistance(observerRef, b));
                return byDistance != 0 ? byDistance : a.Id.CompareTo(b.Id);
            });

            if (current == null)
            {
                return ordered[0];
            }

            int index = ordered.FindIndex(entity => entity.Id == current.Id);
            if (index < 0)
            {
                return ordered[0];
            }

            return ordered[(index + 1) % ordered.Count];
        }
    }
}
