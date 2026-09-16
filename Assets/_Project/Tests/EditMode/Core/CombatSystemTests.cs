using System.Collections.Generic;
using NUnit.Framework;
using RaidSim.Core.Combat;
using RaidSim.Core.Common;
using RaidSim.Core.Entities;
using RaidSim.Core.Events;
using RaidSim.Core.Randomness;

namespace RaidSim.Tests.Core
{
    [TestFixture]
    public sealed class CombatSystemTests
    {
        private const float Tolerance = 0.01f;

        private EventBus _bus;
        private EntityRegistry _registry;
        private CombatSystem _combat;

        [SetUp]
        public void SetUp()
        {
            _bus = new EventBus();
            _registry = new EntityRegistry(_bus);
            _combat = new CombatSystem(
                _bus,
                new CombatTuning(100f, 100f, 0.75f),
                FixedRandomSource.AlwaysFails());
        }

        [TearDown]
        public void TearDown() => _combat.Dispose();

        private TestEntity Register(string name, Faction faction, float maxHealth = 1000f)
        {
            var entity = new TestEntity(name, faction, maxHealth: maxHealth);
            _registry.Register(entity);
            return entity;
        }

        [Test]
        public void ApplyDamage_PublishesBothPerspectivesOfTheSameHit()
        {
            TestEntity attacker = Register("attacker", Faction.Raid);
            TestEntity victim = Register("victim", Faction.Enemy);
            DamageDealtEvent? dealt = null;
            DamageTakenEvent? taken = null;
            _bus.Subscribe<DamageDealtEvent>(evt => dealt = evt);
            _bus.Subscribe<DamageTakenEvent>(evt => taken = evt);

            _combat.ApplyDamage(new DamageRequest(attacker, victim, 100f, DamageType.True));

            Assert.That(dealt.HasValue, Is.True);
            Assert.That(taken.HasValue, Is.True);
            Assert.That(dealt.Value.Result.Source, Is.EqualTo(attacker.Id));
            Assert.That(taken.Value.Result.Target, Is.EqualTo(victim.Id));
        }

        [Test]
        public void ApplyDamage_RemovesHealth()
        {
            TestEntity victim = Register("victim", Faction.Enemy, maxHealth: 500f);

            _combat.ApplyDamage(new DamageRequest(null, victim, 200f, DamageType.True));

            Assert.That(victim.Health.Current, Is.EqualTo(300f).Within(Tolerance));
        }

        [Test]
        public void ALethalBlow_PublishesTheDeath()
        {
            TestEntity attacker = Register("attacker", Faction.Raid);
            TestEntity victim = Register("victim", Faction.Enemy, maxHealth: 100f);
            EntityDiedEvent? died = null;
            _bus.Subscribe<EntityDiedEvent>(evt => died = evt);

            _combat.ApplyDamage(new DamageRequest(attacker, victim, 150f, DamageType.True, sourceLabel: "Strike"));

            Assert.That(died.HasValue, Is.True);
            Assert.That(died.Value.Entity, Is.EqualTo(victim.Id));
            Assert.That(died.Value.Killer, Is.EqualTo(attacker.Id));
            Assert.That(died.Value.Faction, Is.EqualTo(Faction.Enemy));
            Assert.That(died.Value.SourceLabel, Is.EqualTo("Strike"));
        }

        [Test]
        public void DeathIsReportedExactlyOnce_EvenIfTheCorpseIsHitAgain()
        {
            TestEntity victim = Register("victim", Faction.Enemy, maxHealth: 100f);
            int deaths = 0;
            _bus.Subscribe<EntityDiedEvent>(_ => deaths++);

            _combat.ApplyDamage(new DamageRequest(null, victim, 500f, DamageType.True));
            _combat.ApplyDamage(new DamageRequest(null, victim, 500f, DamageType.True));

            Assert.That(deaths, Is.EqualTo(1));
        }

        [Test]
        public void NonLethalDamage_PublishesNoDeath()
        {
            TestEntity victim = Register("victim", Faction.Enemy, maxHealth: 1000f);
            int deaths = 0;
            _bus.Subscribe<EntityDiedEvent>(_ => deaths++);

            _combat.ApplyDamage(new DamageRequest(null, victim, 100f, DamageType.True));

            Assert.That(deaths, Is.Zero);
        }

        [Test]
        public void DamageEventsCarryOverkill_SoStatisticsNeedNotRecomputeIt()
        {
            TestEntity victim = Register("victim", Faction.Enemy, maxHealth: 100f);
            DamageResult captured = default;
            _bus.Subscribe<DamageDealtEvent>(evt => captured = evt.Result);

            _combat.ApplyDamage(new DamageRequest(null, victim, 400f, DamageType.True));

            Assert.That(captured.Applied, Is.EqualTo(100f).Within(Tolerance));
            Assert.That(captured.Overkill, Is.EqualTo(300f).Within(Tolerance));
        }

