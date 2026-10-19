using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements;

public class DrawEntry : MonoBehaviour
{
    [Header("DRAW TEST")]
    [SerializeField] private DrawTest drawTest;

    [Header("UI DOCUMENT")]
    [SerializeField] private UIDocument uiDocument;

    [Header("PUBLISH")]
    [Tooltip(
        "Posición relativa del post-it en el tablero."
    )]
    [SerializeField] private Vector3 postItPosition = Vector3.zero;

    [Header("EVENTS")]
    [Tooltip(
        "Se invoca después de publicar. " +
        "Conéctalo para volver al HUD."
    )]
    public UnityEvent onPublished;

    [Tooltip(
        "Se invoca al cancelar. " +
        "Conéctalo para volver al HUD."
    )]
    public UnityEvent onCancelled;

    [Tooltip(
        "Se invoca cuando la celda elegida ya fue ocupada por otro usuario. " +
        "El dibujo se conserva para poder elegir otra celda."
    )]
    public UnityEvent onCellOccupied;

    private Slider brushSizeSlider;
    private Slider eraserSizeSlider;

    private Button blackColorButton;
    private Button redColorButton;
    private Button blueColorButton;
    private Button greenColorButton;

    private Button eraserButton;
    private Button undoButton;
    private Button redoButton;

    private Button publishButton;
    private Button cancelButton;

    private bool initialized;

    // ID de documento de la celda elegida (vacío = ID automático, comportamiento anterior).
    private string cellId;

    // Evita doble publicación mientras la transacción está en curso.
    private bool isPublishing;


    // ============================================================
    // UNITY
    // ============================================================

    private void OnEnable()
    {
        Initialize();
    }


    // ============================================================
    // PUBLIC API
    // ============================================================

    /// <summary>
    /// Define dónde queda el post-it en el tablero y, opcionalmente,
    /// la celda de la grilla que ocupará.
    /// Llamar antes de mostrar el panel.
    /// </summary>
    public void Configure(Vector3 newPostItPosition, string newCellId = null)
    {
        postItPosition = newPostItPosition;
        cellId = newCellId;
    }


    // ============================================================
    // AUTHOR
    // ============================================================

    // Solo se publica con una sesión iniciada. Igual que los textos,
    // el autor es el Email del usuario autenticado.
    private bool TryGetAuthorId(out string id)
    {
        id = null;

        if (
            AuthManager.Instance == null ||
            AuthManager.Instance.CurrentUser == null
        )
            return false;

        id = AuthManager.Instance.CurrentUser.Email;

        return !string.IsNullOrEmpty(id);
    }


    // ============================================================
    // INITIALIZE
    // ============================================================

