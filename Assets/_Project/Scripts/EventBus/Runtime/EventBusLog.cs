using UnityEngine;

namespace SheepyCreations.EventBus
{
    /// <summary>
    /// Small logging helper used internally by the EventBus package.
    /// Keeps the package self-contained instead of depending on an external utility library
    /// just to color a couple of debug messages.
    /// </summary>
    internal static class EventBusLog
    {
        public static void Info(string message)
        {
            Debug.Log($"<color=#{ColorUtility.ToHtmlStringRGB(Color.green)}>[EventBus]</color> {message}");
        }
    }
}
