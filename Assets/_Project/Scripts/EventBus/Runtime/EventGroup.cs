using System;
using System.Collections.Generic;

namespace SheepyCreations.EventBus
{
    /// <summary>
    /// Groups multiple EventBindings so they can all be cleaned up with a single call.
    /// Removes the need to manually deregister every event in OnDisable/OnDestroy.
    ///
    /// <code>
    /// EventGroup events = new EventGroup();
    ///
    /// void OnEnable()
    /// {
    ///     events.Subscribe&lt;PlayerDiedEvent&gt;(OnPlayerDied)
    ///           .Subscribe&lt;GamePausedEvent&gt;(OnGamePaused);
    /// }
    ///
    /// void OnDisable() => events.Clear();
    /// </code>
    /// </summary>
    [Serializable]
    public class EventGroup
    {
        // A List (not a HashSet) because we need to iterate in subscription order for Clear,
        // duplicates are already prevented by each EventBus's own HashSet, and groups are
        // typically small (under 10 bindings).
        readonly List<IEventBindingBase> bindings = new List<IEventBindingBase>();

        /// <summary>
        /// Number of events currently subscribed in this group.
        /// </summary>
        public int Count => bindings.Count;

        /// <summary>
        /// Subscribes to an event with a callback that receives the event data.
        /// </summary>
        /// <returns>This group, for fluent chaining.</returns>
        public EventGroup Subscribe<T>(Action<T> callback) where T : IEvent
        {
            var binding = new EventBinding<T>(callback);
            EventBus<T>.Register(binding);
            bindings.Add(binding);
            return this;
        }

        /// <summary>
        /// Subscribes to an event with a callback that takes no parameters.
        /// </summary>
        /// <returns>This group, for fluent chaining.</returns>
        public EventGroup Subscribe<T>(Action callback) where T : IEvent
        {
            var binding = new EventBinding<T>(callback);
            EventBus<T>.Register(binding);
            bindings.Add(binding);
            return this;
        }

        /// <summary>
        /// Registers a binding that was created outside the normal Subscribe methods,
        /// so Clear() also cleans it up. Used internally by EventBusExtensions.
        /// </summary>
        internal void Track(IEventBindingBase binding)
        {
            if (binding == null)
            {
                throw new ArgumentNullException(nameof(binding));
            }

            bindings.Add(binding);
        }

        /// <summary>
        /// Unsubscribes every binding of the given event type, leaving the rest of the group intact.
        /// </summary>
        public void Unsubscribe<T>() where T : IEvent
        {
            // Iterate backwards so we can remove entries while iterating.
            for (int i = bindings.Count - 1; i >= 0; i--)
            {
                if (bindings[i] is EventBinding<T>)
                {
                    bindings[i].RemoveFromBus();
                    bindings.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Unsubscribes every event in this group with a single call. Call this from
        /// OnDisable, OnDestroy, or wherever the owner of this group is torn down, to
        /// prevent leaked subscriptions and callbacks firing on destroyed objects.
        /// </summary>
        public void Clear()
        {
            foreach (var binding in bindings)
            {
                binding.RemoveFromBus();
            }

            bindings.Clear();
        }

        /// <summary>
        /// True if this group has at least one active subscription.
        /// </summary>
        public bool HasSubscriptions => bindings.Count > 0;

        /// <summary>
        /// Read-only access to the subscribed bindings, for debugging tools.
        /// </summary>
        public IReadOnlyList<IEventBindingBase> GetBindings() => bindings;
    }
}
