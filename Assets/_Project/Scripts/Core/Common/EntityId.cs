using System;
using System.Globalization;
using System.Threading;

namespace RaidSim.Core.Common
{
    /// <summary>
    /// Stable, process-unique handle for a simulation entity.
    /// </summary>
    /// <remarks>
    /// Systems address each other by id rather than by object reference so that dead entities,
    /// pooled views and serialized combat-log lines never keep a graph of live objects alive.
    /// It also keeps the architecture rule from <c>CLAUDE.md</c> enforceable: nothing in gameplay
    /// code may branch on a display name.
    /// </remarks>
    [Serializable]
    public readonly struct EntityId : IEquatable<EntityId>, IComparable<EntityId>
    {
        private static int _next;

        public readonly int Value;

        private EntityId(int value)
        {
            Value = value;
        }

        /// <summary>The "no entity" value. Never returned by <see cref="Next"/>.</summary>
        public static EntityId None => new EntityId(0);

        public bool IsValid => Value != 0;

        /// <summary>Allocates the next unique id. Thread safe.</summary>
        public static EntityId Next() => new EntityId(Interlocked.Increment(ref _next));

        /// <summary>Rebuilds an id from a persisted value (save games, replays, test fixtures).</summary>
        public static EntityId FromValue(int value) => new EntityId(value);

        /// <summary>
        /// Resets the allocator. Test-only: keeps ids small and deterministic between test cases.
        /// </summary>
        public static void ResetAllocatorForTests() => Interlocked.Exchange(ref _next, 0);

        public bool Equals(EntityId other) => Value == other.Value;

        public override bool Equals(object obj) => obj is EntityId other && Equals(other);

        public override int GetHashCode() => Value;

        public int CompareTo(EntityId other) => Value.CompareTo(other.Value);

        public static bool operator ==(EntityId a, EntityId b) => a.Equals(b);

        public static bool operator !=(EntityId a, EntityId b) => !a.Equals(b);

        public override string ToString() =>
            IsValid ? Value.ToString(CultureInfo.InvariantCulture) : "none";
    }
}
