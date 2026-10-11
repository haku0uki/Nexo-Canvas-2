using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Muestra un DrawingData (solo lectura) dentro de un elemento
/// de UI Toolkit. Reemplaza la versión con LineRenderers.
/// </summary>
public class DrawingRenderer : MonoBehaviour
{
    [Header("UI")]
    [Tooltip(
        "Si está vacío se busca en este mismo GameObject."
    )]
    [SerializeField]
    private UIDocument uiDocument;

    [Tooltip(
        "Nombre del VisualElement del UXML donde se muestra " +
        "el dibujo."
    )]
    [SerializeField]
    private string containerName = "drawing-view";

    [Header("DISPLAY")]
    [Tooltip(
        "Debe coincidir con el Canvas Width Units de DrawTest."
    )]
    [Min(0.1f)]
    [SerializeField]
    private float canvasWidthUnits = 10f;

    [Tooltip(
        "Centra y escala el dibujo para que quepa en el elemento."
    )]
    [SerializeField]
    private bool fitDrawing = true;

    [Min(0f)]
    [SerializeField]
    private float padding = 8f;

    private DrawingSurface surface;

    private DrawingData currentDrawing;


    // ============================================================
    // UNITY
    // ============================================================

    private void OnEnable()
    {
        TryAttach();
    }


    private void OnDisable()
    {
        Detach();
    }


    // ============================================================
    // PUBLIC API
    // ============================================================

    public void SetDrawing(DrawingData drawing)
    {
        currentDrawing = drawing;

        // Si la UI aún no está lista, el dibujo se muestra
        // apenas se conecte el contenedor.
        if (TryAttach())
            surface.SetDrawing(currentDrawing);
    }


    public void Clear()
    {
        currentDrawing = null;

        if (surface != null)
            surface.SetDrawing(null);
    }


    public DrawingData GetCurrentDrawing()
    {
        return currentDrawing;
    }


    /// <summary>
    /// Permite mostrar el dibujo en cualquier elemento creado por
    /// código (por ejemplo, tarjetas dentro de una lista).
    /// </summary>
    public void SetContainer(VisualElement container)
    {
        Detach();

        if (container == null)
            return;

        surface = new DrawingSurface();

        surface.ReadOnly = true;
        surface.UnitsWidth = canvasWidthUnits;
        surface.FitToContent = fitDrawing;
        surface.FitPadding = padding;

        surface.style.position = Position.Absolute;
        surface.style.left = 0;
        surface.style.top = 0;
        surface.style.right = 0;
        surface.style.bottom = 0;

        container.Add(surface);

        surface.SetDrawing(currentDrawing);
    }


    // ============================================================
    // INTERNAL
    // ============================================================

    private bool TryAttach()
    {
        if (surface != null)
            return true;

        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        if (uiDocument == null)
            return false;

        VisualElement root =
            uiDocument.rootVisualElement;

        if (root == null)
            return false;

        VisualElement container =
            root.Q<VisualElement>(containerName);

        if (container == null)
        {
            Debug.LogWarning(
                "DrawingRenderer: No se encontró el elemento '" +
                containerName + "' en el UXML."
            );

            return false;
        }

        SetContainer(container);

        return surface != null;
    }


    private void Detach()
    {
        if (surface == null)
            return;

        surface.RemoveFromHierarchy();

        surface = null;
    }
}
