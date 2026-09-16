using NUnit.Framework;
using RaidSim.Core.Combat;
using RaidSim.Core.Common;
using RaidSim.Core.Entities;
using RaidSim.Core.Events;
using RaidSim.Core.Mathematics;
using RaidSim.Core.Randomness;
using RaidSim.Core.Simulation;
using RaidSim.Core.Targeting;

namespace RaidSim.Tests.Core
{
    [TestFixture]
    public sealed class AutoAttackSystemTests
    {
        private const float Tolerance = 0.01f;

        /// <summary>A target provider a test can point wherever it likes.</summary>
        private sealed class StubTargets : ITargetProvider
        {
            public ISimEntity CurrentTarget { get; set; }
        }

        private EventBus _bus;
        private EntityRegistry _registry;
        private CombatSystem _combat;
        private AutoAttackSystem _autoAttack;
        private StubTargets _targets;

        [SetUp]
        public void SetUp()
        {
            _bus = new EventBus();
            _registry = new EntityRegistry(_bus);
            _combat = new CombatSystem(_bus, new CombatTuning(100f, 100f, 0.75f), FixedRandomSource.AlwaysFails());
            _autoAttack = new AutoAttackSystem(_combat, _bus);
            _targets = new StubTargets();
        }

        [TearDown]
        public void TearDown()
        {
            _autoAttack.Dispose();
            _combat.Dispose();
        }

        private static AutoAttackProfile Profile(float interval = 2f, float damage = 50f, float range = 5f) =>
            new AutoAttackProfile(interval, damage, 0f, DamageType.Physical, range, "Attack");

        private TestEntity Register(string name, Faction faction, float x = 0f, float z = 0f, float maxHealth = 1000f)
        {
            var entity = new TestEntity(
                name, faction, maxHealth: maxHealth, position: new Vec3(x, 0f, z), radius: 0f);
            _registry.Register(entity);
            return entity;
        }

        [Test]
        public void AnAttackerSwingsImmediatelyOnEngaging()
        {
            TestEntity attacker = Register("attacker", Faction.Raid);
            TestEntity victim = Register("victim", Faction.Enemy, z: 2f);
            _autoAttack.Register(attacker, Profile(), _targets);
            _targets.CurrentTarget = victim;

            _autoAttack.Tick(0.1f);

            Assert.That(victim.Health.Current, Is.EqualTo(950f).Within(Tolerance),
                "Waiting a full swing interval before the first hit of a fight feels broken.");
        }

        [Test]
        public void SubsequentSwingsWaitTheFullInterval()
        {
            TestEntity attacker = Register("attacker", Faction.Raid);
            TestEntity victim = Register("victim", Faction.Enemy, z: 2f);
            _autoAttack.Register(attacker, Profile(interval: 2f), _targets);
            _targets.CurrentTarget = victim;

            _autoAttack.Tick(0.1f);   // first swing
            _autoAttack.Tick(1.0f);   // not yet
            Assert.That(victim.Health.Current, Is.EqualTo(950f).Within(Tolerance));

            _autoAttack.Tick(1.0f);   // interval elapsed
            Assert.That(victim.Health.Current, Is.EqualTo(900f).Within(Tolerance));
        }

        [Test]
        public void NoTarget_MeansNoSwing()
        {
            TestEntity attacker = Register("attacker", Faction.Raid);
            Register("victim", Faction.Enemy, z: 2f);
            _autoAttack.Register(attacker, Profile(), _targets);

            Assert.DoesNotThrow(() => _autoAttack.Tick(10f));
        }

        [Test]
        public void ATargetOutOfRange_IsNotAttacked()
        {
            TestEntity attacker = Register("attacker", Faction.Raid);
            TestEntity victim = Register("victim", Faction.Enemy, z: 40f);
            _autoAttack.Register(attacker, Profile(range: 5f), _targets);
            _targets.CurrentTarget = victim;

            _autoAttack.Tick(10f);

            Assert.That(victim.Health.IsFull, Is.True);
        }

        [Test]
        public void RangeIsMeasuredBetweenFootprints()
        {
            var attacker = new TestEntity("attacker", Faction.Raid, position: Vec3.Zero, radius: 0.5f);
            var boss = new TestEntity(
                "boss", Faction.Enemy, maxHealth: 1000f, position: new Vec3(0f, 0f, 8f), radius: 4f);
            _registry.Register(attacker);
            _registry.Register(boss);
            _autoAttack.Register(attacker, Profile(range: 5f), _targets);
            _targets.CurrentTarget = boss;

            _autoAttack.Tick(0.1f);

            Assert.That(boss.Health.Current, Is.EqualTo(950f).Within(Tolerance),
                "Centres are 8m apart but the footprints are only 3.5m apart, so melee reaches.");
        }

        [Test]
        public void AFriendlyTarget_IsNeverAttacked()
        {
            TestEntity attacker = Register("attacker", Faction.Raid);
            TestEntity ally = Register("ally", Faction.Raid, z: 2f);
            _autoAttack.Register(attacker, Profile(), _targets);
            _targets.CurrentTarget = ally;

            _autoAttack.Tick(10f);

            Assert.That(ally.Health.IsFull, Is.True);
        }

        [Test]
        public void ADeadTarget_IsNotAttacked()
        {
            TestEntity attacker = Register("attacker", Faction.Raid);
            TestEntity victim = Register("victim", Faction.Enemy, z: 2f);
            victim.Kill();
            _autoAttack.Register(attacker, Profile(), _targets);
            _targets.CurrentTarget = victim;

            int events = 0;
            _bus.Subscribe<DamageDealtEvent>(_ => events++);
            _autoAttack.Tick(10f);

            Assert.That(events, Is.Zero);
        }

