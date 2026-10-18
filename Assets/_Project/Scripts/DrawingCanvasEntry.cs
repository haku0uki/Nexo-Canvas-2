using SheepyCreations.UIControl.Runtime;
using UnityEngine;
using UnityEngine.UIElements;

// Equivalente de TextCanvasEntry para post-its de tipo "dibujo".
// Mismo ciclo de vida: espera a que el panel y las etiquetas est�n listos,
// muestra el autor, muestra el panel y registra el toque.
public class DrawingCanvasEntry : BaseCanvasEntry
{
    [Header("PANEL")]
    [Tooltip("Si queda vac�o se busca en este mismo prefab.")]
    [SerializeField] private UIDocumentPanel panel;
    [Tooltip("Etiqueta del autor (username-label-postit). Si queda vac�a se usa el primer LabelTextBridge del prefab.")]
    [SerializeField] private LabelTextBridge usernameLabel;
    [Tooltip("Opcional. Si se asigna, se vac�a: el JSON del dibujo no debe verse como texto.")]
    [SerializeField] private LabelTextBridge contentLabel;

    [Header("DRAWING")]
    [SerializeField] private DrawingRenderer drawingRenderer;

    private VisualElement clickRoot;
    private bool pendingPresentation;

    // BoardCanvas llama a Populate con TODOS los post-its cada vez que
    // Firestore reporta un cambio. Se recuerda el �ltimo contenido para no
    // volver a deserializar y repintar un dibujo que no cambi�.
    private string lastContent;
    private bool hasContent;


    // ============================================================
    // UNITY
    // ============================================================

    private void Awake()
    {
        if (panel == null)
            panel = GetComponentInChildren<UIDocumentPanel>(true);

        if (usernameLabel == null)
            usernameLabel = GetComponentInChildren<LabelTextBridge>(true);
    }


    private void Update()
    {
        if (pendingPresentation)
            TryPresentData();
    }


    private void OnDestroy()
    {
        if (clickRoot != null)
            clickRoot.UnregisterCallback<ClickEvent>(OnEntryClick);
    }


    // ============================================================
    // POPULATE
    // ============================================================

    public override void Populate(PostItData data)
    {
        if (data == null)
            return;

        base.Populate(data); // Posici�n (y datos base).

        pendingPresentation = true;
        TryPresentData();

        UpdateDrawing(data);
    }


    // ============================================================
    // PRESENTATION (autor, panel visible, toque)
    // ============================================================

    private void TryPresentData()
    {
        if (Data == null || panel == null || !panel.IsInitialized)
            return;

        VisualElement root = panel.RootVisualElement;

        if (root == null || root.panel == null)
            return;

        if (usernameLabel != null && !usernameLabel.IsBound)
            return;

        if (contentLabel != null && !contentLabel.IsBound)
            return;

        if (usernameLabel != null)
            usernameLabel.SetText(Data.authorId);

        if (contentLabel != null)
            contentLabel.SetText(string.Empty);

        panel.Show();

        if (clickRoot != root)
        {
            if (clickRoot != null)
                clickRoot.UnregisterCallback<ClickEvent>(OnEntryClick);

            clickRoot = root;
            clickRoot.RegisterCallback<ClickEvent>(OnEntryClick);
        }

        pendingPresentation = false;
    }


    private void OnEntryClick(ClickEvent evt)
    {
        RaiseClicked();
    }


    // ============================================================
    // DRAWING
    // ============================================================

    private void UpdateDrawing(PostItData data)
    {
        if (hasContent && data.contenido == lastContent)
            return;

        if (drawingRenderer == null)
        {
            Debug.LogError(
                "DrawingCanvasEntry: " +
                "No hay DrawingRenderer asignado."
            );

            return;
        }

        hasContent = true;
        lastContent = data.contenido;

        if (string.IsNullOrEmpty(data.contenido))
        {
            Debug.LogWarning(
                "DrawingCanvasEntry: " +
                "El dibujo no tiene contenido."
            );

            drawingRenderer.Clear();
            return;
        }

        DrawingData drawing =
            DrawingSerializer.Deserialize(
                data.contenido
            );

        if (drawing == null)
        {
            Debug.LogWarning(
                "DrawingCanvasEntry: " +
                "No se pudo deserializar el dibujo."
            );

            drawingRenderer.Clear();
            return;
        }

        drawingRenderer.SetDrawing(
            drawing
        );
    }
}