using System;
using System.Collections.Generic;
using RaidSim.Core.Diagnostics;
using RaidSim.Core.Entities;
using RaidSim.Core.Events;
using RaidSim.Core.GameFlow;
using RaidSim.Core.Targeting;

namespace RaidSim.Core.Simulation
{
    /// <summary>
    /// Everything one running encounter is made of, wired together in one place.
    /// </summary>
    /// <remarks>
    /// <para>This is the project's answer to the giant <c>GameManager</c>. It is a composition root
    /// and a tick scheduler, and nothing else: it owns no gameplay rules, makes no decisions, and
    /// every member it exposes is a system with its own single responsibility.</para>
    /// <para>Systems are registered rather than hard-referenced, so Phase 3's ability system and
    /// Phase 6's threat system join by calling <see cref="AddSystem"/> — the context does not change
    /// when the game grows.</para>
    /// <para>Because the context depends on nothing from Unity, an entire encounter can be built and
    /// run in a unit test or in the headless batch simulator.</para>
    /// </remarks>
    public sealed class SimulationContext : IDisposable
    {
        private readonly List<ISimulationSystem> _systems = new List<ISimulationSystem>();
        private bool _systemsNeedSorting;

        public SimulationContext(ISimLogSink log = null)
        {
            Log = log ?? NullLogSink.Instance;
            Clock = new SimulationClock();
            Events = new EventBus(Log);
            Entities = new EntityRegistry(Events);
            Targets = new TargetQuery(Entities);
            State = new GameStateMachine(Events, Clock, Log);
        }

        public ISimLogSink Log { get; }

        public SimulationClock Clock { get; }

        public IEventBus Events { get; }

        public EntityRegistry Entities { get; }

        public TargetQuery Targets { get; }

        public GameStateMachine State { get; }

        /// <summary>Systems currently registered, in tick order.</summary>
        public IReadOnlyList<ISimulationSystem> Systems
        {
            get
            {
                EnsureSorted();
                return _systems;
            }
        }

        /// <summary>
        /// Registers a system to be ticked. Registering the same instance twice is ignored.
        /// </summary>
        public void AddSystem(ISimulationSystem system)
        {
            if (system == null)
            {
                throw new ArgumentNullException(nameof(system));
            }

            if (_systems.Contains(system))
            {
                return;
            }

            _systems.Add(system);
            _systemsNeedSorting = true;
        }

        public bool RemoveSystem(ISimulationSystem system) => system != null && _systems.Remove(system);

        /// <summary>First registered system of type <typeparamref name="T"/>, or null.</summary>
        public T GetSystem<T>() where T : class, ISimulationSystem
        {
            for (int i = 0; i < _systems.Count; i++)
            {
                if (_systems[i] is T match)
                {
                    return match;
                }
            }

            return null;
        }

        /// <summary>
        /// Advances the whole simulation by <paramref name="unscaledDeltaSeconds"/> of real time.
        /// Does nothing while the clock is paused.
        /// </summary>
        /// <returns>The simulation delta that was applied.</returns>
        public float Tick(float unscaledDeltaSeconds)
        {
            float delta = Clock.Advance(unscaledDeltaSeconds);
            if (delta <= 0f)
            {
                return 0f;
            }

            EnsureSorted();
            for (int i = 0; i < _systems.Count; i++)
            {
                ISimulationSystem system = _systems[i];
                try
                {
                    system.Tick(delta);
                }
                catch (Exception exception)
                {
                    // One broken system must not take the encounter down with it; the others still
                    // need to tick so the fight stays observable and the failure stays diagnosable.
                    Log.Error(LogChannel.Simulation, $"{system.GetType().Name}.Tick threw: {exception}");
                }
            }

            return delta;
        }

        /// <summary>
        /// Returns the context to a pre-encounter state: no entities, no subscriptions, clock at
        /// zero. Registered systems are kept.
        /// </summary>
        public void ResetEncounter()
        {
            Entities.Clear();
            Events.Clear();
            Clock.Reset();
        }

        public void Dispose()
        {
            _systems.Clear();
            Entities.Clear();
            Events.Clear();
        }

        private void EnsureSorted()
        {
            if (!_systemsNeedSorting)
            {
                return;
            }

            _systems.Sort((a, b) => a.Order.CompareTo(b.Order));
            _systemsNeedSorting = false;
        }
    }
}
