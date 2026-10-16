using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class DrawTest : MonoBehaviour
{
    // ============================================================
    // REFERENCES
    // ============================================================

    [Header("UI DOCUMENT")]
    [Tooltip(
        "Si está vacío se busca en este mismo GameObject."
    )]
    [SerializeField] private UIDocument uiDocument;

    [Tooltip(
        "Nombre del VisualElement del UXML dentro del cual " +
        "se crea la superficie de dibujo."
    )]
    [SerializeField] private string drawingAreaName = "drawing-area";


    // ============================================================
    // CANVAS
    // ============================================================

    [Header("CANVAS")]
    [Tooltip(
        "Ancho del lienzo en unidades de dibujo. " +
        "Brush, goma y distancia mínima se expresan en estas " +
        "unidades, así el dibujo se ve igual en cualquier " +
        "resolución."
    )]
    [Min(0.1f)]
    [SerializeField] private float canvasWidthUnits = 10f;


    // ============================================================
    // BRUSH
    // ============================================================

    [Header("BRUSH")]
    public float brushWidth = 0.05f;

    public Color brushColor = Color.black;


    // ============================================================
    // POINT FILTERING
    // ============================================================

    [Header("POINT FILTERING")]
    [Tooltip(
        "Distancia mínima entre puntos consecutivos."
    )]
    public float minPointDistance = 0.02f;


    // ============================================================
    // ERASER
    // ============================================================

    [Header("ERASER")]
    [Tooltip(
        "Radio de la goma en unidades del lienzo."
    )]
    public float eraserRadius = 0.15f;


    // ============================================================
    // UNDO / REDO
    // ============================================================

    [Header("UNDO / REDO")]
    [Min(1)]
    public int maxUndoSteps = 30;

    private List<DrawingData> undoHistory =
        new List<DrawingData>();

    private List<DrawingData> redoHistory =
        new List<DrawingData>();


    // ============================================================
    // INTERNAL STATE
    // ============================================================

    private DrawingData currentDrawing =
        new DrawingData();

    private DrawingSurface surface;

    private StrokeData currentStroke;

    private Vector2 lastPoint;

    private bool eraserMode = false;

    private bool eraserActionStarted = false;


    // ============================================================
    // UNITY
    // ============================================================

    private void OnEnable()
    {
        TryAttachSurface(false);
    }


    private void Start()
    {
        // Si el UXML aún no estaba listo en OnEnable.
        if (surface == null)
            TryAttachSurface(true);
    }


    private void OnDisable()
    {
        DetachSurface();
    }


    // ============================================================
    // SURFACE
    // ============================================================

    private void TryAttachSurface(bool logErrors)
    {
        if (surface != null)
            return;

        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        if (uiDocument == null)
        {
            if (logErrors)
                Debug.LogError(
                    "DrawTest: No hay UIDocument asignado."
                );

            return;
        }

        VisualElement root =
            uiDocument.rootVisualElement;

        if (root == null)
            return;

        VisualElement area =
            root.Q<VisualElement>(drawingAreaName);

        if (area == null)
        {
            if (logErrors)
                Debug.LogError(
                    "DrawTest: No se encontró el elemento '" +
                    drawingAreaName + "' en el UXML."
                );

            return;
        }

        surface = new DrawingSurface();

        surface.UnitsWidth = canvasWidthUnits;

        // Ocupa todo el área.
        surface.style.position = Position.Absolute;
        surface.style.left = 0;
        surface.style.top = 0;
        surface.style.right = 0;
        surface.style.bottom = 0;

        surface.StrokeStarted += OnStrokeStarted;
        surface.StrokeMoved += OnStrokeMoved;
        surface.StrokeEnded += OnStrokeEnded;

        area.Add(surface);

        surface.SetDrawing(currentDrawing);

        Debug.Log(
            "DrawTest: superficie de dibujo conectada."
        );
    }


    private void DetachSurface()
    {
        if (surface == null)
            return;

        surface.StrokeStarted -= OnStrokeStarted;
        surface.StrokeMoved -= OnStrokeMoved;
        surface.StrokeEnded -= OnStrokeEnded;

        surface.RemoveFromHierarchy();

        surface = null;

        currentStroke = null;
        eraserActionStarted = false;
    }


    private void RefreshSurface()
    {
        if (surface != null)
            surface.Refresh();
    }


    // ============================================================
    // INPUT CALLBACKS (posiciones en unidades de dibujo)
    // ============================================================

    private void OnStrokeStarted(Vector2 position)
    {
        if (eraserMode)
        {
            StartErase();

            EraseAtPosition(position);
        }
        else
        {
            StartLine(position);
        }
    }


    private void OnStrokeMoved(Vector2 position)
    {
        if (eraserMode)
        {
            EraseAtPosition(position);
        }
        else
        {
            ContinueLine(position);
        }
    }


    private void OnStrokeEnded()
    {
        if (eraserMode)
        {
            FinishErase();
        }
        else
        {
            FinishLine();
        }
    }


    // ============================================================
    // DRAWING
    // ============================================================

    private void StartLine(Vector2 position)
    {
        SaveUndoState();

        currentStroke = new StrokeData();

        currentStroke.width = brushWidth;

        currentStroke.colorHex =
            ColorUtility.ToHtmlStringRGBA(brushColor);

        currentStroke.points.Add(
            new Vector2Serialized(position.x, position.y)
        );

        currentDrawing.strokes.Add(currentStroke);

        lastPoint = position;

        RefreshSurface();
    }


    private void ContinueLine(Vector2 position)
    {
        if (currentStroke == null)
            return;

        // ----------------------------------------------------
        // MINIMUM POINT DISTANCE
        // ----------------------------------------------------

        if (Vector2.Distance(position, lastPoint) <
            minPointDistance)
        {
            return;
        }

        currentStroke.points.Add(
            new Vector2Serialized(position.x, position.y)
        );

        lastPoint = position;

        RefreshSurface();
    }


    private void FinishLine()
    {
        currentStroke = null;
    }


    // ============================================================
    // ERASER
    // ============================================================

    private void StartErase()
    {
        eraserActionStarted = false;
    }


    private void FinishErase()
    {
        eraserActionStarted = false;
    }


    private void EraseAtPosition(Vector2 position)
    {
        List<StrokeData> result =
            new List<StrokeData>();

        bool anyHit = false;

        foreach (StrokeData stroke in currentDrawing.strokes)
        {
            if (stroke == null ||
                stroke.points == null ||
                stroke.points.Count == 0)
            {
                continue;
            }

            List<StrokeData> pieces =
                new List<StrokeData>();

            if (SplitStrokeByEraser(stroke, position, pieces))
            {
                anyHit = true;

                result.AddRange(pieces);
            }
            else
            {
                result.Add(stroke);
            }
        }

        if (!anyHit)
            return;

        // Una pasada completa de goma cuenta como una acción.
        // (currentDrawing aún no fue modificado.)
        if (!eraserActionStarted)
        {
            SaveUndoState();

            eraserActionStarted = true;
        }

        currentDrawing.strokes = result;

        RefreshSurface();
    }


    // ============================================================
    // SPLIT STROKE
    // ============================================================

    /// <summary>
    /// Quita los segmentos que toca la goma y devuelve los
    /// trozos restantes en 'output'. Devuelve false si la goma
    /// no tocó el trazo.
    /// </summary>
    private bool SplitStrokeByEraser(
        StrokeData stroke,
        Vector2 eraserPosition,
        List<StrokeData> output
    )
    {
        List<Vector2Serialized> points = stroke.points;

        // Punto suelto (toque simple).
        if (points.Count == 1)
        {
            return Vector2.Distance(
                ToVector2(points[0]),
                eraserPosition
            ) <= eraserRadius;
        }

        List<StrokeData> parts =
            new List<StrokeData>();

        List<Vector2Serialized> part =
            new List<Vector2Serialized> { points[0] };

        bool hit = false;

        for (int i = 0; i < points.Count - 1; i++)
        {
            float distance =
                DistancePointToSegment(
                    eraserPosition,
                    ToVector2(points[i]),
                    ToVector2(points[i + 1])
                );

            if (distance <= eraserRadius)
            {
                hit = true;

                AddPart(parts, part, stroke);

                part =
                    new List<Vector2Serialized>
                    {
                        points[i + 1]
                    };
            }
            else
            {
                part.Add(points[i + 1]);
            }
        }

        if (!hit)
            return false;

        AddPart(parts, part, stroke);

        output.AddRange(parts);

        return true;
    }


    private void AddPart(
        List<StrokeData> parts,
        List<Vector2Serialized> points,
        StrokeData style
    )
    {
        if (points.Count < 2)
            return;

        StrokeData stroke = new StrokeData();

        stroke.width = style.width;
        stroke.colorHex = style.colorHex;
        stroke.points = points;

        parts.Add(stroke);
    }


    // ============================================================
    // POINT -> SEGMENT DISTANCE
    // ============================================================

    private float DistancePointToSegment(
        Vector2 point,
        Vector2 segmentStart,
        Vector2 segmentEnd
    )
    {
        Vector2 segment =
            segmentEnd - segmentStart;

        float segmentLengthSquared =
            segment.sqrMagnitude;

        if (segmentLengthSquared == 0f)
        {
            return Vector2.Distance(
                point,
                segmentStart
            );
        }

        float t =
            Vector2.Dot(
                point - segmentStart,
                segment
            )
            / segmentLengthSquared;

        t = Mathf.Clamp01(t);

        Vector2 closestPoint =
            segmentStart + segment * t;

        return Vector2.Distance(
            point,
            closestPoint
        );
    }


    private static Vector2 ToVector2(Vector2Serialized p)
    {
        return new Vector2(p.x, p.y);
    }


    // ============================================================
    // UNDO
    // ============================================================

    public void Undo()
    {
        if (undoHistory.Count == 0)
        {
            Debug.Log(
                "DrawTest: No hay acciones para deshacer."
            );

            return;
        }

        // --------------------------------------------------------
        // CURRENT -> REDO

        redoHistory.Add(
            CloneDrawingData(currentDrawing)
        );

        // --------------------------------------------------------
        // PREVIOUS STATE

        int lastIndex =
            undoHistory.Count - 1;

        DrawingData previousState =
            undoHistory[lastIndex];

        undoHistory.RemoveAt(lastIndex);

        // --------------------------------------------------------
        // RESTORE

        LoadDrawingDataInternal(previousState);
    }


    // ============================================================
    // REDO
    // ============================================================

    public void Redo()
    {
        if (redoHistory.Count == 0)
        {
            Debug.Log(
                "DrawTest: No hay acciones para rehacer."
            );

            return;
        }

        // --------------------------------------------------------
        // CURRENT -> UNDO

        undoHistory.Add(
            CloneDrawingData(currentDrawing)
        );

        LimitUndoHistory();

        // --------------------------------------------------------
        // NEXT STATE

        int lastIndex =
            redoHistory.Count - 1;

        DrawingData nextState =
            redoHistory[lastIndex];

        redoHistory.RemoveAt(lastIndex);

        // --------------------------------------------------------
        // RESTORE

        LoadDrawingDataInternal(nextState);
    }


    // ============================================================
    // SAVE UNDO STATE
    // ============================================================

    private void SaveUndoState()
    {
        undoHistory.Add(
            CloneDrawingData(currentDrawing)
        );

        // Una acción nueva invalida Redo.
        redoHistory.Clear();

        LimitUndoHistory();
    }


    // ============================================================
    // LIMIT HISTORY
    // ============================================================

    private void LimitUndoHistory()
    {
        if (maxUndoSteps < 1)
        {
            undoHistory.Clear();

            return;
        }

        while (undoHistory.Count > maxUndoSteps)
        {
            undoHistory.RemoveAt(0);
        }
    }


    // ============================================================
    // CLONE DRAWING DATA
    // ============================================================

    private DrawingData CloneDrawingData(
        DrawingData source
    )
    {
        if (source == null)
            return new DrawingData();

        string json =
            DrawingSerializer.Serialize(source);

        DrawingData copy =
            DrawingSerializer.Deserialize(json);

        return copy ?? new DrawingData();
    }


    // ============================================================
    // GET DRAWING DATA
    // ============================================================

    /// <summary>
    /// Devuelve el dibujo actual (referencia viva, no copia).
    /// </summary>
    public DrawingData GetDrawingData()
    {
        return currentDrawing;
    }


    public bool HasDrawing()
    {
        return
            currentDrawing != null &&
            currentDrawing.strokes != null &&
            currentDrawing.strokes.Count > 0;
    }


    // ============================================================
    // LOAD DRAWING DATA
    // ============================================================

    public void LoadDrawingData(DrawingData data)
    {
        if (data == null)
        {
            Debug.LogWarning(
                "DrawTest: LoadDrawingData recibió null."
            );

            return;
        }

        // Un dibujo cargado desde fuera
        // comienza una nueva sesión de edición.
        undoHistory.Clear();
        redoHistory.Clear();

        // Se copia para no modificar el objeto original.
        LoadDrawingDataInternal(
            CloneDrawingData(data)
        );
    }


    // ============================================================
    // INTERNAL LOAD
    // ============================================================

    private void LoadDrawingDataInternal(DrawingData data)
    {
        currentStroke = null;

        eraserActionStarted = false;

        currentDrawing =
            data ?? new DrawingData();

        if (currentDrawing.strokes == null)
        {
            currentDrawing.strokes =
                new List<StrokeData>();
        }

        currentDrawing.strokes.RemoveAll(
            s =>
                s == null ||
                s.points == null ||
                s.points.Count == 0
        );

        if (surface != null)
            surface.SetDrawing(currentDrawing);
    }


    // ============================================================
    // CLEAR DRAWING
    // ============================================================

    public void ClearDrawing()
    {
        SaveUndoState();

        LoadDrawingDataInternal(new DrawingData());
    }


    // ============================================================
    // BRUSH SETTINGS
    // ============================================================

    public void SetBrushWidth(float width)
    {
        brushWidth =
            Mathf.Max(0f, width);
    }


    public void SetBrushColor(Color color)
    {
        brushColor = color;
    }


    // ============================================================
    // ERASER SETTINGS
    // ============================================================

    public void SetEraserRadius(float radius)
    {
        eraserRadius =
            Mathf.Max(0f, radius);
    }


    // ============================================================
    // ERASER MODE
    // ============================================================

    public bool IsEraserMode()
    {
        return eraserMode;
    }


    public void SetEraserMode(bool enabled)
    {
        eraserMode = enabled;
    }


    public void ToggleEraser()
    {
        eraserMode = !eraserMode;

        Debug.Log(
            eraserMode
                ? "Modo goma activado"
                : "Modo pincel activado"
        );
    }


    // ============================================================
    // JSON
    // ============================================================

    public string GetDrawingJson()
    {
        return DrawingSerializer.Serialize(
            GetDrawingData()
        );
    }


    public void PrintDrawingJson()
    {
        Debug.Log(
            "DRAWING JSON:\n" +
            GetDrawingJson()
        );
    }


    // ============================================================
    // FIRESTORE
    // ============================================================

    /// <summary>
    /// Publica el dibujo completo como un único PostIt
    /// de tipo "dibujo".
    /// </summary>
    public void PublicarDibujoEnFirestore(
        string authorId,
        Vector3 posRelativa
    )
    {
        if (FirestoreService.Instance == null)
        {
            Debug.LogError(
                "DrawTest: No existe una instancia de FirestoreService."
            );

            return;
        }

        if (!HasDrawing())
        {
            Debug.LogWarning(
                "DrawTest: No hay dibujo para publicar."
            );

            return;
        }

        FirestoreService.Instance.PublicarPostIt(
            authorId,
            "dibujo",
            GetDrawingJson(),
            posRelativa
        );

        Debug.Log(
            "DrawTest: dibujo publicado en Firestore."
        );
    }


    // ============================================================
    // FIRESTORE - TEST
    // ============================================================

    [Header("FIRESTORE TEST")]
    [SerializeField]
    private string testAuthorId = "test-user";

    [SerializeField]
    private Vector3 testPosition = Vector3.zero;


    [ContextMenu("Publicar dibujo en Firestore")]
    public void PublicarDibujoTest()
    {
        PublicarDibujoEnFirestore(
            testAuthorId,
            testPosition
        );
    }
}
