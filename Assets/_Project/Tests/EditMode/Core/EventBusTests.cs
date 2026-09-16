using System;
using System.Collections.Generic;
using NUnit.Framework;
using RaidSim.Core.Diagnostics;
using RaidSim.Core.Events;

namespace RaidSim.Tests.Core
{
    [TestFixture]
    public sealed class EventBusTests
    {
        private struct Ping
        {
            public int Value;
        }

        /// <summary>A second event type, used to prove events do not leak across types.</summary>
        private struct Pong
        {
        }

        private EventBus _bus;
        private CollectingLogSink _log;

        [SetUp]
        public void SetUp()
        {
            _log = new CollectingLogSink();
            _bus = new EventBus(_log);
        }

        [Test]
        public void Publish_ReachesEverySubscriberInOrder()
        {
            var order = new List<int>();
            _bus.Subscribe<Ping>(_ => order.Add(1));
            _bus.Subscribe<Ping>(_ => order.Add(2));
            _bus.Subscribe<Ping>(_ => order.Add(3));

            _bus.Publish(new Ping { Value = 7 });

            Assert.That(order, Is.EqualTo(new[] { 1, 2, 3 }));
        }

        [Test]
        public void Publish_DeliversOnlyToMatchingEventType()
        {
            int pings = 0;
            int pongs = 0;
            _bus.Subscribe<Ping>(_ => pings++);
            _bus.Subscribe<Pong>(_ => pongs++);

            _bus.Publish(new Ping());

            Assert.That(pings, Is.EqualTo(1));
            Assert.That(pongs, Is.Zero);
        }

        [Test]
        public void Publish_WithNoSubscribers_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _bus.Publish(new Ping()));
        }

        [Test]
        public void Publish_PassesPayloadThrough()
        {
            int received = 0;
            _bus.Subscribe<Ping>(evt => received = evt.Value);

            _bus.Publish(new Ping { Value = 42 });

            Assert.That(received, Is.EqualTo(42));
        }

        [Test]
        public void DisposingSubscription_StopsDelivery()
        {
            int calls = 0;
            IDisposable subscription = _bus.Subscribe<Ping>(_ => calls++);

            _bus.Publish(new Ping());
            subscription.Dispose();
            _bus.Publish(new Ping());

            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void DisposingSubscriptionTwice_IsSafe()
        {
            IDisposable subscription = _bus.Subscribe<Ping>(_ => { });

            subscription.Dispose();

            Assert.DoesNotThrow(subscription.Dispose);
        }

        [Test]
        public void Unsubscribe_RemovesHandler()
        {
            int calls = 0;
            Action<Ping> handler = _ => calls++;
            _bus.Subscribe(handler);

            _bus.Unsubscribe(handler);
            _bus.Publish(new Ping());

            Assert.That(calls, Is.Zero);
        }

        [Test]
        public void Unsubscribe_ForUnknownHandler_IsIgnored()
        {
            Assert.DoesNotThrow(() => _bus.Unsubscribe<Ping>(_ => { }));
        }

        [Test]
        public void SubscribingDuringDispatch_DoesNotAffectTheEventInFlight()
        {
            int lateCalls = 0;
            _bus.Subscribe<Ping>(_ => _bus.Subscribe<Ping>(__ => lateCalls++));

            _bus.Publish(new Ping());

            Assert.That(lateCalls, Is.Zero, "A handler added mid-dispatch must not see the event in flight.");

            _bus.Publish(new Ping());
            Assert.That(lateCalls, Is.EqualTo(1), "It must see the next event.");
        }

        [Test]
        public void UnsubscribingDuringDispatch_SuppressesTheHandlerImmediately()
        {
            // An entity dying inside a damage event unsubscribes itself; it must not be called again
            // during the same dispatch.
            int secondHandlerCalls = 0;
            Action<Ping> second = _ => secondHandlerCalls++;
            _bus.Subscribe<Ping>(_ => _bus.Unsubscribe(second));
            _bus.Subscribe(second);

            _bus.Publish(new Ping());

            Assert.That(secondHandlerCalls, Is.Zero);
        }

        [Test]
        public void ThrowingHandler_DoesNotStopTheOthers()
        {
            int reached = 0;
            _bus.Subscribe<Ping>(_ => throw new InvalidOperationException("boom"));
            _bus.Subscribe<Ping>(_ => reached++);

            _bus.Publish(new Ping());

            Assert.That(reached, Is.EqualTo(1));
            Assert.That(_log.CountOf(LogSeverity.Error), Is.EqualTo(1), "The fault must be reported, not swallowed.");
        }

        [Test]
        public void NestedPublish_IsDelivered()
        {
            int pongs = 0;
            _bus.Subscribe<Ping>(_ => _bus.Publish(new Pong()));
            _bus.Subscribe<Pong>(_ => pongs++);

            _bus.Publish(new Ping());

            Assert.That(pongs, Is.EqualTo(1));
        }

        [Test]
        public void Subscribe_WithNullHandler_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => _bus.Subscribe<Ping>(null));
        }

        [Test]
        public void Clear_RemovesEverySubscription()
        {
            int calls = 0;
            _bus.Subscribe<Ping>(_ => calls++);

            _bus.Clear();
            _bus.Publish(new Ping());

            Assert.That(calls, Is.Zero);
            Assert.That(_bus.SubscribedTypeCount, Is.Zero);
        }
    }
}
