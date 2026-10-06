using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SheepyCreations.EventBus.Editor
{
    /// <summary>
    /// Adds "Assets > Create > SheepyCreations > Event Listener". Picks an existing IEvent type
    /// from a dropdown and generates the one-line EventBusListener&lt;T&gt; subclass needed to
    /// use it as a component.
    /// </summary>
    public class EventListenerCreationWizard : EditorWindow
    {
        TextField classNameField;
        DropdownField eventTypeDropdown;
        Type[] availableEventTypes;

        [MenuItem("Assets/Create/SheepyCreations/Event Listener", priority = 2)]
        public static void ShowWindow()
        {
            var window = GetWindow<EventListenerCreationWizard>(true, "New Event Listener");
            window.minSize = new Vector2(320, 150);
            window.maxSize = new Vector2(320, 150);
        }

        public void CreateGUI()
        {
            availableEventTypes = AssemblyTypeFinder.GetTypes(typeof(IEvent))
                .OrderBy(eventType => eventType.Name)
                .ToArray();

            rootVisualElement.style.paddingLeft = 8;
            rootVisualElement.style.paddingRight = 8;
            rootVisualElement.style.paddingTop = 8;
            rootVisualElement.style.paddingBottom = 8;

            if (availableEventTypes.Length == 0)
            {
                rootVisualElement.Add(new Label(
                    "No event types found yet. Create one first with Assets > Create > SheepyCreations > Event."));
                return;
            }

            classNameField = new TextField("Class Name") { value = "NewEventListener" };
            rootVisualElement.Add(classNameField);

            var eventTypeNames = availableEventTypes.Select(eventType => eventType.Name).ToList();
            eventTypeDropdown = new DropdownField("Event Type", eventTypeNames, 0);
            rootVisualElement.Add(eventTypeDropdown);

            var createButton = new Button(CreateListenerScript) { text = "Create" };
            createButton.style.marginTop = 8;
            rootVisualElement.Add(createButton);
        }

        void CreateListenerScript()
        {
            string className = classNameField.value;
            if (string.IsNullOrWhiteSpace(className))
            {
                Debug.LogWarning("[EventBus] Enter a class name before creating the listener.");
                return;
            }

            Type eventType = availableEventTypes[eventTypeDropdown.index];
            string content = $"using SheepyCreations.EventBus;\n\npublic class {className} : EventBusListener<{eventType.Name}>\n{{\n}}\n";

            Type projectWindowUtilType = typeof(ProjectWindowUtil);

            MethodInfo getActiveFolderPathMethod = projectWindowUtilType.GetMethod(
                        "GetActiveFolderPath",
                        BindingFlags.Static | BindingFlags.NonPublic
                    );

            if (getActiveFolderPathMethod == null) return;

            string activePath = (string)getActiveFolderPathMethod.Invoke(null, null);
            string targetFolder = activePath;
            string path = AssetDatabase.GenerateUniqueAssetPath($"{targetFolder}/{className}.cs");

            File.WriteAllText(path, content);
            AssetDatabase.Refresh();

            var createdAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            EditorGUIUtility.PingObject(createdAsset);

            Close();
        }
    }
}
