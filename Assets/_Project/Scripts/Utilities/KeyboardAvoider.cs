using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Sube el contenido de un UIDocument cuando el teclado del movil taparia
/// el campo de texto con foco. Se agrega al mismo GameObject que el UIDocument
/// (login, registro, compositor de post-its...).
///
/// - Solo actua si el campo con foco pertenece a este UIDocument.
/// - Sube lo justo para que el campo (y los elementos de "Keep Visible Element Names",
///   por ejemplo el boton de confirmar) queden por encima del teclado.
/// - Nunca sube tanto como para sacar el campo por arriba de la pantalla.
/// - Mueve el elemento raiz con "translate", sin alterar el layout.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(UIDocument))]
public class KeyboardAvoider : MonoBehaviour
{
    [Header("Elementos que deben quedar visibles")]
    [Tooltip(
        "Nombres (name en el UXML) de elementos que conviene ver mientras se escribe, " +
        "por ejemplo el boton de iniciar sesion. Opcional."
    )]
    [SerializeField] private string[] keepVisibleElementNames;

    [Header("Ajustes (unidades del panel)")]
    [Tooltip("Espacio libre entre lo que se muestra y el borde superior del teclado.")]
    [SerializeField] private float gap = 24f;

    [Tooltip("Margen minimo que debe quedar entre el campo y el borde superior de la pantalla.")]
    [SerializeField] private float topMargin = 16f;

    [Tooltip(
        "Altura estimada del teclado (fraccion de la pantalla) cuando el dispositivo " +
        "no informa la real."
    )]
    [SerializeField, Range(0.2f, 0.6f)] private float fallbackKeyboardRatio = 0.38f;

    [Tooltip("Velocidad de la animacion. Mayor = mas rapido.")]
    [SerializeField] private float smoothing = 18f;

    [Tooltip("Segundos que se mantiene el desplazamiento si el teclado parpadea al cambiar de campo.")]
    [SerializeField] private float releaseDelay = 0.15f;

    [Header("Pruebas")]
    [Tooltip("Solo Editor: simula un teclado visible mientras un campo de texto tiene foco.")]
    [SerializeField] private bool simulateKeyboardInEditor;

    [Tooltip("Escribe en el log la altura del teclado y el desplazamiento al abrirse.")]
    [SerializeField] private bool logDebug;

    private UIDocument document;
    private VisualElement shiftedRoot;

    private float currentShift;
    private float lastTarget;
    private float lastKeyboardTime = -100f;
    private bool wasKeyboardVisible;

#if UNITY_ANDROID && !UNITY_EDITOR
    private bool androidQueryFailed;
    private float androidKeyboardPixels;
    private float nextAndroidQueryTime;
#endif


    // ============================================================
    // UNITY
    // ============================================================

    private void Awake()
    {
        document = GetComponent<UIDocument>();
    }


    private void OnDisable()
    {
        ResetShift();
    }


    private void Update()
    {
        if (document == null)
            return;

        VisualElement root = document.rootVisualElement;

        if (root == null || root.panel == null)
            return;

        // Si el UIDocument reconstruyo su arbol, se parte de cero.
        if (root != shiftedRoot)
        {
            ResetShift();
            shiftedRoot = root;
        }

        float target = ComputeTargetShift(root);

        if (Mathf.Abs(target - currentShift) < 0.5f)
        {
            currentShift = target;
        }
        else
        {
            float t = 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime);
            currentShift = Mathf.Lerp(currentShift, target, t);
        }

