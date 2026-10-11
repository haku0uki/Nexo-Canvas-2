using System;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// VisualElement que dibuja un DrawingData con Painter2D y,
/// si no es ReadOnly, captura el puntero (mouse / touch) y avisa
/// mediante eventos. NO modifica los datos: eso lo hace DrawTest.
///
/// Sistema de coordenadas de los datos (igual que con LineRenderer):
///   - Origen en el centro del lienzo.
///   - Y positivo hacia arriba.
///   - Unidades de dibujo. El ancho del lienzo equivale a UnitsWidth.
/// </summary>
public class DrawingSurface : VisualElement
{
    // ============================================================
    // EVENTS (posiciones en unidades de dibujo)
    // ============================================================

    public event Action<Vector2> StrokeStarted;
    public event Action<Vector2> StrokeMoved;
    public event Action StrokeEnded;


    // ============================================================
    // STATE
    // ============================================================

    private DrawingData drawing;

    private float unitsWidth = 10f;
    private bool readOnly;
    private bool fitToContent;
    private float fitPadding = 8f;

    private int activePointer = -1;


    // ============================================================
    // PROPERTIES
    // ============================================================

    /// <summary>Ancho del lienzo en unidades de dibujo.</summary>
    public float UnitsWidth
    {
        get => unitsWidth;
        set
        {
            unitsWidth = Mathf.Max(0.01f, value);
            MarkDirtyRepaint();
        }
    }

    /// <summary>
    /// Solo lectura: no captura input (deja pasar los clics al
    /// elemento de abajo).
    /// </summary>
    public bool ReadOnly
    {
        get => readOnly;
        set
        {
            readOnly = value;

            pickingMode =
                readOnly
                    ? PickingMode.Ignore
                    : PickingMode.Position;

            MarkDirtyRepaint();
        }
    }

    /// <summary>
    /// Solo tiene efecto en ReadOnly: centra y escala el dibujo
    /// para que quepa en el elemento (reemplaza a
    /// centerDrawing / FitToSize del antiguo DrawingRenderer).
    /// </summary>
    public bool FitToContent
    {
        get => fitToContent;
        set
        {
            fitToContent = value;
            MarkDirtyRepaint();
        }
    }

    /// <summary>Margen en píxeles para FitToContent.</summary>
    public float FitPadding
    {
        get => fitPadding;
        set
        {
            fitPadding = Mathf.Max(0f, value);
            MarkDirtyRepaint();
        }
    }


    // ============================================================
    // CONSTRUCTOR
    // ============================================================

    public DrawingSurface()
    {
        pickingMode = PickingMode.Position;

        // Los trazos no se salen del lienzo.
        style.overflow = Overflow.Hidden;

        generateVisualContent += OnGenerateVisualContent;

        RegisterCallback<PointerDownEvent>(OnPointerDown);
        RegisterCallback<PointerMoveEvent>(OnPointerMove);
        RegisterCallback<PointerUpEvent>(OnPointerUp);
        RegisterCallback<PointerCancelEvent>(
            evt => EndInteraction(evt.pointerId)
        );
        RegisterCallback<PointerCaptureOutEvent>(
            evt => EndInteraction(evt.pointerId)
        );

        RegisterCallback<GeometryChangedEvent>(
            _ => MarkDirtyRepaint()
        );

        RegisterCallback<DetachFromPanelEvent>(
            _ => activePointer = -1
        );
    }


    // ============================================================
    // PUBLIC API
    // ============================================================

    /// <summary>
    /// Asigna el dibujo a mostrar. Se guarda la referencia
    /// (no una copia): si cambia, llama a Refresh().
    /// </summary>
    public void SetDrawing(DrawingData data)
    {
        drawing = data;

        MarkDirtyRepaint();
    }

    public void Refresh()
    {
        MarkDirtyRepaint();
    }


    // ============================================================
    // INPUT
    // ============================================================

    private void OnPointerDown(PointerDownEvent evt)
    {
        if (readOnly)
            return;

        if (activePointer != -1)
            return;

        if (evt.button != 0)
            return;

        if (!TryGetMapping(out _, out _))
            return;

        activePointer = evt.pointerId;

        this.CapturePointer(evt.pointerId);

        StrokeStarted?.Invoke(
            LocalToUnits(evt.localPosition)
        );

        evt.StopPropagation();
    }


    private void OnPointerMove(PointerMoveEvent evt)
    {
        if (evt.pointerId != activePointer)
            return;

        if (!this.HasPointerCapture(evt.pointerId))
            return;

        StrokeMoved?.Invoke(
            LocalToUnits(evt.localPosition)
        );

        evt.StopPropagation();
    }


    private void OnPointerUp(PointerUpEvent evt)
    {
        if (evt.pointerId != activePointer)
            return;

        EndInteraction(evt.pointerId);

        evt.StopPropagation();
    }


    private void EndInteraction(int pointerId)
    {
        if (activePointer == -1)
            return;

        if (pointerId != activePointer)
            return;

        activePointer = -1;

        if (this.HasPointerCapture(pointerId))
            this.ReleasePointer(pointerId);

        StrokeEnded?.Invoke();
    }


