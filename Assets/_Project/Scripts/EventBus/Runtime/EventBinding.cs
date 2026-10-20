using System;
using System.Collections.Generic;

namespace SheepyCreations.EventBus
{
    public interface IEventBinding<T>
    {
        Action<T> OnEvent { get; set; }
    }

    public class EventBinding<T> : IEventBinding<T>, IEventBindingBase where T : IEvent
    {
        Action<T> onEvent = _ => { };

        // Tracks every wrapper created for a no-args callback, so Remove can unhook the exact
        // wrapper that the matching Add created while still respecting delegate semantics when
        // the same callback is subscribed multiple times.
        readonly Dictionary<Action, Stack<Action<T>>> noArgsCallbackWrappers = new Dictionary<Action, Stack<Action<T>>>();

        Action<T> IEventBinding<T>.OnEvent
        {
            get => onEvent;
            set => onEvent = value;
        }

        public EventBinding(Action<T> onEvent)
        {
            this.onEvent = onEvent ?? throw new ArgumentNullException(nameof(onEvent));
        }

        public EventBinding(Action onEvent)
        {
            if (onEvent == null)
            {
                throw new ArgumentNullException(nameof(onEvent));
            }

            Add(onEvent);
        }

        public void Add(Action<T> callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            onEvent += callback;
        }

        public void Remove(Action<T> callback) => onEvent -= callback;

        public void Add(Action callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            Action<T> wrapper = _ => callback();
            if (!noArgsCallbackWrappers.TryGetValue(callback, out Stack<Action<T>> wrappers))
            {
                wrappers = new Stack<Action<T>>();
                noArgsCallbackWrappers[callback] = wrappers;
            }

            wrappers.Push(wrapper);
            onEvent += wrapper;
        }

        public void Remove(Action callback)
        {
            if (callback == null)
            {
                return;
            }

            if (noArgsCallbackWrappers.TryGetValue(callback, out Stack<Action<T>> wrappers) && wrappers.Count > 0)
            {
                Action<T> wrapper = wrappers.Pop();
                onEvent -= wrapper;

                if (wrappers.Count == 0)
                {
                    noArgsCallbackWrappers.Remove(callback);
                }
            }
        }

        /// <summary>
        /// Removes this binding from its EventBus. Lets EventGroup clean up subscriptions
        /// without knowing the generic event type.
        /// </summary>
        public void RemoveFromBus() => EventBus<T>.Deregister(this);
    }
}
