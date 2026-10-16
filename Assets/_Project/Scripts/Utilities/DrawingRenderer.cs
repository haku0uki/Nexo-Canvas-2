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

    [Header("TEST")]
    [Tooltip(
        "Id del documento en la colección 'postits' de Firestore."
    )]
    [SerializeField]
    private string testPostItId;

    [Tooltip(
        "Alternativa sin red: pega aquí el campo 'contenido'."
    )]
    [TextArea(5, 15)]
    [SerializeField]
    private string testJson;

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


    // Cuando BoardCanvas instancia el prefab y llama a SetDrawing en el
    // mismo frame, el UIDocument puede no tener todavía su árbol listo.
    // Mientras haya un dibujo pendiente, se reintenta cada frame (en
    // silencio) hasta conectarlo.
    private void Update()
    {
        if (surface != null || currentDrawing == null)
            return;

        if (TryAttach(false))
            surface.SetDrawing(currentDrawing);
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
    // TEST
    // ============================================================

    /// <summary>
    /// Descarga un post-it de Firestore por su id y muestra
    /// su dibujo.
    /// </summary>
    public void LoadFromPostIt(string postItId)
    {
        if (FirestoreService.Instance == null)
        {
            Debug.LogError(
                "DrawingRenderer: No existe FirestoreService en la escena."
            );

            return;
        }

        FirestoreService.Instance.ObtenerPostIt(
            postItId,
            data =>
            {
                if (data == null)
                    return;

                if (data.tipo != "dibujo")
                {
                    Debug.LogWarning(
                        "DrawingRenderer: El post-it '" +
                        data.id +
                        "' es de tipo '" +
                        data.tipo +
                        "', no de dibujo."
                    );

                    return;
                }

                DrawingData drawing =
                    DrawingSerializer.Deserialize(data.contenido);

                if (
                    drawing == null ||
                    drawing.strokes == null ||
                    drawing.strokes.Count == 0
                )
                {
                    Debug.LogWarning(
                        "DrawingRenderer: El post-it '" +
                        data.id +
                        "' no tiene trazos."
                    );

                    return;
                }

                Debug.Log(
                    "DrawingRenderer: Mostrando " +
                    drawing.strokes.Count +
                    " trazos del post-it " +
                    data.id +
                    "."
                );

                SetDrawing(drawing);
            }
        );
    }


    [ContextMenu("Cargar post-it por id")]
    private void LoadTestPostIt()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "DrawingRenderer: Entra en Play para probar el dibujo."
            );

            return;
        }

        LoadFromPostIt(testPostItId);
    }


    [ContextMenu("Mostrar JSON de prueba")]
    private void ShowTestJson()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "DrawingRenderer: Entra en Play para probar el dibujo."
            );

            return;
        }

        DrawingData drawing;

        try
        {
            drawing =
                DrawingSerializer.Deserialize(testJson);
        }
        catch (System.Exception e)
        {
            Debug.LogError(
                "DrawingRenderer: El JSON de prueba no es válido.\n" +
                e.Message
            );

            return;
        }

        if (
            drawing == null ||
            drawing.strokes == null ||
            drawing.strokes.Count == 0
        )
        {
            Debug.LogWarning(
                "DrawingRenderer: El JSON de prueba no tiene trazos."
            );

            return;
        }

        Debug.Log(
            "DrawingRenderer: Mostrando " +
            drawing.strokes.Count +
            " trazos."
        );

        SetDrawing(drawing);
    }


    // ============================================================
    // INTERNAL
    // ============================================================

    private bool TryAttach(bool logIfMissing = true)
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
            if (logIfMissing)
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