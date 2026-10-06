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

        // Maps a no-args callback to the wrapper that was created for it, so Remove can find
        // and unhook the exact same wrapper the matching Add created.
        readonly Dictionary<Action, Action<T>> noArgsCallbackWrappers = new Dictionary<Action, Action<T>>();

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
            noArgsCallbackWrappers[callback] = wrapper;
            onEvent += wrapper;
        }

        public void Remove(Action callback)
        {
            if (callback == null)
            {
                return;
            }

            if (noArgsCallbackWrappers.TryGetValue(callback, out Action<T> wrapper))
            {
                onEvent -= wrapper;
                noArgsCallbackWrappers.Remove(callback);
            }
        }

        /// <summary>
        /// Removes this binding from its EventBus. Lets EventGroup clean up subscriptions
        /// without knowing the generic event type.
        /// </summary>
        public void RemoveFromBus() => EventBus<T>.Deregister(this);
    }
}
