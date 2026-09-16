using System.Collections.Generic;
using NUnit.Framework;
using RaidSim.Core.Common;
using RaidSim.Core.Entities;
using RaidSim.Core.Events;
using RaidSim.Core.Mathematics;
using RaidSim.Core.Targeting;

namespace RaidSim.Tests.Core
{
    [TestFixture]
    public sealed class TargetFilterTests
    {
        [Test]
        public void HostileFilter_AcceptsEnemiesAndRejectsAllies()
        {
            var player = new TestEntity("player", Faction.Raid);
            var ally = new TestEntity("ally", Faction.Raid);
            var enemy = new TestEntity("enemy", Faction.Enemy);

            TargetFilter filter = TargetFilter.AnyHostile;

            Assert.That(filter.Matches(player, enemy), Is.True);
            Assert.That(filter.Matches(player, ally), Is.False);
        }

        [Test]
        public void FriendlyFilter_AcceptsAlliesAndRejectsEnemies()
        {
            var player = new TestEntity("player", Faction.Raid);
            var ally = new TestEntity("ally", Faction.Raid);
            var enemy = new TestEntity("enemy", Faction.Enemy);

            TargetFilter filter = TargetFilter.AnyFriendly;

            Assert.That(filter.Matches(player, ally), Is.True);
            Assert.That(filter.Matches(player, enemy), Is.False);
        }

        [Test]
        public void SelfIsExcluded_UnlessExplicitlyAllowed()
        {
            var player = new TestEntity("player", Faction.Raid);

            Assert.That(new TargetFilter(TargetAllegiance.Friendly).Matches(player, player), Is.False);
            Assert.That(TargetFilter.AnyFriendly.Matches(player, player), Is.True);
        }

        [Test]
        public void DeadEntitiesAreExcluded_UnlessExplicitlyAllowed()
        {
            var player = new TestEntity("player", Faction.Raid);
            var corpse = new TestEntity("corpse", Faction.Raid).Kill();

            Assert.That(TargetFilter.AnyFriendly.Matches(player, corpse), Is.False);
            Assert.That(TargetFilter.AnyFriendly.WithDeadAllowed(true).Matches(player, corpse), Is.True);
        }

        [Test]
        public void RangeIsMeasuredBetweenFootprints_NotCentres()
        {
            var player = new TestEntity("player", Faction.Raid, radius: 0.5f);
            var boss = new TestEntity("boss", Faction.Enemy, position: new Vec3(0f, 0f, 9f), radius: 4f);

            // Centres are 9m apart, but the footprints are only 4.5m apart, so a 5m melee ability reaches.
            Assert.That(TargetFilter.HostileWithin(5f).Matches(player, boss), Is.True);
            Assert.That(TargetFilter.HostileWithin(4f).Matches(player, boss), Is.False);
        }

        [Test]
        public void MinimumRange_ExcludesTargetsInsideTheDeadZone()
        {
            var archer = new TestEntity("archer", Faction.Raid, radius: 0f);
            var adjacent = new TestEntity("adjacent", Faction.Enemy, position: new Vec3(0f, 0f, 2f), radius: 0f);
            var distant = new TestEntity("distant", Faction.Enemy, position: new Vec3(0f, 0f, 20f), radius: 0f);

            var filter = new TargetFilter(TargetAllegiance.Hostile, maxRange: 40f, minRange: 8f);

            Assert.That(filter.Matches(archer, adjacent), Is.False);
            Assert.That(filter.Matches(archer, distant), Is.True);
        }

        [Test]
        public void HeightDifference_IsCheckedSeparatelyFromHorizontalRange()
        {
            var player = new TestEntity("player", Faction.Raid, radius: 0f);
            var onBalcony = new TestEntity("balcony", Faction.Enemy, position: new Vec3(0f, 12f, 1f), radius: 0f);

            var melee = new TargetFilter(TargetAllegiance.Hostile, maxRange: 5f, maxHeightDifference: 3f);

            Assert.That(melee.Matches(player, onBalcony), Is.False,
                "Standing directly under a balcony must not put the target in melee range.");
            Assert.That(TargetFilter.HostileWithin(5f).Matches(player, onBalcony), Is.True,
                "Without a height limit the horizontal check alone decides.");
        }

        [Test]
        public void EdgeDistance_IsNeverNegativeForOverlappingFootprints()
        {
            var a = new TestEntity("a", Faction.Raid, radius: 3f);
            var b = new TestEntity("b", Faction.Enemy, position: new Vec3(0f, 0f, 1f), radius: 3f);

            Assert.That(TargetFilter.EdgeDistance(a, b), Is.Zero);
        }
    }

