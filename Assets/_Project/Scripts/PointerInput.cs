using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Shared "did the user just tap/click" check. Works with either input backend.
public static class PointerInput
{
    public static bool TryGetPointerDown(out Vector2 screenPosition)
    {
#if ENABLE_INPUT_SYSTEM
        Pointer pointer = Pointer.current;
        if (pointer != null && pointer.press.wasPressedThisFrame)
        {
            screenPosition = pointer.position.ReadValue();
            return true;
        }
#else
        if (Input.GetMouseButtonDown(0))
        {
            screenPosition = Input.mousePosition;
            return true;
        }
#endif
        screenPosition = default;
        return false;
    }
}