    // ============================================================
    // COORDINATES
    // ============================================================

    /// <summary>
    /// Obtiene el origen (en píxeles locales) y los píxeles por
    /// unidad de dibujo.
    /// </summary>
    private bool TryGetMapping(
        out Vector2 origin,
        out float pixelsPerUnit
    )
    {
        Rect r = contentRect;

        pixelsPerUnit = 1f;
        origin = Vector2.zero;

        if (r.width <= 0f || r.height <= 0f)
            return false;

        pixelsPerUnit = r.width / unitsWidth;

        origin = new Vector2(
            r.width * 0.5f,
            r.height * 0.5f
        );

        if (fitToContent &&
            readOnly &&
            TryGetBounds(out Rect bounds))
        {
            float availableW =
                Mathf.Max(1f, r.width - fitPadding * 2f);

            float availableH =
                Mathf.Max(1f, r.height - fitPadding * 2f);

            float scaleX =
                bounds.width > 0.00001f
                    ? availableW / bounds.width
                    : float.MaxValue;

            float scaleY =
                bounds.height > 0.00001f
                    ? availableH / bounds.height
                    : float.MaxValue;

            float scale = Mathf.Min(scaleX, scaleY);

            // Si es un punto o una línea perfecta se mantiene
            // la escala por defecto en el eje sin tamaño.
            if (scale < float.MaxValue)
                pixelsPerUnit = scale;

            Vector2 c = bounds.center;

            origin = new Vector2(
                r.width * 0.5f - c.x * pixelsPerUnit,
                r.height * 0.5f + c.y * pixelsPerUnit
            );
        }

        return true;
    }


    private bool TryGetBounds(out Rect bounds)
    {
        bounds = default;

        if (drawing == null || drawing.strokes == null)
            return false;

        bool hasPoint = false;

        float minX = 0f, maxX = 0f, minY = 0f, maxY = 0f;

        foreach (StrokeData stroke in drawing.strokes)
        {
            if (stroke == null || stroke.points == null)
                continue;

            foreach (Vector2Serialized p in stroke.points)
            {
                if (!hasPoint)
                {
                    minX = maxX = p.x;
                    minY = maxY = p.y;
                    hasPoint = true;
                }
                else
                {
                    minX = Mathf.Min(minX, p.x);
                    maxX = Mathf.Max(maxX, p.x);
                    minY = Mathf.Min(minY, p.y);
                    maxY = Mathf.Max(maxY, p.y);
                }
            }
        }

        if (!hasPoint)
            return false;

        bounds = new Rect(minX, minY, maxX - minX, maxY - minY);

        return true;
    }


    private Vector2 LocalToUnits(Vector2 local)
    {
        TryGetMapping(out Vector2 origin, out float ppu);

        // El puntero está capturado: si el dedo sale del área,
        // el punto se recorta al borde para que no queden trazos
        // fuera del lienzo.
        Rect r = contentRect;

        local.x = Mathf.Clamp(local.x, 0f, r.width);
        local.y = Mathf.Clamp(local.y, 0f, r.height);

        return new Vector2(
            (local.x - origin.x) / ppu,
            -(local.y - origin.y) / ppu
        );
    }


    // ============================================================
    // RENDER
    // ============================================================

    private void OnGenerateVisualContent(MeshGenerationContext ctx)
    {
        if (drawing == null || drawing.strokes == null)
            return;

        if (!TryGetMapping(out Vector2 origin, out float ppu))
            return;

        Painter2D painter = ctx.painter2D;

        painter.lineCap = LineCap.Round;
        painter.lineJoin = LineJoin.Round;

        foreach (StrokeData stroke in drawing.strokes)
        {
            if (stroke == null ||
                stroke.points == null ||
                stroke.points.Count == 0)
            {
                continue;
            }

            Color color;

            if (!ColorUtility.TryParseHtmlString(
                    "#" + stroke.colorHex,
                    out color))
            {
                color = Color.black;
            }

            float lineWidth =
                Mathf.Max(1f, stroke.width * ppu);

            // ----------------------------------------------------
            // TOQUE SIMPLE -> PUNTO
            // ----------------------------------------------------

            if (stroke.points.Count == 1)
            {
                painter.fillColor = color;

                painter.BeginPath();

                painter.Arc(
                    ToPixels(stroke.points[0], origin, ppu),
                    lineWidth * 0.5f,
                    Angle.Degrees(0f),
                    Angle.Degrees(360f)
                );

                painter.Fill();

                continue;
            }

            // ----------------------------------------------------
            // LINEA
            // ----------------------------------------------------

            painter.strokeColor = color;
            painter.lineWidth = lineWidth;

            painter.BeginPath();

            painter.MoveTo(
                ToPixels(stroke.points[0], origin, ppu)
            );

            for (int i = 1; i < stroke.points.Count; i++)
            {
                painter.LineTo(
                    ToPixels(stroke.points[i], origin, ppu)
                );
            }

            painter.Stroke();
        }
    }


    private static Vector2 ToPixels(
        Vector2Serialized p,
        Vector2 origin,
        float ppu
    )
    {
        return new Vector2(
            origin.x + p.x * ppu,
            origin.y - p.y * ppu
        );
    }
}