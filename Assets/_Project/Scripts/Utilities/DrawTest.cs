using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DrawTest : MonoBehaviour
{
    // ============================================================
    // REFERENCES
    // ============================================================

    [Header("LINE PREFAB")]
    public GameObject linePrefab;

    [Header("DRAWING CONTAINER")]
    [Tooltip(
        "Debe estar ubicado en el centro del lienzo. " +
        "Los puntos se guardarán relativos a este Transform."
    )]
    public Transform drawingContainer;


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

    private Coroutine drawing;

    private List<LineRenderer> lines =
        new List<LineRenderer>();

    private bool eraserMode = false;

    private bool eraserActionStarted = false;


    // ============================================================
    // UNITY
    // ============================================================

    private void Update()
    {
        // --------------------------------------------------------
        // TOGGLE ERASER
        // --------------------------------------------------------
        if (Input.GetKeyDown(KeyCode.E))
        {
            ToggleEraser();
        }
        // --------------------------------------------------------
        // MOUSE DOWN

        if (Input.GetMouseButtonDown(0))
        {
            if (eraserMode)
            {
                StartErase();
            }
            else
            {
                StartLine();
            }
        }

        // --------------------------------------------------------
        // ERASER

        if (Input.GetMouseButton(0) && eraserMode)
        {
            if (TryGetDrawingLocalPosition(out Vector3 mouseLocalPosition))
            {
                EraseAtPosition(mouseLocalPosition);
            }
        }

        // --------------------------------------------------------
        // MOUSE UP

        if (Input.GetMouseButtonUp(0))
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
    }


    // ============================================================
    // DRAWING
    // ============================================================

    private void StartLine()
    {
        SaveUndoState();

        drawing =
            StartCoroutine(
                DrawLine()
            );
    }


    private void FinishLine()
    {
        if (drawing != null)
        {
            StopCoroutine(drawing);

            drawing = null;
        }
    }


    private IEnumerator DrawLine()
    {
        if (linePrefab == null)
        {
            Debug.LogError(
                "DrawTest: No hay Line Prefab asignado."
            );

            yield break;
        }


        if (drawingContainer == null)
        {
            Debug.LogError(
                "DrawTest: No hay Drawing Container asignado."
            );

            yield break;
        }


        // --------------------------------------------------------
        // CREATE LINE
        // --------------------------------------------------------

        GameObject lineObject =
            Instantiate(
                linePrefab,
                drawingContainer
            );


        LineRenderer line =
            lineObject.GetComponent<LineRenderer>();


        if (line == null)
        {
            Debug.LogError(
                "DrawTest: El Line Prefab no tiene LineRenderer."
            );

            Destroy(lineObject);

            yield break;
        }


        // --------------------------------------------------------
        // CONFIGURE
        // --------------------------------------------------------

        line.useWorldSpace = false;

        line.startWidth =
            brushWidth;

        line.endWidth =
            brushWidth;

        line.startColor =
            brushColor;

        line.endColor =
            brushColor;

        line.positionCount = 0;


        lines.Add(line);


        // --------------------------------------------------------
        // DRAW
        // --------------------------------------------------------

        Vector3 lastPoint =
            Vector3.zero;

        bool hasLastPoint =
            false;


        while (Input.GetMouseButton(0))
        {
            Vector3 mouseLocalPosition;

            if (!TryGetDrawingLocalPosition(
                out mouseLocalPosition
            ))
            {
                yield return null;
                continue;
            }


            // ----------------------------------------------------
            // MINIMUM POINT DISTANCE
            // ----------------------------------------------------

            if (
                !hasLastPoint ||
                Vector3.Distance(
                    mouseLocalPosition,
                    lastPoint
                ) >= minPointDistance
            )
            {
                line.positionCount++;


                line.SetPosition(
                    line.positionCount - 1,
                    mouseLocalPosition
                );


                lastPoint =
                    mouseLocalPosition;

                hasLastPoint =
                    true;
            }


            yield return null;
        }


        drawing = null;
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


    private void EraseAtPosition(
        Vector3 mousePosition
    )
    {
        List<LineRenderer> currentLines =
            new List<LineRenderer>(lines);


        foreach (
            LineRenderer line
            in currentLines
        )
        {
            if (line == null)
                continue;


            int pointCount =
                line.positionCount;


            if (pointCount < 2)
                continue;


            // ----------------------------------------------------
            // CHECK SEGMENTS
            // ----------------------------------------------------

            for (
                int i = 0;
                i < pointCount - 1;
                i++
            )
            {
                Vector3 pointA =
                    line.GetPosition(i);

                Vector3 pointB =
                    line.GetPosition(i + 1);


                float distance =
                    DistancePointToSegment(
                        mousePosition,
                        pointA,
                        pointB
                    );


                if (
                    distance <=
                    eraserRadius
                )
                {
                    // Una pasada completa de goma
                    // cuenta como una acción.
                    if (!eraserActionStarted)
                    {
                        SaveUndoState();

                        eraserActionStarted =
                            true;
                    }


                    SplitLine(
                        line,
                        i
                    );


                    break;
                }
            }
        }
    }


    // ============================================================
    // SPLIT LINE
    // ============================================================

    private void SplitLine(
        LineRenderer line,
        int segmentIndex
    )
    {
        int pointCount =
            line.positionCount;


        List<Vector3> firstPart =
            new List<Vector3>();

        List<Vector3> secondPart =
            new List<Vector3>();


        // --------------------------------------------------------
        // FIRST PART
        // --------------------------------------------------------

        for (
            int i = 0;
            i <= segmentIndex;
            i++
        )
        {
            firstPart.Add(
                line.GetPosition(i)
            );
        }


        // --------------------------------------------------------
        // SECOND PART
        // --------------------------------------------------------

        for (
            int i = segmentIndex + 1;
            i < pointCount;
            i++
        )
        {
            secondPart.Add(
                line.GetPosition(i)
            );
        }

        // --------------------------------------------------------
        // STYLE

        float width =
            line.startWidth;

        Color color =
            line.startColor;

        // --------------------------------------------------------
        // REMOVE ORIGINAL

        lines.Remove(line);

        Destroy(
            line.gameObject
        );

        // --------------------------------------------------------
        // CREATE FIRST PART

        if (firstPart.Count >= 2)
        {
            CreateLine(
                firstPart,
                width,
                color
            );
        }

        // --------------------------------------------------------
        // CREATE SECOND PART

        if (secondPart.Count >= 2)
        {
            CreateLine(
                secondPart,
                width,
                color
            );
        }
    }

    // ============================================================
    // CREATE LINE
    private void CreateLine(
        List<Vector3> points,
        float width,
        Color color
    )
    {
        if (
            points == null ||
            points.Count < 2
        )
        {
            return;
        }

        if (linePrefab == null)
        {
            Debug.LogError(
                "DrawTest: No hay Line Prefab asignado."
            );
            return;
        }

        if (drawingContainer == null)
        {
            Debug.LogError(
                "DrawTest: No hay Drawing Container asignado."
            );
            return;
        }

        GameObject lineObject =
            Instantiate(
                linePrefab,
                drawingContainer
            );

        LineRenderer line =
            lineObject.GetComponent<LineRenderer>();

        if (line == null)
        {
            Destroy(lineObject);

            return;
        }

        line.useWorldSpace = false;

        line.startWidth =
            width;

        line.endWidth =
            width;

        line.startColor =
            color;

        line.endColor =
            color;

        line.positionCount =
            points.Count;

        line.SetPositions(
            points.ToArray()
        );

        lines.Add(line);
    }

    // ============================================================
    // COORDINATE CONVERSION
    private bool TryGetDrawingLocalPosition(
        out Vector3 localPosition
    )
    {
        localPosition = Vector3.zero;

        if (Camera.main == null)
        {
            Debug.LogError(
                "DrawTest: No hay Main Camera."
            );

            return false;
        }

        if (drawingContainer == null)
        {
            Debug.LogError(
                "DrawTest: No hay Drawing Container."
            );

            return false;
        }

        Ray ray =
            Camera.main.ScreenPointToRay(
                Input.mousePosition
            );

        Plane drawingPlane =
            new Plane(
                drawingContainer.forward,
                drawingContainer.position
            );

        if (!drawingPlane.Raycast(
            ray,
            out float distance
        ))
        {
            return false;
        }

        Vector3 worldPosition =
            ray.GetPoint(distance);

        localPosition =
            drawingContainer.InverseTransformPoint(
                worldPosition
            );

        return true;
    }

    // ============================================================
    // POINT -> SEGMENT DISTANCE
    private float DistancePointToSegment(
        Vector3 point,
        Vector3 segmentStart,
        Vector3 segmentEnd
    )
    {
        Vector3 segment =
            segmentEnd - segmentStart;

        float segmentLengthSquared =
            segment.sqrMagnitude;

        if (
            segmentLengthSquared ==
            0f
        )
        {
            return Vector3.Distance(
                point,
                segmentStart
            );
        }

        float t =
            Vector3.Dot(
                point - segmentStart,
                segment
            )
            / segmentLengthSquared;

        t =
            Mathf.Clamp01(t);

        Vector3 closestPoint =
            segmentStart +
            segment * t;

        return Vector3.Distance(
            point,
            closestPoint
        );
    }

    // ============================================================
    // UNDO

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

        DrawingData currentState =
            GetDrawingData();

        redoHistory.Add(
            CloneDrawingData(
                currentState
            )
        );

        // --------------------------------------------------------
        // PREVIOUS STATE

        int lastIndex =
            undoHistory.Count - 1;

        DrawingData previousState =
            undoHistory[lastIndex];

        undoHistory.RemoveAt(
            lastIndex
        );

        // --------------------------------------------------------
        // RESTORE

        LoadDrawingDataInternal(
            previousState
        );
    }

    // ============================================================
    // REDO

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

        DrawingData currentState =
            GetDrawingData();

        undoHistory.Add(
            CloneDrawingData(
                currentState
            )
        );

        LimitUndoHistory();

        // --------------------------------------------------------
        // NEXT STATE

        int lastIndex =
            redoHistory.Count - 1;


        DrawingData nextState =
            redoHistory[lastIndex];


        redoHistory.RemoveAt(
            lastIndex
        );


        // --------------------------------------------------------
        // RESTORE
        // --------------------------------------------------------

        LoadDrawingDataInternal(
            nextState
        );
    }
    // ============================================================
    // SAVE UNDO STATE

    private void SaveUndoState()
    {
        DrawingData currentState =
            GetDrawingData();


        DrawingData copy =
            CloneDrawingData(
                currentState
            );

        undoHistory.Add(copy);

        // Una acción nueva invalida Redo.
        redoHistory.Clear();

        LimitUndoHistory();
    }
    // ============================================================
    // LIMIT HISTORY

    private void LimitUndoHistory()
    {
        if (maxUndoSteps < 1)
        {
            undoHistory.Clear();

            return;
        }

        while (
            undoHistory.Count >
            maxUndoSteps
        )
        {
            undoHistory.RemoveAt(0);
        }
    }

    // ============================================================
    // CLONE DRAWING DATA
    private DrawingData CloneDrawingData(
        DrawingData source
    )
    {
        if (source == null)
            return new DrawingData();

        string json =
            DrawingSerializer.Serialize(
                source
            );

        return DrawingSerializer.Deserialize(
            json
        );
    }

    // ============================================================
    // GET DRAWING DATA
    public DrawingData GetDrawingData()
    {
        DrawingData drawing =
            new DrawingData();


        foreach (
            LineRenderer line
            in lines
        )
        {
            if (line == null)
                continue;

            StrokeData stroke =
                LineToStrokeData(
                    line
                );

            if (
                stroke.points != null &&
                stroke.points.Count >= 2
            )
            {
                drawing.strokes.Add(
                    stroke
                );
            }
        }

        return drawing;
    }


    // ============================================================
    // LINE -> STROKE DATA
    // ============================================================

    private StrokeData LineToStrokeData(
        LineRenderer line
    )
    {
        StrokeData stroke =
            new StrokeData();


        // --------------------------------------------------------
        // WIDTH
        // --------------------------------------------------------

        stroke.width =
            line.startWidth;


        // --------------------------------------------------------
        // COLOR -> HEX
        // --------------------------------------------------------

        stroke.colorHex =
            ColorUtility.ToHtmlStringRGBA(
                line.startColor
            );


        // --------------------------------------------------------
        // POINTS
        // --------------------------------------------------------

        for (
            int i = 0;
            i < line.positionCount;
            i++
        )
        {
            Vector3 position =
                line.GetPosition(i);


            stroke.points.Add(
                new Vector2Serialized(
                    position.x,
                    position.y
                )
            );
        }


        return stroke;
    }


    // ============================================================
    // LOAD DRAWING DATA
    // ============================================================

    public void LoadDrawingData(
        DrawingData data
    )
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


        LoadDrawingDataInternal(
            data
        );
    }


    // ============================================================
    // INTERNAL LOAD
    // ============================================================

    private void LoadDrawingDataInternal(
        DrawingData data
    )
    {
        ClearDrawingVisualOnly();


        if (data == null)
            return;


        foreach (
            StrokeData stroke
            in data.strokes
        )
        {
            if (stroke == null)
                continue;


            if (
                stroke.points == null ||
                stroke.points.Count < 2
            )
            {
                continue;
            }


            CreateLineFromStroke(
                stroke
            );
        }
    }


    // ============================================================
    // STROKE -> LINE
    // ============================================================

    private void CreateLineFromStroke(
        StrokeData stroke
    )
    {
        if (
            stroke == null ||
            stroke.points == null ||
            stroke.points.Count < 2
        )
        {
            return;
        }


        if (linePrefab == null)
        {
            Debug.LogError(
                "DrawTest: No hay Line Prefab asignado."
            );

            return;
        }


        if (drawingContainer == null)
        {
            Debug.LogError(
                "DrawTest: No hay Drawing Container."
            );

            return;
        }


        GameObject lineObject =
            Instantiate(
                linePrefab,
                drawingContainer
            );


        LineRenderer line =
            lineObject.GetComponent<LineRenderer>();


        if (line == null)
        {
            Destroy(lineObject);

            return;
        }


        line.useWorldSpace = false;


        // --------------------------------------------------------
        // WIDTH
        // --------------------------------------------------------

        line.startWidth =
            stroke.width;

        line.endWidth =
            stroke.width;


        // --------------------------------------------------------
        // HEX -> COLOR
        // --------------------------------------------------------

        Color color;


        if (
            !ColorUtility.TryParseHtmlString(
                "#" + stroke.colorHex,
                out color
            )
        )
        {
            Debug.LogWarning(
                "DrawTest: No se pudo interpretar el color " +
                stroke.colorHex
            );

            color =
                Color.black;
        }


        line.startColor =
            color;

        line.endColor =
            color;


        // --------------------------------------------------------
        // POINTS
        // --------------------------------------------------------

        line.positionCount =
            stroke.points.Count;


        for (
            int i = 0;
            i < stroke.points.Count;
            i++
        )
        {
            Vector2Serialized point =
                stroke.points[i];


            line.SetPosition(
                i,
                new Vector3(
                    point.x,
                    point.y,
                    0f
                )
            );
        }


        lines.Add(line);
    }


    // ============================================================
    // CLEAR DRAWING
    // ============================================================

    public void ClearDrawing()
    {
        SaveUndoState();

        ClearDrawingVisualOnly();
    }


    private void ClearDrawingVisualOnly()
    {
        if (drawing != null)
        {
            StopCoroutine(drawing);

            drawing = null;
        }


        foreach (
            LineRenderer line
            in lines
        )
        {
            if (line != null)
            {
                Destroy(
                    line.gameObject
                );
            }
        }


        lines.Clear();
    }


    // ============================================================
    // BRUSH SETTINGS
    // ============================================================

    public void SetBrushWidth(
        float width
    )
    {
        brushWidth =
            Mathf.Max(
                0f,
                width
            );
    }


    public void SetBrushColor(
        Color color
    )
    {
        brushColor =
            color;
    }

    // ============================================================
    // ERASER SETTINGS
    // ============================================================

    public void SetEraserRadius(
        float radius
    )
    {
        eraserRadius =
            Mathf.Max(
                0f,
                radius
            );
    }


    // ============================================================
    // ERASER MODE
    // ============================================================

    public void SetEraserMode(
        bool enabled
    )
    {
        eraserMode =
            enabled;
    }


    public void ToggleEraser()
    {
        eraserMode =
            !eraserMode;


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

        string json = GetDrawingJson();

        if (string.IsNullOrEmpty(json))
        {
            Debug.LogWarning(
                "DrawTest: No hay dibujo para publicar."
            );

            return;
        }

        FirestoreService.Instance.PublicarPostIt(
            authorId,
            "dibujo",
            json,
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
        if (FirestoreService.Instance == null)
        {
            Debug.LogError(
                "DrawTest: No existe FirestoreService en la escena."
            );

            return;
        }

        string json = GetDrawingJson();

        if (string.IsNullOrEmpty(json))
        {
            Debug.LogWarning(
                "DrawTest: No hay dibujo para publicar."
            );

            return;
        }

        FirestoreService.Instance.PublicarPostIt(
            testAuthorId,
            "dibujo",
            json,
            testPosition
        );

        Debug.Log(
            "DrawTest: dibujo enviado a Firestore."
        );
    }

}