        [Test]
        public void ADeadAttacker_DoesNotSwing()
        {
            TestEntity attacker = Register("attacker", Faction.Raid);
            TestEntity victim = Register("victim", Faction.Enemy, z: 2f);
            attacker.Kill();
            _autoAttack.Register(attacker, Profile(), _targets);
            _targets.CurrentTarget = victim;

            _autoAttack.Tick(10f);

            Assert.That(victim.Health.IsFull, Is.True);
        }

        [Test]
        public void SwappingTargets_DoesNotResetTheSwingTimer()
        {
            TestEntity attacker = Register("attacker", Faction.Raid);
            TestEntity first = Register("first", Faction.Enemy, z: 2f);
            TestEntity second = Register("second", Faction.Enemy, x: 2f);
            _autoAttack.Register(attacker, Profile(interval: 2f), _targets);

            _targets.CurrentTarget = first;
            _autoAttack.Tick(0.1f);                       // swing lands, timer set to 2s
            Assert.That(first.Health.Current, Is.EqualTo(950f).Within(Tolerance));

            _targets.CurrentTarget = second;
            _autoAttack.Tick(0.1f);

            Assert.That(second.Health.IsFull, Is.True,
                "Target swapping must not be a way to swing faster than the interval allows.");
        }

        [Test]
        public void LosingATarget_PausesTheTimerRatherThanResettingIt()
        {
            TestEntity attacker = Register("attacker", Faction.Raid);
            TestEntity victim = Register("victim", Faction.Enemy, z: 2f);
            _autoAttack.Register(attacker, Profile(interval: 2f), _targets);
            _targets.CurrentTarget = victim;

            _autoAttack.Tick(0.1f);         // swing, timer at 2s
            _autoAttack.Tick(1.5f);         // timer at 0.5s
            _targets.CurrentTarget = null;
            _autoAttack.Tick(100f);         // no target: nothing should change
            _targets.CurrentTarget = victim;
            _autoAttack.Tick(0.4f);         // 0.1s still to go

            Assert.That(victim.Health.Current, Is.EqualTo(950f).Within(Tolerance));

            _autoAttack.Tick(0.2f);
            Assert.That(victim.Health.Current, Is.EqualTo(900f).Within(Tolerance));
        }

        [Test]
        public void AProfileWithNoSwingInterval_IsNotRegistered()
        {
            TestEntity dummy = Register("dummy", Faction.Enemy);

            bool registered = _autoAttack.Register(
                dummy, new AutoAttackProfile(0f, 100f, 0f, DamageType.Physical, 5f, "None"), _targets);

            Assert.That(registered, Is.False, "A practice target that never attacks costs nothing to tick.");
            Assert.That(_autoAttack.AttackerCount, Is.Zero);
        }

        [Test]
        public void RegisteringTheSameEntityTwice_IsIgnored()
        {
            TestEntity attacker = Register("attacker", Faction.Raid);

            _autoAttack.Register(attacker, Profile(), _targets);

            Assert.That(_autoAttack.Register(attacker, Profile(), _targets), Is.False);
            Assert.That(_autoAttack.AttackerCount, Is.EqualTo(1));
        }

        [Test]
        public void AnEntityLeavingTheSimulation_StopsSwinging()
        {
            TestEntity attacker = Register("attacker", Faction.Raid);
            _autoAttack.Register(attacker, Profile(), _targets);

            _registry.Unregister(attacker);

            Assert.That(_autoAttack.AttackerCount, Is.Zero);
        }

        [Test]
        public void UnregisteringOneOfSeveral_KeepsTheRestSwinging()
        {
            TestEntity a = Register("a", Faction.Raid);
            TestEntity b = Register("b", Faction.Raid);
            TestEntity c = Register("c", Faction.Raid);
            TestEntity victim = Register("victim", Faction.Enemy, z: 2f);
            _autoAttack.Register(a, Profile(), _targets);
            _autoAttack.Register(b, Profile(), _targets);
            _autoAttack.Register(c, Profile(), _targets);
            _targets.CurrentTarget = victim;

            // Removing the middle entry exercises the swap-remove bookkeeping.
            _autoAttack.Unregister(b.Id);
            _autoAttack.Tick(0.1f);

            Assert.That(_autoAttack.AttackerCount, Is.EqualTo(2));
            Assert.That(victim.Health.Current, Is.EqualTo(900f).Within(Tolerance),
                "Both remaining attackers must still land their swing.");
        }

        [Test]
        public void AutoAttack_KillsATargetOverTime_WhichIsThePhaseTwoGoal()
        {
            TestEntity attacker = Register("attacker", Faction.Raid);
            TestEntity victim = Register("victim", Faction.Enemy, z: 2f, maxHealth: 200f);
            _autoAttack.Register(attacker, Profile(interval: 1f, damage: 50f), _targets);
            _targets.CurrentTarget = victim;
            EntityDiedEvent? died = null;
            _bus.Subscribe<EntityDiedEvent>(evt => died = evt);

            for (int i = 0; i < 40; i++)
            {
                _autoAttack.Tick(0.1f);
            }

            Assert.That(victim.IsAlive, Is.False);
            Assert.That(died.HasValue, Is.True);
            Assert.That(died.Value.Killer, Is.EqualTo(attacker.Id));
        }

        [Test]
        public void TheSystemTicksAtTheAbilityOrder()
        {
            Assert.That(_autoAttack.Order, Is.EqualTo(SystemOrder.Abilities));
        }
    }
}