    [TestFixture]
    public sealed class TargetQueryTests
    {
        private EventBus _bus;
        private EntityRegistry _registry;
        private TargetQuery _query;
        private TestEntity _player;

        [SetUp]
        public void SetUp()
        {
            _bus = new EventBus();
            _registry = new EntityRegistry(_bus);
            _query = new TargetQuery(_registry);
            _player = new TestEntity("player", Faction.Raid, radius: 0f);
            _registry.Register(_player);
        }

        private TestEntity AddEnemyAt(string name, float x, float z, float radius = 0f)
        {
            var enemy = new TestEntity(name, Faction.Enemy, position: new Vec3(x, 0f, z), radius: radius);
            _registry.Register(enemy);
            return enemy;
        }

        [Test]
        public void SelectNearest_PicksTheClosestHostile()
        {
            AddEnemyAt("far", 0f, 30f);
            TestEntity near = AddEnemyAt("near", 0f, 5f);

            ISimEntity result = _query.SelectNearest(_player, TargetFilter.AnyHostile);

            Assert.That(result, Is.SameAs(near));
        }

        [Test]
        public void SelectNearest_IgnoresTheDead()
        {
            AddEnemyAt("corpse", 0f, 2f).Kill();
            TestEntity alive = AddEnemyAt("alive", 0f, 20f);

            Assert.That(_query.SelectNearest(_player, TargetFilter.AnyHostile), Is.SameAs(alive));
        }

        [Test]
        public void SelectNearest_ReturnsNull_WhenNothingMatches()
        {
            Assert.That(_query.SelectNearest(_player, TargetFilter.AnyHostile), Is.Null);
        }

        [Test]
        public void SelectNearest_RespectsMaximumRange()
        {
            AddEnemyAt("distant", 0f, 100f);

            Assert.That(_query.SelectNearest(_player, TargetFilter.HostileWithin(10f)), Is.Null);
        }

        [Test]
        public void Collect_ReturnsEveryMatchAndClearsTheBufferFirst()
        {
            AddEnemyAt("a", 0f, 1f);
            AddEnemyAt("b", 0f, 2f);
            var results = new List<ISimEntity> { _player };

            _query.Collect(_player, TargetFilter.AnyHostile, results);

            Assert.That(results.Count, Is.EqualTo(2));
        }

        [Test]
        public void SelectNearestInCone_IgnoresTargetsOutsideTheCone()
        {
            _player.Facing = Vec3.Forward;
            TestEntity ahead = AddEnemyAt("ahead", 0f, 20f);
            AddEnemyAt("behind", 0f, -3f);

            ISimEntity result = _query.SelectNearestInCone(_player, TargetFilter.AnyHostile, halfAngleDegrees: 45f);

            Assert.That(result, Is.SameAs(ahead),
                "The closer enemy is behind the player and must not be selected.");
        }

        [Test]
        public void SelectNearestInCone_ReturnsNull_WhenTheConeIsEmpty()
        {
            _player.Facing = Vec3.Forward;
            AddEnemyAt("behind", 0f, -10f);

            Assert.That(_query.SelectNearestInCone(_player, TargetFilter.AnyHostile, 30f), Is.Null);
        }

        [Test]
        public void SelectNextCycling_WalksTargetsInDistanceOrderAndWraps()
        {
            TestEntity first = AddEnemyAt("first", 0f, 5f);
            TestEntity second = AddEnemyAt("second", 0f, 10f);
            TestEntity third = AddEnemyAt("third", 0f, 15f);

            ISimEntity a = _query.SelectNextCycling(_player, TargetFilter.AnyHostile, null);
            ISimEntity b = _query.SelectNextCycling(_player, TargetFilter.AnyHostile, a);
            ISimEntity c = _query.SelectNextCycling(_player, TargetFilter.AnyHostile, b);
            ISimEntity wrapped = _query.SelectNextCycling(_player, TargetFilter.AnyHostile, c);

            Assert.That(a, Is.SameAs(first));
            Assert.That(b, Is.SameAs(second));
            Assert.That(c, Is.SameAs(third));
            Assert.That(wrapped, Is.SameAs(first));
        }

        [Test]
        public void SelectNextCycling_RestartsFromTheNearest_WhenTheCurrentTargetIsGone()
        {
            TestEntity near = AddEnemyAt("near", 0f, 5f);
            TestEntity dead = AddEnemyAt("dead", 0f, 10f);
            dead.Kill();

            Assert.That(_query.SelectNextCycling(_player, TargetFilter.AnyHostile, dead), Is.SameAs(near));
        }

