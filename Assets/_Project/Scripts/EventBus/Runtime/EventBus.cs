using System;
using System.Collections.Generic;

namespace SheepyCreations.EventBus
{
    public static class EventBus<T> where T : IEvent
    {
        static readonly HashSet<IEventBinding<T>> bindings = new HashSet<IEventBinding<T>>();

        // Cached snapshot used by Raise, so it only allocates when bindings actually changed
        // since the last Raise instead of copying the whole set every single call.
        static IEventBinding<T>[] bindingsSnapshot = Array.Empty<IEventBinding<T>>();
        static bool bindingsSnapshotIsStale;

        static EventBus()
        {
            EventBusUtil.RegisterClearAction(Clear);
            EventBusUtil.RegisterBusInfo(new EventBusInfo(typeof(T), GetBindingCount, DescribeBindings));
        }

        public static void Register(EventBinding<T> binding)
        {
            if (binding == null)
            {
                throw new ArgumentNullException(nameof(binding));
            }

            EventBusUtil.WarnIfNotMainThread(nameof(Register));

            if (bindings.Add(binding))
            {
                bindingsSnapshotIsStale = true;
            }
        }

        public static void Deregister(EventBinding<T> binding)
        {
            if (binding == null)
            {
                return;
            }

            if (bindings.Remove(binding))
            {
                bindingsSnapshotIsStale = true;
            }
        }

        public static void Raise(T @event)
        {
            EventBusUtil.WarnIfNotMainThread(nameof(Raise));

#if UNITY_EDITOR
            EventBusUtil.RecordRaisedEvent(typeof(T));
#endif

            if (bindingsSnapshotIsStale)
            {
                bindingsSnapshot = new IEventBinding<T>[bindings.Count];
                bindings.CopyTo(bindingsSnapshot);
                bindingsSnapshotIsStale = false;
            }

            foreach (var binding in bindingsSnapshot)
            {
                // A binding may have been removed by an earlier callback in this same Raise,
                // or could still be pending removal from a stale snapshot after Clear().
                if (bindings.Contains(binding))
                {
                    binding.OnEvent.Invoke(@event);
                }
            }
        }

        static void Clear()
        {
            EventBusLog.Info($"Clearing {typeof(T).Name} bindings");
            bindings.Clear();
            bindingsSnapshotIsStale = true;
        }

        static int GetBindingCount() => bindings.Count;

        // Describes every subscribed method, one line per callback, for the Visual Debugger.
        static IEnumerable<string> DescribeBindings()
        {
            foreach (var binding in bindings)
            {
                Action<T> callback = binding.OnEvent;
                if (callback == null)
                {
                    continue;
                }

                foreach (var invocation in callback.GetInvocationList())
                {
                    string targetName = invocation.Target != null ? invocation.Target.GetType().Name : "Static";
                    yield return $"{targetName}.{invocation.Method.Name}()";
                }
            }
        }
    }
}