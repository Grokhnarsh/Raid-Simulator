using System.Collections.Generic;
using NUnit.Framework;
using RaidSim.Core.Common;
using RaidSim.Core.Entities;
using RaidSim.Core.Events;

namespace RaidSim.Tests.Core
{
    [TestFixture]
    public sealed class EntityRegistryTests
    {
        private EventBus _bus;
        private EntityRegistry _registry;

        [SetUp]
        public void SetUp()
        {
            _bus = new EventBus();
            _registry = new EntityRegistry(_bus);
        }

        [Test]
        public void Register_AddsTheEntityAndPublishesIt()
        {
            var entity = new TestEntity("aric", Faction.Raid);
            EntityRegisteredEvent? received = null;
            _bus.Subscribe<EntityRegisteredEvent>(evt => received = evt);

            Assert.That(_registry.Register(entity), Is.True);

            Assert.That(_registry.Count, Is.EqualTo(1));
            Assert.That(received.HasValue, Is.True);
            Assert.That(received.Value.Entity, Is.EqualTo(entity.Id));
        }

        [Test]
        public void Register_IsIdempotentForTheSameEntity()
        {
            var entity = new TestEntity("aric");
            _registry.Register(entity);

            Assert.That(_registry.Register(entity), Is.False);
            Assert.That(_registry.Count, Is.EqualTo(1), "A double registration must not duplicate a raid frame.");
        }

        [Test]
        public void Unregister_RemovesTheEntityAndPublishesIt()
        {
            var entity = new TestEntity("aric");
            _registry.Register(entity);
            EntityUnregisteredEvent? received = null;
            _bus.Subscribe<EntityUnregisteredEvent>(evt => received = evt);

            Assert.That(_registry.Unregister(entity), Is.True);

            Assert.That(_registry.Count, Is.Zero);
            Assert.That(received.Value.Entity, Is.EqualTo(entity.Id));
        }

        [Test]
        public void Unregister_ForAnUnknownEntity_ReturnsFalse()
        {
            Assert.That(_registry.Unregister(new TestEntity("stranger")), Is.False);
        }

        [Test]
        public void TryGet_FindsARegisteredEntityById()
        {
            var entity = new TestEntity("aric");
            _registry.Register(entity);

            Assert.That(_registry.TryGet(entity.Id, out ISimEntity found), Is.True);
            Assert.That(found, Is.SameAs(entity));
        }

        [Test]
        public void Get_ReturnsNullForAnUnknownId()
        {
            Assert.That(_registry.Get<TestEntity>(EntityId.FromValue(4242)), Is.Null);
        }

        [Test]
        public void OfFaction_ReturnsOnlyThatFaction()
        {
            _registry.Register(new TestEntity("raider", Faction.Raid));
            _registry.Register(new TestEntity("raider2", Faction.Raid));
            _registry.Register(new TestEntity("enemy", Faction.Enemy));

            Assert.That(_registry.OfFaction(Faction.Raid).Count, Is.EqualTo(2));
            Assert.That(_registry.OfFaction(Faction.Enemy).Count, Is.EqualTo(1));
        }

        [Test]
        public void OfFaction_ReturnsAnEmptyListForAnUnusedFaction()
        {
            Assert.That(_registry.OfFaction(Faction.Enemy), Is.Not.Null.And.Empty);
        }

        [Test]
        public void CollectHostiles_SkipsAlliesAndTheDead()
        {
            _registry.Register(new TestEntity("ally", Faction.Raid));
            _registry.Register(new TestEntity("enemy", Faction.Enemy));
            _registry.Register(new TestEntity("corpse", Faction.Enemy).Kill());
            var results = new List<ISimEntity>();

            _registry.CollectHostiles(Faction.Raid, results);

            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].DisplayName, Is.EqualTo("enemy"));
        }

        [Test]
        public void CollectAllies_IncludesTheWholeFaction()
        {
            _registry.Register(new TestEntity("a", Faction.Raid));
            _registry.Register(new TestEntity("b", Faction.Raid));
            _registry.Register(new TestEntity("enemy", Faction.Enemy));
            var results = new List<ISimEntity>();

            _registry.CollectAllies(Faction.Raid, results);

            Assert.That(results.Count, Is.EqualTo(2));
        }

        [Test]
        public void NeutralEntitiesAreHostileToNobody()
        {
            _registry.Register(new TestEntity("prop", Faction.Neutral));
            var results = new List<ISimEntity>();

            _registry.CollectHostiles(Faction.Raid, results);

            Assert.That(results, Is.Empty);
        }

        [Test]
        public void Clear_EmptiesTheRegistry()
        {
            _registry.Register(new TestEntity("a"));
            _registry.Register(new TestEntity("b"));

            _registry.Clear();

            Assert.That(_registry.Count, Is.Zero);
            Assert.That(_registry.OfFaction(Faction.Raid), Is.Empty);
        }
    }
}
