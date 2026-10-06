using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SheepyCreations.EventBus.Editor
{
    /// <summary>
    /// Editor window that shows every active EventBus and its current subscribers in real time.
    /// Open it from Tools/SheepyCreations/EventBus/Visual Debugger.
    /// </summary>
    public class EventBusDebuggerWindow : EditorWindow
    {
        const double RefreshInterval = 0.5;

        Toggle autoRefreshToggle;
        Button clearAllBusesButton;
        Button subscriptionsTabButton;
        Button historyTabButton;
        Label playModeHintLabel;
        ScrollView eventListScrollView;
        ScrollView eventHistoryScrollView;
        Label summaryLabel;

        double lastRefreshTime;
        bool showingHistoryTab;

        readonly HashSet<Type> expandedEventTypes = new HashSet<Type>();

        [MenuItem("Tools/SheepyCreations/EventBus/Visual Debugger")]
        public static void ShowWindow()
        {
            var window = GetWindow<EventBusDebuggerWindow>("EventBus Debugger");
            window.minSize = new Vector2(400, 300);
        }

        public void CreateGUI()
        {
            var visualTree = LoadPackageAsset<VisualTreeAsset>(nameof(EventBusDebuggerWindow));
            if (visualTree == null)
            {
                rootVisualElement.Add(new Label($"Could not find {nameof(EventBusDebuggerWindow)}.uxml"));
                return;
            }

            visualTree.CloneTree(rootVisualElement);

            autoRefreshToggle = rootVisualElement.Q<Toggle>("autoRefreshToggle");
            clearAllBusesButton = rootVisualElement.Q<Button>("clearAllBusesButton");
            subscriptionsTabButton = rootVisualElement.Q<Button>("subscriptionsTabButton");
            historyTabButton = rootVisualElement.Q<Button>("historyTabButton");
            playModeHintLabel = rootVisualElement.Q<Label>("playModeHintLabel");
            eventListScrollView = rootVisualElement.Q<ScrollView>("eventListScrollView");
            eventHistoryScrollView = rootVisualElement.Q<ScrollView>("eventHistoryScrollView");
            summaryLabel = rootVisualElement.Q<Label>("summaryLabel");

            clearAllBusesButton.clicked += EventBusUtil.ClearAllBuses;
            subscriptionsTabButton.clicked += () => SetActiveTab(showHistory: false);
            historyTabButton.clicked += () => SetActiveTab(showHistory: true);

            lastRefreshTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += OnEditorUpdate;

            Refresh();
        }

        void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        void OnEditorUpdate()
        {
            if (autoRefreshToggle == null || !autoRefreshToggle.value)
            {
                return;
            }

            if (EditorApplication.timeSinceStartup - lastRefreshTime < RefreshInterval)
            {
                return;
            }

            lastRefreshTime = EditorApplication.timeSinceStartup;
            Refresh();
        }

        void SetActiveTab(bool showHistory)
        {
            showingHistoryTab = showHistory;
            subscriptionsTabButton.EnableInClassList("tab-button--active", !showHistory);
            historyTabButton.EnableInClassList("tab-button--active", showHistory);
            Refresh();
        }

        void Refresh()
        {
            clearAllBusesButton.SetEnabled(!Application.isPlaying);

            bool isPlaying = Application.isPlaying;
            playModeHintLabel.style.display = isPlaying ? DisplayStyle.None : DisplayStyle.Flex;
            summaryLabel.style.display = isPlaying ? DisplayStyle.Flex : DisplayStyle.None;
            eventListScrollView.style.display = isPlaying && !showingHistoryTab ? DisplayStyle.Flex : DisplayStyle.None;
            eventHistoryScrollView.style.display = isPlaying && showingHistoryTab ? DisplayStyle.Flex : DisplayStyle.None;

            if (!isPlaying)
            {
                return;
            }

            RefreshSubscriptions();
            RefreshHistory();
        }

        void RefreshSubscriptions()
        {
            eventListScrollView.Clear();

            int totalBindings = 0;
            int activeEventBuses = 0;

            foreach (var busInfo in EventBusUtil.RegisteredBuses.OrderBy(info => info.EventType.Name))
            {
                int count = busInfo.GetBindingCount();
                totalBindings += count;

                if (count > 0)
                {
                    activeEventBuses++;
                }

                eventListScrollView.Add(CreateEventRow(busInfo, count));
            }

            summaryLabel.text = $"{activeEventBuses} active event(s), {totalBindings} total binding(s)";
        }

        void RefreshHistory()
        {
            eventHistoryScrollView.Clear();

            var history = EventBusUtil.EventHistory;
            if (history.Count == 0)
            {
                eventHistoryScrollView.Add(new Label("No events raised yet."));
                return;
            }

            double now = EditorApplication.timeSinceStartup;

            // Most recent first.
            for (int index = history.Count - 1; index >= 0; index--)
            {
                var entry = history[index];
                double secondsAgo = now - entry.TimeSinceStartup;

                var line = new Label($"{secondsAgo:0.0}s ago  -  {entry.EventType.Name}");
                line.AddToClassList("history-line");
                eventHistoryScrollView.Add(line);
            }
        }

        VisualElement CreateEventRow(EventBusInfo busInfo, int bindingCount)
        {
            var foldout = new Foldout
            {
                text = $"{busInfo.EventType.Name} ({bindingCount} binding(s))",
                value = expandedEventTypes.Contains(busInfo.EventType)
            };

            foldout.AddToClassList("event-row");
            foldout.AddToClassList(GetRowStatusClass(bindingCount));

            foldout.RegisterValueChangedCallback(changeEvent =>
            {
                if (changeEvent.newValue)
                {
                    expandedEventTypes.Add(busInfo.EventType);
                }
                else
                {
                    expandedEventTypes.Remove(busInfo.EventType);
                }
            });

            var raiseButton = new Button(() => RaiseEventForDebugging(busInfo.EventType)) { text = "Raise" };
            raiseButton.AddToClassList("raise-button");
            foldout.Add(raiseButton);

            if (bindingCount == 0)
            {
                var noSubscribersLabel = new Label("No active subscribers.");
                noSubscribersLabel.AddToClassList("binding-line");
                foldout.Add(noSubscribersLabel);
                return foldout;
            }

            foreach (var description in busInfo.DescribeBindings())
            {
                var line = new Label(description);
                line.AddToClassList("binding-line");
                foldout.Add(line);
            }

            return foldout;
        }

        // Raises a default instance of the given event type, for manually testing subscribers
        // from the Editor without having to reach that situation in gameplay.
        static void RaiseEventForDebugging(Type eventType)
        {
            try
            {
                object eventInstance = Activator.CreateInstance(eventType);
                Type busType = typeof(EventBus<>).MakeGenericType(eventType);
                MethodInfo raiseMethod = busType.GetMethod("Raise", BindingFlags.Public | BindingFlags.Static);
                raiseMethod.Invoke(null, new[] { eventInstance });

                Debug.Log($"[EventBus] Manually raised {eventType.Name} with default values from the Visual Debugger.");
            }
            catch (Exception exception)
            {
                Debug.LogError($"[EventBus] Could not raise {eventType.Name}: {exception.Message}");
            }
        }

        static string GetRowStatusClass(int bindingCount)
        {
            if (bindingCount == 0) return "event-row--idle";
            if (bindingCount <= 2) return "event-row--low";
            if (bindingCount <= 5) return "event-row--medium";
            return "event-row--high";
        }

        /// <summary>
        /// Finds an asset by name and type anywhere in the project, so the package works
        /// regardless of which folder it was imported into.
        /// </summary>
        internal static T LoadPackageAsset<T>(string assetName) where T : UnityEngine.Object
        {
            string[] guids = AssetDatabase.FindAssets($"{assetName} t:{typeof(T).Name}");
            if (guids.Length == 0)
            {
                return null;
            }

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }
    }

    /// <summary>
    /// Custom Inspector field for EventGroup. Shows a hint pointing to the Visual Debugger,
    /// since the live subscription list only makes sense at runtime.
    /// </summary>
    [CustomPropertyDrawer(typeof(EventGroup))]
    public class EventGroupDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var label = new Label($"{property.displayName} (see Visual Debugger for live subscriptions)");
            label.AddToClassList("event-group-field-label");
            return label;
        }
    }
}