    private void Initialize()
    {
        if (initialized)
            return;

        if (drawTest == null)
        {
            Debug.LogError(
                "DrawEntry: No hay DrawTest asignado."
            );

            return;
        }

        if (uiDocument == null)
        {
            uiDocument = GetComponent<UIDocument>();
        }

        if (uiDocument == null)
        {
            Debug.LogError(
                "DrawEntry: No hay UIDocument asignado."
            );

            return;
        }

        VisualElement root =
            uiDocument.rootVisualElement;

        if (root == null)
        {
            Debug.LogError(
                "DrawEntry: No se encontró rootVisualElement."
            );

            return;
        }


        // ========================================================
        // SLIDERS
        // ========================================================

        brushSizeSlider =
            root.Q<Slider>(
                "brush-size-slider"
            );

        eraserSizeSlider =
            root.Q<Slider>(
                "eraser-size-slider"
            );


        // ========================================================
        // COLOR BUTTONS
        // ========================================================

        blackColorButton =
            root.Q<Button>(
                "black-color-button"
            );

        redColorButton =
            root.Q<Button>(
                "red-color-button"
            );

        blueColorButton =
            root.Q<Button>(
                "blue-color-button"
            );

        greenColorButton =
            root.Q<Button>(
                "green-color-button"
            );


        // ========================================================
        // ACTION BUTTONS
        // ========================================================

        eraserButton =
            root.Q<Button>(
                "eraser-button"
            );

        undoButton =
            root.Q<Button>(
                "undo-button"
            );

        redoButton =
            root.Q<Button>(
                "redo-button"
            );

        publishButton =
            root.Q<Button>(
                "publish-button"
            );

        cancelButton =
            root.Q<Button>(
                "cancel-button"
            );


        // ========================================================
        // VALIDATE
        // ========================================================

        if (
            brushSizeSlider == null ||
            eraserSizeSlider == null ||
            blackColorButton == null ||
            redColorButton == null ||
            blueColorButton == null ||
            greenColorButton == null ||
            eraserButton == null ||
            undoButton == null ||
            redoButton == null ||
            publishButton == null ||
            cancelButton == null
        )
        {
            Debug.LogError(
                "DrawEntry: No se pudieron encontrar todos " +
                "los elementos del UXML."
            );

            return;
        }


        // ========================================================
        // SLIDER EVENTS
        // ========================================================

        brushSizeSlider.RegisterValueChangedCallback(
            OnBrushSizeChanged
        );

        eraserSizeSlider.RegisterValueChangedCallback(
            OnEraserSizeChanged
        );


        // ========================================================
        // COLOR EVENTS
        // ========================================================

        blackColorButton.clicked +=
            SetBlack;

        redColorButton.clicked +=
            SetRed;

        blueColorButton.clicked +=
            SetBlue;

        greenColorButton.clicked +=
            SetGreen;


        // ========================================================
        // ACTION EVENTS
        // ========================================================

        eraserButton.clicked +=
            ToggleEraser;

        undoButton.clicked +=
            Undo;

        redoButton.clicked +=
            Redo;

        publishButton.clicked +=
            Publish;

        cancelButton.clicked +=
            Cancel;


        // ========================================================
        // INITIAL VALUES
        // ========================================================

        drawTest.SetBrushWidth(
            brushSizeSlider.value
        );

        drawTest.SetEraserRadius(
            eraserSizeSlider.value
        );

        drawTest.SetBrushColor(
            Color.black
        );


        initialized = true;

        Debug.Log(
            "DrawEntry: UI conectada correctamente."
        );
    }


    // ============================================================
    // BRUSH WIDTH
    // ============================================================

    private void OnBrushSizeChanged(
        ChangeEvent<float> evt
    )
    {
        if (drawTest == null)
            return;

        drawTest.SetBrushWidth(
            evt.newValue
        );

        Debug.Log(
            "DrawEntry: Grosor = " +
            evt.newValue
        );
    }


    // ============================================================
    // ERASER SIZE
    // ============================================================

    private void OnEraserSizeChanged(
        ChangeEvent<float> evt
    )
    {
        if (drawTest == null)
            return;

        drawTest.SetEraserRadius(
            evt.newValue
        );

        Debug.Log(
            "DrawEntry: Goma = " +
            evt.newValue
        );
    }


    // ============================================================
    // COLORS
    // ============================================================

    private void SetBlack()
    {
        SetColor(Color.black);

        Debug.Log(
            "DrawEntry: Color negro."
        );
    }


    private void SetRed()
    {
        SetColor(Color.red);

        Debug.Log(
            "DrawEntry: Color rojo."
        );
    }


    private void SetBlue()
    {
        SetColor(Color.blue);

        Debug.Log(
            "DrawEntry: Color azul."
        );
    }


    private void SetGreen()
    {
        SetColor(Color.green);

        Debug.Log(
            "DrawEntry: Color verde."
        );
    }


    private void SetColor(Color color)
    {
        if (drawTest == null)
            return;

        drawTest.SetBrushColor(
            color
        );

        // Elegir un color vuelve al modo pincel.
        drawTest.SetEraserMode(
            false
        );
    }


    // ============================================================
    // ERASER
    // ============================================================

