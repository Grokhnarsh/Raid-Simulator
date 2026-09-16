using System;
using System.Collections.Generic;
using RaidSim.Core.Common;
using RaidSim.Core.Events;

namespace RaidSim.Core.Entities
{
    /// <summary>
    /// The authoritative list of everything currently in the simulation.
    /// </summary>
    /// <remarks>
    /// <para>This exists so that no system ever calls <c>FindObjectsOfType</c> or walks the scene
    /// graph. Entities register when they enter the encounter and unregister when they leave, and
    /// every query — "all hostiles", "all raid members", "who is within eight metres" — is served
    /// from these lists.</para>
    /// <para>The per-faction buckets are maintained on write because reads happen many times per
    /// tick (every AI, every area-of-effect, every raid frame) while writes happen a handful of
    /// times per pull.</para>
    /// </remarks>
    public sealed class EntityRegistry
    {
        private readonly Dictionary<EntityId, ISimEntity> _byId = new Dictionary<EntityId, ISimEntity>();
        private readonly List<ISimEntity> _all = new List<ISimEntity>();
        private readonly Dictionary<Faction, List<ISimEntity>> _byFaction = new Dictionary<Faction, List<ISimEntity>>();
        private readonly IEventBus _eventBus;

        public EntityRegistry(IEventBus eventBus = null)
        {
            _eventBus = eventBus;
        }

        public int Count => _all.Count;

        /// <summary>Every registered entity. Do not mutate while iterating; copy first if you must.</summary>
        public IReadOnlyList<ISimEntity> All => _all;

        /// <summary>
        /// Adds <paramref name="entity"/>. Returns false when the id is already registered, which
        /// makes double-registration a no-op rather than a duplicated raid frame.
        /// </summary>
        public bool Register(ISimEntity entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            if (!entity.Id.IsValid || _byId.ContainsKey(entity.Id))
            {
                return false;
            }

            _byId.Add(entity.Id, entity);
            _all.Add(entity);
            GetOrCreateBucket(entity.Faction).Add(entity);

            _eventBus?.Publish(new EntityRegisteredEvent(entity.Id, entity.Faction));
            return true;
        }

        public bool Unregister(ISimEntity entity) => entity != null && Unregister(entity.Id);

        public bool Unregister(EntityId id)
        {
            if (!_byId.TryGetValue(id, out ISimEntity entity))
            {
                return false;
            }

            _byId.Remove(id);
            _all.Remove(entity);
            if (_byFaction.TryGetValue(entity.Faction, out List<ISimEntity> bucket))
            {
                bucket.Remove(entity);
            }

            _eventBus?.Publish(new EntityUnregisteredEvent(id, entity.Faction));
            return true;
        }

        public bool TryGet(EntityId id, out ISimEntity entity) => _byId.TryGetValue(id, out entity);

        /// <summary>Typed lookup. Returns null when the id is unknown or is not a <typeparamref name="T"/>.</summary>
        public T Get<T>(EntityId id) where T : class, ISimEntity =>
            _byId.TryGetValue(id, out ISimEntity entity) ? entity as T : null;

        public bool Contains(EntityId id) => _byId.ContainsKey(id);

        /// <summary>Entities of one faction. Returns an empty list rather than null.</summary>
        public IReadOnlyList<ISimEntity> OfFaction(Faction faction) =>
            _byFaction.TryGetValue(faction, out List<ISimEntity> bucket)
                ? bucket
                : (IReadOnlyList<ISimEntity>)Array.Empty<ISimEntity>();

        /// <summary>
        /// Appends every living entity hostile to <paramref name="observer"/> into
        /// <paramref name="results"/>. The caller owns the list, so queries in the tick loop can
        /// reuse one buffer instead of allocating.
        /// </summary>
        public void CollectHostiles(Faction observer, List<ISimEntity> results, bool livingOnly = true)
        {
            CollectWhere(results, entity =>
                FactionRelations.IsHostile(observer, entity.Faction) && (!livingOnly || entity.IsAlive));
        }

        /// <summary>Appends every living ally of <paramref name="observer"/>, including the observer.</summary>
        public void CollectAllies(Faction observer, List<ISimEntity> results, bool livingOnly = true)
        {
            CollectWhere(results, entity =>
                FactionRelations.IsFriendly(observer, entity.Faction) && (!livingOnly || entity.IsAlive));
        }

        public void CollectWhere(List<ISimEntity> results, Func<ISimEntity, bool> predicate)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            results.Clear();
            for (int i = 0; i < _all.Count; i++)
            {
                ISimEntity entity = _all[i];
                if (predicate == null || predicate(entity))
                {
                    results.Add(entity);
                }
            }
        }

        public void Clear()
        {
            _byId.Clear();
            _all.Clear();
            _byFaction.Clear();
        }

        private List<ISimEntity> GetOrCreateBucket(Faction faction)
        {
            if (_byFaction.TryGetValue(faction, out List<ISimEntity> bucket))
            {
                return bucket;
            }

            bucket = new List<ISimEntity>();
            _byFaction[faction] = bucket;
            return bucket;
        }
    }
}
