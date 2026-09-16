using System;

namespace RaidSim.Core.Events
{
    /// <summary>
    /// Typed publish/subscribe channel used for all cross-system communication.
    /// </summary>
    /// <remarks>
    /// Systems never call each other directly. The ability system raises a cast event, the combat
    /// system raises a damage event, and threat, UI, the combat log and the statistics collector
    /// each subscribe to what they care about. Adding a new listener must never require editing the
    /// publisher. See <c>ARCHITECTURE.md</c>, "Event architecture".
    /// </remarks>
    public interface IEventBus
    {
        /// <summary>
        /// Registers <paramref name="handler"/> for events of type <typeparamref name="TEvent"/>.
        /// </summary>
        /// <returns>
        /// A token that unsubscribes when disposed. Disposing is the supported way to unsubscribe;
        /// it is safe to dispose more than once and safe to dispose from inside a handler.
        /// </returns>
        IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : struct;

        /// <summary>Removes a previously registered handler. No-op when it was never registered.</summary>
        void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : struct;

        /// <summary>
        /// Delivers <paramref name="payload"/> to every current subscriber, in subscription order.
        /// </summary>
        void Publish<TEvent>(in TEvent payload) where TEvent : struct;

        /// <summary>Drops every subscription. Used when tearing an encounter down.</summary>
        void Clear();
    }
}