        ApplyShift(root, currentShift);
    }


    // ============================================================
    // CALCULO
    // ============================================================

    private float ComputeTargetShift(VisualElement root)
    {
        bool keyboardVisible = IsKeyboardVisible();

        if (keyboardVisible)
            lastKeyboardTime = Time.unscaledTime;

        bool inGracePeriod =
            !keyboardVisible &&
            Time.unscaledTime - lastKeyboardTime < releaseDelay;

        if (!keyboardVisible && !inGracePeriod)
        {
            wasKeyboardVisible = false;
            lastTarget = 0f;
            return 0f;
        }

        // Entre campo y campo el teclado puede parpadear: se mantiene lo anterior.
        if (!keyboardVisible)
            return lastTarget;

        TextField field = FindFocusedTextField(root);

        if (field == null)
        {
            lastTarget = 0f;
            return 0f;
        }

        float panelHeight = root.layout.height;

        if (panelHeight <= 0f || Screen.height <= 0)
            return lastTarget;

        float keyboardPixels = GetKeyboardHeightPixels();
        float keyboardPanel = keyboardPixels * (panelHeight / Screen.height);
        float limitBottom = panelHeight - keyboardPanel - gap;

        float fieldTop;
        float fieldBottom;

        if (!TryGetVerticalBounds(root, field, out fieldTop, out fieldBottom))
            return lastTarget;

        float requiredBottom = fieldBottom;

        if (keepVisibleElementNames != null)
        {
            foreach (string elementName in keepVisibleElementNames)
            {
                if (string.IsNullOrEmpty(elementName))
                    continue;

                VisualElement element = root.Q<VisualElement>(elementName);

                if (element == null)
                    continue;

                float top;
                float bottom;

                if (TryGetVerticalBounds(root, element, out top, out bottom))
                    requiredBottom = Mathf.Max(requiredBottom, bottom);
            }
        }

        float needed = requiredBottom - limitBottom;

        // Nunca sacar el campo con foco por arriba de la pantalla.
        float maxShift = Mathf.Max(0f, fieldTop - topMargin);

        lastTarget = Mathf.Clamp(needed, 0f, maxShift);

        if (logDebug && !wasKeyboardVisible)
        {
            Debug.Log(
                "KeyboardAvoider: teclado=" + keyboardPixels.ToString("0") +
                "px (pantalla " + Screen.height + "px), campo=" + field.name +
                ", desplazamiento=" + lastTarget.ToString("0.0")
            );
        }

        wasKeyboardVisible = true;

        return lastTarget;
    }


    // Posicion vertical del elemento respecto al borde superior del raiz,
    // sin el desplazamiento que este componente aplica (ambos lo incluyen y se cancela).
    private static bool TryGetVerticalBounds(
        VisualElement root,
        VisualElement element,
        out float top,
        out float bottom
    )
    {
        top = 0f;
        bottom = 0f;

        if (element.resolvedStyle.display == DisplayStyle.None)
            return false;

        Rect bound = element.worldBound;

        if (float.IsNaN(bound.yMin) || float.IsNaN(bound.yMax))
            return false;

        float rootTop = root.worldBound.y;

        top = bound.yMin - rootTop;
        bottom = bound.yMax - rootTop;

        return bound.height > 0f;
    }


    private static TextField FindFocusedTextField(VisualElement root)
    {
        IPanel panel = root.panel;

        if (panel == null || panel.focusController == null)
            return null;

        VisualElement focused =
            panel.focusController.focusedElement as VisualElement;

        for (VisualElement element = focused; element != null; element = element.parent)
        {
            TextField field = element as TextField;

            if (field != null)
                return root.Contains(field) ? field : null;
        }

        return null;
    }


    // ============================================================
    // TECLADO
    // ============================================================

    private bool IsKeyboardVisible()
    {
#if UNITY_EDITOR
        if (simulateKeyboardInEditor)
            return true;
#endif
        return TouchScreenKeyboard.visible;
    }


    private float GetKeyboardHeightPixels()
    {
        // iOS (y algunos Android) informan el area real.
        float area = TouchScreenKeyboard.area.height;

        if (area > 1f)
            return area;

#if UNITY_ANDROID && !UNITY_EDITOR
        // Android: se consulta la parte visible de la ventana.
        if (Time.unscaledTime >= nextAndroidQueryTime)
        {
            androidKeyboardPixels = QueryAndroidKeyboardPixels();
            nextAndroidQueryTime = Time.unscaledTime + 0.1f;
        }

        if (androidKeyboardPixels > 1f)
            return androidKeyboardPixels;
#endif

        return Screen.height * fallbackKeyboardRatio;
    }


#if UNITY_ANDROID && !UNITY_EDITOR
    private float QueryAndroidKeyboardPixels()
    {
        if (androidQueryFailed)
            return 0f;

        try
        {
            using (AndroidJavaClass player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaObject window = activity.Call<AndroidJavaObject>("getWindow"))
            using (AndroidJavaObject decor = window.Call<AndroidJavaObject>("getDecorView"))
            using (AndroidJavaObject rect = new AndroidJavaObject("android.graphics.Rect"))
            {
                decor.Call("getWindowVisibleDisplayFrame", rect);

                int visibleBottom = rect.Get<int>("bottom");

                return Mathf.Max(0f, Screen.height - visibleBottom);
            }
        }
        catch (System.Exception e)
        {
            androidQueryFailed = true;

            Debug.LogWarning(
                "KeyboardAvoider: no se pudo consultar la ventana de Android, " +
                "se usa la altura estimada. " + e.Message
            );

            return 0f;
        }
    }
#endif


    // ============================================================
    // APLICAR
    // ============================================================

    private static void ApplyShift(VisualElement root, float shift)
    {
        root.style.translate = new Translate(0f, -shift);
    }


    private void ResetShift()
    {
        currentShift = 0f;
        lastTarget = 0f;
        wasKeyboardVisible = false;

        if (shiftedRoot != null)
            ApplyShift(shiftedRoot, 0f);

        shiftedRoot = null;
    }
}