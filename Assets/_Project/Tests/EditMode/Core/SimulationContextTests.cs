using System;
using System.Collections.Generic;
using NUnit.Framework;
using RaidSim.Core.Common;
using RaidSim.Core.Diagnostics;
using RaidSim.Core.GameFlow;
using RaidSim.Core.Simulation;

namespace RaidSim.Tests.Core
{
    [TestFixture]
    public sealed class SimulationContextTests
    {
        private CollectingLogSink _log;
        private SimulationContext _context;

        [SetUp]
        public void SetUp()
        {
            _log = new CollectingLogSink();
            _context = new SimulationContext(_log);
            _context.State.TransitionTo(GameState.Booting);
            _context.State.TransitionTo(GameState.Playing);
        }

        [TearDown]
        public void TearDown() => _context.Dispose();

        private sealed class RecordingSystem : ISimulationSystem
        {
            private readonly List<string> _log;
            private readonly string _name;

            public RecordingSystem(string name, int order, List<string> log)
            {
                _name = name;
                Order = order;
                _log = log;
            }

            public int Order { get; }

            public float TotalDelta { get; private set; }

            public void Tick(float deltaSeconds)
            {
                TotalDelta += deltaSeconds;
                _log.Add(_name);
            }
        }

        private sealed class ThrowingSystem : ISimulationSystem
        {
            public int Order => SystemOrder.Vitals;

            public void Tick(float deltaSeconds) => throw new InvalidOperationException("boom");
        }

        [Test]
        public void Tick_AdvancesEveryRegisteredSystem()
        {
            var order = new List<string>();
            var system = new RecordingSystem("only", SystemOrder.Vitals, order);
            _context.AddSystem(system);

            _context.Tick(0.1f);

            Assert.That(system.TotalDelta, Is.EqualTo(0.1f).Within(0.0001f));
        }

        [Test]
        public void Tick_RunsSystemsInTheDeclaredOrder_RegardlessOfRegistrationOrder()
        {
            var order = new List<string>();
            _context.AddSystem(new RecordingSystem("ai", SystemOrder.Ai, order));
            _context.AddSystem(new RecordingSystem("threat", SystemOrder.Threat, order));
            _context.AddSystem(new RecordingSystem("effects", SystemOrder.Effects, order));

            _context.Tick(0.1f);

            Assert.That(order, Is.EqualTo(new[] { "effects", "threat", "ai" }),
                "Threat must settle before AI reads the threat table.");
        }

        [Test]
        public void Tick_DoesNothingWhilePaused()
        {
            var order = new List<string>();
            _context.AddSystem(new RecordingSystem("only", SystemOrder.Vitals, order));
            _context.State.TransitionTo(GameState.Paused);

            Assert.That(_context.Tick(0.1f), Is.Zero);
            Assert.That(order, Is.Empty);
        }

        [Test]
        public void Tick_KeepsGoingWhenOneSystemThrows()
        {
            var order = new List<string>();
            _context.AddSystem(new ThrowingSystem());
            _context.AddSystem(new RecordingSystem("survivor", SystemOrder.Ai, order));

            _context.Tick(0.1f);

            Assert.That(order, Is.EqualTo(new[] { "survivor" }));
            Assert.That(_log.CountOf(LogSeverity.Error), Is.EqualTo(1));
        }

        [Test]
        public void AddSystem_IgnoresADuplicateInstance()
        {
            var order = new List<string>();
            var system = new RecordingSystem("only", SystemOrder.Vitals, order);

            _context.AddSystem(system);
            _context.AddSystem(system);
            _context.Tick(0.1f);

            Assert.That(order.Count, Is.EqualTo(1));
        }

        [Test]
        public void GetSystem_FindsARegisteredSystemByType()
        {
            var system = new RecordingSystem("only", SystemOrder.Vitals, new List<string>());
            _context.AddSystem(system);

            Assert.That(_context.GetSystem<RecordingSystem>(), Is.SameAs(system));
        }

        [Test]
        public void GetSystem_ReturnsNullWhenAbsent()
        {
            Assert.That(_context.GetSystem<RecordingSystem>(), Is.Null);
        }

        [Test]
        public void RemoveSystem_StopsItBeingTicked()
        {
            var order = new List<string>();
            var system = new RecordingSystem("only", SystemOrder.Vitals, order);
            _context.AddSystem(system);

            Assert.That(_context.RemoveSystem(system), Is.True);
            _context.Tick(0.1f);

            Assert.That(order, Is.Empty);
        }

        [Test]
        public void ResetEncounter_ClearsEntitiesAndTime_ButKeepsSystems()
        {
            var order = new List<string>();
            _context.AddSystem(new RecordingSystem("only", SystemOrder.Vitals, order));
            _context.Entities.Register(new TestEntity("aric", Faction.Raid));
            _context.Tick(0.5f);

            _context.ResetEncounter();

            Assert.That(_context.Entities.Count, Is.Zero);
            Assert.That(_context.Clock.Now, Is.Zero);
            Assert.That(_context.Systems.Count, Is.EqualTo(1));
        }

        [Test]
        public void Context_WiresTargetingAgainstItsOwnRegistry()
        {
            var player = new TestEntity("player", Faction.Raid);
            var enemy = new TestEntity("enemy", Faction.Enemy);
            _context.Entities.Register(player);
            _context.Entities.Register(enemy);

            Assert.That(
                _context.Targets.SelectNearest(player, RaidSim.Core.Targeting.TargetFilter.AnyHostile),
                Is.SameAs(enemy));
        }
    }
}