    private void ToggleEraser()
    {
        if (drawTest == null)
            return;

        drawTest.ToggleEraser();

        Debug.Log(
            "DrawEntry: Se cambió el modo goma."
        );
    }


    // ============================================================
    // UNDO
    // ============================================================

    private void Undo()
    {
        if (drawTest == null)
            return;

        drawTest.Undo();
    }


    // ============================================================
    // REDO
    // ============================================================

    private void Redo()
    {
        if (drawTest == null)
            return;

        drawTest.Redo();
    }


    // ============================================================
    // PUBLISH
    // ============================================================

    private void Publish()
    {
        if (isPublishing)
            return;

        if (drawTest == null)
            return;

        if (!drawTest.HasDrawing())
        {
            Debug.LogWarning(
                "DrawEntry: No hay nada dibujado para publicar."
            );

            return;
        }

        if (FirestoreService.Instance == null)
        {
            Debug.LogError(
                "DrawEntry: No existe FirestoreService en la escena."
            );

            return;
        }

        string id;

        if (!TryGetAuthorId(out id))
        {
            Debug.LogError(
                "DrawEntry: Hay que iniciar sesión para publicar. " +
                "El dibujo se conserva."
            );

            return;
        }

        isPublishing = true;

        bool sent =
            FirestoreService.Instance.PublicarPostIt(
                id,
                "dibujo",
                drawTest.GetDrawingJson(),
                postItPosition,
                cellId,
                OnPublishResult
            );

        // Si Firestore aún no está listo, el dibujo se conserva.
        if (!sent)
            isPublishing = false;
    }


    private void OnPublishResult(
        ResultadoPublicacion resultado
    )
    {
        isPublishing = false;

        switch (resultado)
        {
            case ResultadoPublicacion.Ok:

                Debug.Log(
                    "DrawEntry: Dibujo publicado en Firestore."
                );

                // Deja el lienzo limpio para el próximo dibujo.
                if (drawTest != null)
                {
                    drawTest.LoadDrawingData(
                        new DrawingData()
                    );
                }

                onPublished?.Invoke();
                break;

            case ResultadoPublicacion.CeldaOcupada:

                Debug.LogWarning(
                    "DrawEntry: La celda ya fue ocupada. " +
                    "El dibujo se conserva."
                );

                // No se limpia el dibujo: el usuario elige otra celda y publica de nuevo.
                onCellOccupied?.Invoke();
                break;

            case ResultadoPublicacion.Error:

                Debug.LogError(
                    "DrawEntry: Error al publicar. " +
                    "El dibujo se conserva."
                );

                break;
        }
    }


    // ============================================================
    // CANCEL
    // ============================================================

    private void Cancel()
    {
        Debug.Log(
            "DrawEntry: Cancelar."
        );

        if (drawTest != null)
        {
            drawTest.LoadDrawingData(
                new DrawingData()
            );
        }

        onCancelled?.Invoke();
    }


    // ============================================================
    // CLEANUP
    // ============================================================

    private void OnDisable()
    {
        if (!initialized)
            return;

        if (brushSizeSlider != null)
            brushSizeSlider.UnregisterValueChangedCallback(
                OnBrushSizeChanged
            );

        if (eraserSizeSlider != null)
            eraserSizeSlider.UnregisterValueChangedCallback(
                OnEraserSizeChanged
            );

        if (blackColorButton != null)
            blackColorButton.clicked -= SetBlack;

        if (redColorButton != null)
            redColorButton.clicked -= SetRed;

        if (blueColorButton != null)
            blueColorButton.clicked -= SetBlue;

        if (greenColorButton != null)
            greenColorButton.clicked -= SetGreen;

        if (eraserButton != null)
            eraserButton.clicked -= ToggleEraser;

        if (undoButton != null)
            undoButton.clicked -= Undo;

        if (redoButton != null)
            redoButton.clicked -= Redo;

        if (publishButton != null)
            publishButton.clicked -= Publish;

        if (cancelButton != null)
            cancelButton.clicked -= Cancel;

        initialized = false;
    }
}