        [Test]
        public void SelectBest_BreaksTiesDeterministically()
        {
            TestEntity lowerId = AddEnemyAt("a", 0f, 10f);
            AddEnemyAt("b", 0f, 10f);

            ISimEntity first = _query.SelectBest(_player, TargetFilter.AnyHostile, _ => 1f);
            ISimEntity again = _query.SelectBest(_player, TargetFilter.AnyHostile, _ => 1f);

            Assert.That(first, Is.SameAs(lowerId));
            Assert.That(again, Is.SameAs(first), "Equal candidates must not make the selection flicker.");
        }

        [Test]
        public void SelectBest_FindsTheLowestHealthAlly_WhichIsHowHealerAiWillAsk()
        {
            var hurt = new TestEntity("hurt", Faction.Raid, maxHealth: 1000f);
            var healthy = new TestEntity("healthy", Faction.Raid, maxHealth: 1000f);
            hurt.Health.Remove(800f);
            _registry.Register(hurt);
            _registry.Register(healthy);

            ISimEntity result = _query.SelectBest(
                _player,
                TargetFilter.AnyFriendly,
                candidate => candidate is ICombatEntity combatant ? -combatant.Health.Fraction : float.NegativeInfinity);

            Assert.That(result, Is.SameAs(hurt));
        }
    }

    [TestFixture]
    public sealed class TargetSelectionTests
    {
        private EventBus _bus;
        private EntityRegistry _registry;
        private TestEntity _player;
        private TargetSelection _selection;

        [SetUp]
        public void SetUp()
        {
            _bus = new EventBus();
            _registry = new EntityRegistry(_bus);
            _player = new TestEntity("player", Faction.Raid);
            _registry.Register(_player);
            _selection = new TargetSelection(_player.Id, _registry, _bus);
        }

        [TearDown]
        public void TearDown() => _selection.Dispose();

        private TestEntity RegisterEnemy(string name)
        {
            var enemy = new TestEntity(name, Faction.Enemy);
            _registry.Register(enemy);
            return enemy;
        }

        [Test]
        public void Set_StoresTheTargetAndPublishesTheChange()
        {
            TestEntity enemy = RegisterEnemy("enemy");
            TargetChangedEvent? received = null;
            _bus.Subscribe<TargetChangedEvent>(evt => received = evt);

            _selection.Set(enemy);

            Assert.That(_selection.Target, Is.SameAs(enemy));
            Assert.That(received.HasValue, Is.True);
            Assert.That(received.Value.NewTarget, Is.EqualTo(enemy.Id));
            Assert.That(received.Value.PreviousTarget, Is.EqualTo(EntityId.None));
        }

        [Test]
        public void Set_ToTheSameTarget_PublishesNothing()
        {
            TestEntity enemy = RegisterEnemy("enemy");
            _selection.Set(enemy);

            int events = 0;
            _bus.Subscribe<TargetChangedEvent>(_ => events++);
            _selection.Set(enemy);

            Assert.That(events, Is.Zero);
        }

        [Test]
        public void Set_RefusesEntitiesTheSimulationDoesNotKnow()
        {
            var stranger = new TestEntity("stranger", Faction.Enemy);

            _selection.Set(stranger);

            Assert.That(_selection.HasTarget, Is.False);
        }

        [Test]
        public void Clear_PublishesTheChangeAndDropsTheTarget()
        {
            TestEntity enemy = RegisterEnemy("enemy");
            _selection.Set(enemy);

            TargetChangedEvent? received = null;
            _bus.Subscribe<TargetChangedEvent>(evt => received = evt);
            _selection.Clear();

            Assert.That(_selection.HasTarget, Is.False);
            Assert.That(received.Value.PreviousTarget, Is.EqualTo(enemy.Id));
        }

        [Test]
        public void TargetLeavingTheSimulation_ClearsTheSelectionAutomatically()
        {
            TestEntity enemy = RegisterEnemy("enemy");
            _selection.Set(enemy);

            _registry.Unregister(enemy);

            Assert.That(_selection.HasTarget, Is.False,
                "A despawned target must never stay selected; abilities would fire at nothing.");
        }

        [Test]
        public void DeadTargetIsKept_ButFlagged()
        {
            TestEntity enemy = RegisterEnemy("enemy");
            _selection.Set(enemy);

            enemy.Kill();

            Assert.That(_selection.HasTarget, Is.True, "The corpse stays selected so the UI can grey it out.");
            Assert.That(_selection.HasDeadTarget, Is.True);
        }

        [Test]
        public void ClearIfInvalid_DropsATargetThatLeftRange()
        {
            TestEntity enemy = RegisterEnemy("enemy");
            enemy.Position = new Vec3(0f, 0f, 100f);
            _selection.Set(enemy);

            _selection.ClearIfInvalid(_player, TargetFilter.HostileWithin(10f));

            Assert.That(_selection.HasTarget, Is.False);
        }
    }
}