        [Test]
        public void AFullyMitigatedHit_IsStillReported()
        {
            TestEntity victim = Register("victim", Faction.Enemy);
            victim.Stats.AddModifier(
                RaidSim.Core.Stats.StatModifier.PercentAdditive(RaidSim.Core.Stats.StatType.DamageTakenModifier, -1f));
            int dealtEvents = 0;
            _bus.Subscribe<DamageDealtEvent>(_ => dealtEvents++);

            _combat.ApplyDamage(new DamageRequest(null, victim, 500f, DamageType.True));

            Assert.That(dealtEvents, Is.EqualTo(1),
                "An attack that did nothing still happened; the combat log should show it.");
            Assert.That(victim.Health.IsFull, Is.True);
        }

        [Test]
        public void Kill_EndsTheTargetAndReportsItLikeAnyOtherDeath()
        {
            TestEntity victim = Register("victim", Faction.Enemy, maxHealth: 9999f);
            EntityDiedEvent? died = null;
            _bus.Subscribe<EntityDiedEvent>(evt => died = evt);

            _combat.Kill(victim, sourceLabel: "Debug");

            Assert.That(victim.IsAlive, Is.False);
            Assert.That(died.HasValue, Is.True, "Debug kills must go through the same funnel.");
        }

        [Test]
        public void Kill_OnACorpse_DoesNothing()
        {
            TestEntity victim = Register("victim", Faction.Enemy);
            _combat.Kill(victim);
            int deaths = 0;
            _bus.Subscribe<EntityDiedEvent>(_ => deaths++);

            _combat.Kill(victim);

            Assert.That(deaths, Is.Zero);
        }

        [Test]
        public void AnEntityLeavingTheSimulation_IsForgotten_SoItCanDieAgainIfReused()
        {
            TestEntity victim = Register("victim", Faction.Enemy, maxHealth: 100f);
            _combat.ApplyDamage(new DamageRequest(null, victim, 500f, DamageType.True));
            Assert.That(_combat.ReportedDeathCount, Is.EqualTo(1));

            _registry.Unregister(victim);

            Assert.That(_combat.ReportedDeathCount, Is.Zero);
        }

        [Test]
        public void ResetForEncounter_ForgetsEveryDeath()
        {
            TestEntity a = Register("a", Faction.Enemy, maxHealth: 10f);
            TestEntity b = Register("b", Faction.Enemy, maxHealth: 10f);
            _combat.Kill(a);
            _combat.Kill(b);

            _combat.ResetForEncounter();

            Assert.That(_combat.ReportedDeathCount, Is.Zero);
        }

        [Test]
        public void ApplyHealing_PublishesTheHeal()
        {
            TestEntity healer = Register("healer", Faction.Raid);
            TestEntity wounded = Register("wounded", Faction.Raid, maxHealth: 1000f);
            wounded.Health.Remove(400f);
            HealAppliedEvent? healed = null;
            _bus.Subscribe<HealAppliedEvent>(evt => healed = evt);

            _combat.ApplyHealing(new HealRequest(healer, wounded, 300f));

            Assert.That(healed.HasValue, Is.True);
            Assert.That(healed.Value.Result.Applied, Is.EqualTo(300f).Within(Tolerance));
            Assert.That(wounded.Health.Current, Is.EqualTo(900f).Within(Tolerance));
        }

        [Test]
        public void AnnounceAttack_PublishesTheStartOfTheSwing()
        {
            TestEntity attacker = Register("attacker", Faction.Raid);
            TestEntity victim = Register("victim", Faction.Enemy);
            AttackStartedEvent? started = null;
            _bus.Subscribe<AttackStartedEvent>(evt => started = evt);

            _combat.AnnounceAttack(attacker, victim, "Swing");

            Assert.That(started.HasValue, Is.True);
            Assert.That(started.Value.Source, Is.EqualTo(attacker.Id));
            Assert.That(started.Value.Target, Is.EqualTo(victim.Id));
        }

        [Test]
        public void ADeathListenerMayKillSomethingElse_WithoutBreakingDispatch()
        {
            // A chain reaction — an add that explodes on death — must not corrupt the event bus.
            TestEntity first = Register("first", Faction.Enemy, maxHealth: 10f);
            TestEntity second = Register("second", Faction.Enemy, maxHealth: 10f);
            var deaths = new List<EntityId>();
            _bus.Subscribe<EntityDiedEvent>(evt =>
            {
                deaths.Add(evt.Entity);
                if (evt.Entity == first.Id)
                {
                    _combat.Kill(second);
                }
            });

            _combat.Kill(first);

            Assert.That(deaths, Is.EqualTo(new[] { first.Id, second.Id }));
        }
    }
}
