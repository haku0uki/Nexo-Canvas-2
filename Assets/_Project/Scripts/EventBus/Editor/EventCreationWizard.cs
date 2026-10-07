using UnityEditor;
using UnityEngine;

namespace SheepyCreations.EventBus.Editor
{
    /// <summary>
    /// Adds "Assets > Create > SheepyCreations > Event", which generates a new IEvent struct
    /// from a template, the same way Unity generates a new MonoBehaviour script.
    /// </summary>
    public static class EventCreationWizard
    {
        const string TemplateAssetName = "NewEventTemplate";
        const string DefaultFileName = "NewEvent.cs";

        [MenuItem("Assets/Create/SheepyCreations/Eventbus/Event", priority = 1)]
        public static void CreateNewEvent()
        {
            string templatePath = FindTemplatePath();
            if (templatePath == null)
            {
                Debug.LogError($"[EventBus] Could not find {TemplateAssetName}.cs.txt in the project.");
                return;
            }

            ProjectWindowUtil.CreateScriptAssetFromTemplateFile(templatePath, DefaultFileName);
        }

        // Searches by name instead of a fixed path, so the wizard keeps working no matter
        // which folder this package was imported into.
        static string FindTemplatePath()
        {
            string[] guids = AssetDatabase.FindAssets($"{TemplateAssetName} t:TextAsset");
            if (guids.Length == 0)
            {
                return null;
            }

            return AssetDatabase.GUIDToAssetPath(guids[0]);
        }
    }
}
