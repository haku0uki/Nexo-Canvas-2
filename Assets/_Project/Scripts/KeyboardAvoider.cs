using UnityEngine;
using UnityEngine.UIElements;

public class KeyboardAvoider : MonoBehaviour
{
    const string KeyboardOpenClass = "keyboard-open";

    [SerializeField] UIDocument document;
    [SerializeField] string targetName = "comment-composer";

    VisualElement target;
    float lastKeyboardHeight = -1f;

    void Update()
    {
        var root = document != null ? document.rootVisualElement : null;
        if (root == null)
            return;

        if (target == null || target.panel == null)
        {
            target = root.Q(targetName);
            lastKeyboardHeight = -1f;
            if (target == null)
                return;
        }

        float keyboardHeight = TouchScreenKeyboard.visible ? TouchScreenKeyboard.area.height : 0f;
        if (Mathf.Approximately(keyboardHeight, lastKeyboardHeight))
            return;

        float screenToPanel = root.layout.height / Mathf.Max(Screen.height, 1);
        if (float.IsNaN(screenToPanel))
            return;

        lastKeyboardHeight = keyboardHeight;
        target.style.bottom = keyboardHeight * screenToPanel;
        target.EnableInClassList(KeyboardOpenClass, keyboardHeight > 0f);
    }
}
