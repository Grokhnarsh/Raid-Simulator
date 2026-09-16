using System;
using System.Collections.Generic;
using RaidSim.Core.Diagnostics;

namespace RaidSim.Core.Events
{
    /// <summary>
    /// Default <see cref="IEventBus"/>: a synchronous, allocation-light, re-entrancy safe bus.
    /// </summary>
    /// <remarks>
    /// <para><b>Re-entrancy.</b> Handlers routinely publish further events (damage raises threat,
    /// threat raises a target change) and routinely unsubscribe while being invoked (an entity dies
    /// during the damage event it is handling). Dispatch therefore iterates a snapshot of the
    /// handler list and defers structural edits until the outermost publish has drained.</para>
    /// <para><b>Handler faults.</b> A throwing subscriber must not stop the others from seeing the
    /// event, or the combat log and the statistics collector would silently diverge from the fight.
    /// Exceptions are reported to the log sink and dispatch continues.</para>
    /// <para>The bus is not thread safe; the simulation ticks on one thread by design.</para>
    /// </remarks>
    public sealed class EventBus : IEventBus
    {
        private readonly Dictionary<Type, List<Delegate>> _handlers = new Dictionary<Type, List<Delegate>>();
        private readonly List<PendingEdit> _pendingEdits = new List<PendingEdit>();
        private readonly ISimLogSink _log;
        private int _dispatchDepth;

        public EventBus(ISimLogSink log = null)
        {
            _log = log ?? NullLogSink.Instance;
        }

        /// <summary>Number of distinct event types that currently have at least one subscriber.</summary>
        public int SubscribedTypeCount => _handlers.Count;

        public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : struct
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            ApplyOrDefer(new PendingEdit(typeof(TEvent), handler, add: true));
            return new Subscription(this, typeof(TEvent), handler);
        }

        public void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : struct
        {
            if (handler == null)
            {
                return;
            }

            ApplyOrDefer(new PendingEdit(typeof(TEvent), handler, add: false));
        }

        public void Publish<TEvent>(in TEvent payload) where TEvent : struct
        {
            if (!_handlers.TryGetValue(typeof(TEvent), out List<Delegate> handlers) || handlers.Count == 0)
            {
                return;
            }

            // Snapshot: handlers may subscribe or unsubscribe while this event is being delivered.
            int count = handlers.Count;
            Delegate[] snapshot = handlers.ToArray();

            _dispatchDepth++;
            try
            {
                for (int i = 0; i < count; i++)
                {
                    var typed = (Action<TEvent>)snapshot[i];
                    if (IsRemovalPending(typeof(TEvent), typed))
                    {
                        continue;
                    }

                    try
                    {
                        typed(payload);
                    }
                    catch (Exception exception)
                    {
                        _log.Error(
                            LogChannel.Events,
                            $"Handler for {typeof(TEvent).Name} threw: {exception}");
                    }
                }
            }
            finally
            {
                _dispatchDepth--;
                if (_dispatchDepth == 0)
                {
                    FlushPendingEdits();
                }
            }
        }

        public void Clear()
        {
            if (_dispatchDepth > 0)
            {
                _log.Warn(LogChannel.Events, "EventBus.Clear() called during dispatch; clearing anyway.");
            }

            _handlers.Clear();
            _pendingEdits.Clear();
        }

        private void ApplyOrDefer(PendingEdit edit)
        {
            if (_dispatchDepth > 0)
            {
                _pendingEdits.Add(edit);
                return;
            }

            Apply(edit);
        }

        private void Apply(PendingEdit edit)
        {
            if (edit.Add)
            {
                if (!_handlers.TryGetValue(edit.EventType, out List<Delegate> handlers))
                {
                    handlers = new List<Delegate>(4);
                    _handlers[edit.EventType] = handlers;
                }

                handlers.Add(edit.Handler);
                return;
            }

            if (_handlers.TryGetValue(edit.EventType, out List<Delegate> existing))
            {
                existing.Remove(edit.Handler);
                if (existing.Count == 0)
                {
                    _handlers.Remove(edit.EventType);
                }
            }
        }

        private bool IsRemovalPending(Type eventType, Delegate handler)
        {
            for (int i = 0; i < _pendingEdits.Count; i++)
            {
                PendingEdit edit = _pendingEdits[i];
                if (!edit.Add && edit.EventType == eventType && Equals(edit.Handler, handler))
                {
                    return true;
                }
            }

            return false;
        }

        private void FlushPendingEdits()
        {
            if (_pendingEdits.Count == 0)
            {
                return;
            }

            for (int i = 0; i < _pendingEdits.Count; i++)
            {
                Apply(_pendingEdits[i]);
            }

            _pendingEdits.Clear();
        }

        private readonly struct PendingEdit
        {
            public readonly Type EventType;
            public readonly Delegate Handler;
            public readonly bool Add;

            public PendingEdit(Type eventType, Delegate handler, bool add)
            {
                EventType = eventType;
                Handler = handler;
                Add = add;
            }
        }

        private sealed class Subscription : IDisposable
        {
            private EventBus _bus;
            private readonly Type _eventType;
            private readonly Delegate _handler;

            public Subscription(EventBus bus, Type eventType, Delegate handler)
            {
                _bus = bus;
                _eventType = eventType;
                _handler = handler;
            }

            public void Dispose()
            {
                if (_bus == null)
                {
                    return;
                }

                _bus.ApplyOrDefer(new PendingEdit(_eventType, _handler, add: false));
                _bus = null;
            }
        }
    }
}
