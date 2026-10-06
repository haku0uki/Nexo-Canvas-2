using System;
using UnityEngine;

namespace SheepyCreations.EventBus
{
    /// <summary>
    /// Extension methods that make the EventBus more intuitive and reduce boilerplate.
    /// </summary>
    public static class EventBusExtensions
    {
        // ============================================================================
        // MANUAL UNSUBSCRIBE (for bindings created by hand, outside EventGroup)
        // ============================================================================

        /// <summary>
        /// Unsubscribes a specific binding with fluent syntax: binding.Unsubscribe();
        /// </summary>
        public static void Unsubscribe<T>(this EventBinding<T> binding) where T : IEvent
        {
            if (binding == null)
            {
                return;
            }

            EventBus<T>.Deregister(binding);
        }

        // ============================================================================
        // RAISE EXTENSIONS (more natural syntax for raising events)
        // ============================================================================

        /// <summary>
        /// Raises an event from any object: this.RaiseEvent(new PlayerDiedEvent());
        /// </summary>
        public static void RaiseEvent<T>(this object source, T @event) where T : IEvent
        {
            EventBus<T>.Raise(@event);
        }

        /// <summary>
        /// Raises an empty struct event without instantiating it: this.RaiseEvent&lt;PlayerDiedEvent&gt;();
        /// </summary>
        public static void RaiseEvent<T>(this object source) where T : struct, IEvent
        {
            EventBus<T>.Raise(new T());
        }

        // ============================================================================
        // MONOBEHAVIOUR SHORTCUTS
        // ============================================================================

        /// <summary>
        /// Subscribes with automatic error logging, useful for catching broken callbacks during development.
        /// </summary>
        public static EventGroup SubscribeWithLogging<T>(this EventGroup group, MonoBehaviour mb, Action<T> callback) where T : IEvent
        {
            if (mb == null)
            {
                throw new ArgumentNullException(nameof(mb));
            }

            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            return group.Subscribe<T>(@event =>
            {
                try
                {
                    callback(@event);
                }
                catch (Exception exception)
                {
                    Debug.LogError($"[EventBus] Error in {mb.name}.{callback.Method.Name}: {exception.Message}", mb);
                }
            });
        }

        /// <summary>
        /// Subscribes but only invokes the callback while the GameObject is active in the hierarchy.
        /// </summary>
        public static EventGroup SubscribeIfActive<T>(this EventGroup group, MonoBehaviour mb, Action<T> callback) where T : IEvent
        {
            if (mb == null)
            {
                throw new ArgumentNullException(nameof(mb));
            }

            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            return group.Subscribe<T>(@event =>
            {
                if (mb != null && mb.gameObject.activeInHierarchy)
                {
                    callback(@event);
                }
            });
        }

        // ============================================================================
        // EVENTGROUP HELPERS
        // ============================================================================

        /// <summary>
        /// Subscribes the same callback to two event types.
        /// </summary>
        public static EventGroup SubscribeMultiple<T1, T2>(this EventGroup group, Action callback)
            where T1 : IEvent
            where T2 : IEvent
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            return group.Subscribe<T1>(callback)
                       .Subscribe<T2>(callback);
        }

        /// <summary>
        /// Subscribes the same callback to three event types.
        /// </summary>
        public static EventGroup SubscribeMultiple<T1, T2, T3>(this EventGroup group, Action callback)
            where T1 : IEvent
            where T2 : IEvent
            where T3 : IEvent
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            return group.Subscribe<T1>(callback)
                       .Subscribe<T2>(callback)
                       .Subscribe<T3>(callback);
        }

        /// <summary>
        /// Subscribes the same callback to four event types.
        /// </summary>
        public static EventGroup SubscribeMultiple<T1, T2, T3, T4>(this EventGroup group, Action callback)
            where T1 : IEvent
            where T2 : IEvent
            where T3 : IEvent
            where T4 : IEvent
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            return group.Subscribe<T1>(callback)
                       .Subscribe<T2>(callback)
                       .Subscribe<T3>(callback)
                       .Subscribe<T4>(callback);
        }

        // ============================================================================
        // DEBUG EXTENSIONS
        // ============================================================================

        /// <summary>
        /// Subscribes and logs every time the event fires, without touching existing code.
        /// </summary>
        public static EventGroup SubscribeWithDebugLog<T>(this EventGroup group, string logMessage, Action<T> callback) where T : IEvent
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            return group.Subscribe<T>(@event =>
            {
                Debug.Log($"[EventBus] {logMessage}: {typeof(T).Name}");
                callback(@event);
            });
        }

        /// <summary>
        /// Subscribes to an event that should only be handled once, then unsubscribes itself automatically.
        /// </summary>
        public static EventGroup SubscribeOnce<T>(this EventGroup group, Action<T> callback) where T : IEvent
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            EventBinding<T> binding = null;
            binding = new EventBinding<T>((T @event) =>
            {
                callback(@event);
                binding.RemoveFromBus();
            });

            EventBus<T>.Register(binding);
            group.Track(binding);
            return group;
        }

        // ============================================================================
        // CONDITIONAL SUBSCRIPTIONS
        // ============================================================================

        /// <summary>
        /// Subscribes, but only invokes the callback when the condition is true at the time the event fires.
        /// </summary>
        public static EventGroup SubscribeIf<T>(this EventGroup group, Func<bool> condition, Action<T> callback) where T : IEvent
        {
            if (condition == null)
            {
                throw new ArgumentNullException(nameof(condition));
            }

            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            return group.Subscribe<T>(@event =>
            {
                if (condition())
                {
                    callback(@event);
                }
            });
        }

        /// <summary>
        /// Subscribes with no event data, but only invokes the callback when the condition is true.
        /// </summary>
        public static EventGroup SubscribeIf<T>(this EventGroup group, Func<bool> condition, Action callback) where T : IEvent
        {
            if (condition == null)
            {
                throw new ArgumentNullException(nameof(condition));
            }

            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            return group.Subscribe<T>(() =>
            {
                if (condition())
                {
                    callback();
                }
            });
        }
    }
}
