using RaidSim.Core.Common;
using RaidSim.Core.Entities;
using RaidSim.Core.Mathematics;

namespace RaidSim.Core.Targeting
{
    /// <summary>Which allegiances a query accepts.</summary>
    public enum TargetAllegiance
    {
        /// <summary>Anything, regardless of faction.</summary>
        Any = 0,

        /// <summary>Only entities hostile to the observer.</summary>
        Hostile = 1,

        /// <summary>Only the observer's own faction.</summary>
        Friendly = 2,
    }

    /// <summary>
    /// The conditions a candidate must satisfy to be a legal target.
    /// </summary>
    /// <remarks>
    /// <para>Every targeting decision in the project — a player clicking an enemy, an ability
    /// validating its target, a healer looking for someone to save, a boss picking a random raider
    /// for a mechanic — is the same query with a different filter. Keeping that in one struct is
    /// what stops "find a target" logic from being reinvented per ability.</para>
    /// <para>Ranges are measured between footprints, so a boss with a four-metre radius is in melee
    /// range from further away than a trash mob. Heights are compared separately: a target on a
    /// balcony directly overhead is horizontally close but should not be meleeable.</para>
    /// </remarks>
    public readonly struct TargetFilter
    {
        public readonly TargetAllegiance Allegiance;

        /// <summary>Maximum horizontal distance between footprints. Negative means unlimited.</summary>
        public readonly float MaxRange;

        /// <summary>Minimum horizontal distance between footprints. Used by abilities with a dead zone.</summary>
        public readonly float MinRange;

        /// <summary>Maximum absolute height difference. Negative means unlimited.</summary>
        public readonly float MaxHeightDifference;

        /// <summary>Whether dead entities qualify. Resurrection abilities set this to true.</summary>
        public readonly bool AllowDead;

        /// <summary>Whether the observer may be returned by its own query.</summary>
        public readonly bool AllowSelf;

        public TargetFilter(
            TargetAllegiance allegiance,
            float maxRange = -1f,
            float minRange = 0f,
            float maxHeightDifference = -1f,
            bool allowDead = false,
            bool allowSelf = false)
        {
            Allegiance = allegiance;
            MaxRange = maxRange;
            MinRange = minRange;
            MaxHeightDifference = maxHeightDifference;
            AllowDead = allowDead;
            AllowSelf = allowSelf;
        }

        /// <summary>Any living hostile, at any distance.</summary>
        public static TargetFilter AnyHostile => new TargetFilter(TargetAllegiance.Hostile);

        /// <summary>Any living ally including the observer, at any distance.</summary>
        public static TargetFilter AnyFriendly => new TargetFilter(TargetAllegiance.Friendly, allowSelf: true);

        /// <summary>Living hostiles within <paramref name="range"/> metres.</summary>
        public static TargetFilter HostileWithin(float range) =>
            new TargetFilter(TargetAllegiance.Hostile, range);

        /// <summary>Living allies within <paramref name="range"/> metres, including the observer.</summary>
        public static TargetFilter FriendlyWithin(float range) =>
            new TargetFilter(TargetAllegiance.Friendly, range, allowSelf: true);

        public TargetFilter WithMaxRange(float maxRange) => new TargetFilter(
            Allegiance, maxRange, MinRange, MaxHeightDifference, AllowDead, AllowSelf);

        public TargetFilter WithSelfAllowed(bool allowSelf) => new TargetFilter(
            Allegiance, MaxRange, MinRange, MaxHeightDifference, AllowDead, allowSelf);

        public TargetFilter WithDeadAllowed(bool allowDead) => new TargetFilter(
            Allegiance, MaxRange, MinRange, MaxHeightDifference, allowDead, AllowSelf);

        /// <summary>Whether <paramref name="candidate"/> satisfies every condition for <paramref name="observer"/>.</summary>
        public bool Matches(ISimEntity observer, ISimEntity candidate)
        {
            if (candidate == null)
            {
                return false;
            }

            if (observer != null && candidate.Id == observer.Id && !AllowSelf)
            {
                return false;
            }

            if (!candidate.IsAlive && !AllowDead)
            {
                return false;
            }

            if (observer != null && !MatchesAllegiance(observer.Faction, candidate.Faction))
            {
                return false;
            }

            if (observer == null)
            {
                return true;
            }

            if (MaxHeightDifference >= 0f)
            {
                float heightDelta = candidate.Position.Y - observer.Position.Y;
                if (heightDelta < 0f)
                {
                    heightDelta = -heightDelta;
                }

                if (heightDelta > MaxHeightDifference)
                {
                    return false;
                }
            }

            if (MaxRange < 0f && MinRange <= 0f)
            {
                return true;
            }

            float gap = EdgeDistance(observer, candidate);
            if (MaxRange >= 0f && gap > MaxRange)
            {
                return false;
            }

            return !(MinRange > 0f) || !(gap < MinRange);
        }

        /// <summary>
        /// Horizontal gap between two footprints, never below zero. This is the distance every range
        /// check in the project uses.
        /// </summary>
        public static float EdgeDistance(ISimEntity a, ISimEntity b)
        {
            float centreDistance = Vec3.FlatDistance(a.Position, b.Position);
            float gap = centreDistance - a.Radius - b.Radius;
            return gap < 0f ? 0f : gap;
        }

        private bool MatchesAllegiance(Faction observer, Faction candidate)
        {
            switch (Allegiance)
            {
                case TargetAllegiance.Hostile:
                    return FactionRelations.IsHostile(observer, candidate);
                case TargetAllegiance.Friendly:
                    return FactionRelations.IsFriendly(observer, candidate);
                default:
                    return true;
            }
        }
    }
}
