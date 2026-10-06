using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SheepyCreations.EventBus
{
    /// <summary>
    /// Read-only information about one registered EventBus&lt;T&gt;, exposed for tooling
    /// (like the Visual Debugger) without needing reflection into private members.
    /// </summary>
    public readonly struct EventBusInfo
    {
        public readonly Type EventType;
        public readonly Func<int> GetBindingCount;
        public readonly Func<IEnumerable<string>> DescribeBindings;

        public EventBusInfo(Type eventType, Func<int> getBindingCount, Func<IEnumerable<string>> describeBindings)
        {
            EventType = eventType;
            GetBindingCount = getBindingCount;
            DescribeBindings = describeBindings;
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// One entry in the Visual Debugger's event history: which event type was raised, and when.
    /// </summary>
    public readonly struct EventHistoryEntry
    {
        public readonly Type EventType;
        public readonly double TimeSinceStartup;

        public EventHistoryEntry(Type eventType, double timeSinceStartup)
        {
            EventType = eventType;
            TimeSinceStartup = timeSinceStartup;
        }
    }
#endif

    /// <summary>
    /// Contains methods and properties related to event buses and event types in the Unity application.
    /// </summary>
    public static class EventBusUtil
    {
        public static IReadOnlyList<Type> EventTypes { get; private set; }
        public static IReadOnlyList<Type> EventBusTypes { get; private set; }
        public static IReadOnlyList<EventBusInfo> RegisteredBuses => registeredBuses;

        static readonly List<Action> clearActions = new List<Action>();
        static readonly List<EventBusInfo> registeredBuses = new List<EventBusInfo>();

        /// <summary>
        /// Called by each EventBus&lt;T&gt; static constructor to register its own clear action.
        /// This lets ClearAllBuses() reset every bus without reflecting into private members.
        /// </summary>
        internal static void RegisterClearAction(Action clearAction) => clearActions.Add(clearAction);

        /// <summary>
        /// Called by each EventBus&lt;T&gt; static constructor to register its own debug info,
        /// so tooling like the Visual Debugger can read it without reflection.
        /// </summary>
        internal static void RegisterBusInfo(EventBusInfo info) => registeredBuses.Add(info);

#if UNITY_EDITOR
        public static PlayModeStateChange PlayModeState { get; set; }

        const int MaxEventHistoryEntries = 100;
        static readonly List<EventHistoryEntry> eventHistory = new List<EventHistoryEntry>();

        /// <summary>
        /// The most recently raised events, oldest first, capped at 100 entries. Powers the
        /// Visual Debugger's History tab.
        /// </summary>
        public static IReadOnlyList<EventHistoryEntry> EventHistory => eventHistory;

        internal static void RecordRaisedEvent(Type eventType)
        {
            eventHistory.Add(new EventHistoryEntry(eventType, EditorApplication.timeSinceStartup));

            if (eventHistory.Count > MaxEventHistoryEntries)
            {
                eventHistory.RemoveAt(0);
            }
        }

        /// <summary>
        /// Runs every time scripts reload or Play Mode starts in the Editor, and clears all
        /// buses automatically when exiting Play Mode.
        /// </summary>
        [InitializeOnLoadMethod]
        public static void InitializeEditor()
        {
            mainThreadId = Thread.CurrentThread.ManagedThreadId;

            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            PlayModeState = state;
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                ClearAllBuses();
                eventHistory.Clear();
            }
        }
#endif

        /// <summary>
        /// Discovers every IEvent type and initializes its EventBus before any scene loads,
        /// in both Play Mode and builds.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            mainThreadId = Thread.CurrentThread.ManagedThreadId;

            clearActions.Clear();
            registeredBuses.Clear();
            EventTypes = AssemblyTypeFinder.GetTypes(typeof(IEvent));
            EventBusTypes = InitializeAllBuses();
        }

        static int mainThreadId;

        /// <summary>
        /// Logs a warning if called from a thread other than the one EventBus was initialized on.
        /// EventBus is not thread-safe, so cross-thread calls are a likely source of bugs.
        /// </summary>
        internal static void WarnIfNotMainThread(string callerName)
        {
            if (Thread.CurrentThread.ManagedThreadId != mainThreadId)
            {
                Debug.LogWarning($"[EventBus] {callerName} was called from a background thread. EventBus is not thread-safe.");
            }
        }

        static List<Type> InitializeAllBuses()
        {
            List<Type> eventBusTypes = new List<Type>();

            var typedef = typeof(EventBus<>);
            foreach (var eventType in EventTypes)
            {
                var busType = typedef.MakeGenericType(eventType);
                eventBusTypes.Add(busType);

                // Forces the static constructor to run now, so the bus registers its clear
                // action immediately instead of waiting for the first Register/Raise call.
                RuntimeHelpers.RunClassConstructor(busType.TypeHandle);

                EventBusLog.Info($"Initializing EventBus<{eventType.Name}>...");
            }

            return eventBusTypes;
        }

        /// <summary>
        /// Clears (removes all listeners from) all event buses in the application.
        /// </summary>
        public static void ClearAllBuses()
        {
            EventBusLog.Info("Clearing all buses...");
            foreach (var clearAction in clearActions)
            {
                clearAction();
            }
        }
    }
}
