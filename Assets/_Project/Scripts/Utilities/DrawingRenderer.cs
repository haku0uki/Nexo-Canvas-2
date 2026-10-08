using System.Collections.Generic;
using UnityEngine;

public class DrawingRenderer : MonoBehaviour
{
    [Header("LINE PREFAB")]
    [SerializeField]
    private GameObject linePrefab;

    [Header("DISPLAY")]
    [Min(0.001f)]
    [SerializeField]
    private float scale = 1f;

    [SerializeField]
    private bool centerDrawing = true;

    private readonly List<LineRenderer> lines =
        new List<LineRenderer>();

    private DrawingData currentDrawing;

    public void SetDrawing(DrawingData drawing)
    {
        Clear();

        currentDrawing = drawing;

        if (drawing == null)
            return;

        if (drawing.strokes == null)
            return;

        foreach (StrokeData stroke in drawing.strokes)
        {
            if (stroke == null)
                continue;

            if (stroke.points == null ||
                stroke.points.Count < 2)
                continue;

            CreateLine(stroke);
        }

        if (centerDrawing)
            CenterDrawing();

        ApplyScale();
    }

    private void CreateLine(StrokeData stroke)
    {
        if (linePrefab == null)
        {
            Debug.LogError(
                "DrawingRenderer: No hay Line Prefab asignado."
            );

            return;
        }

        GameObject lineObject =
            Instantiate(
                linePrefab,
                transform
            );

        LineRenderer line =
            lineObject.GetComponent<LineRenderer>();

        if (line == null)
        {
            Debug.LogError(
                "DrawingRenderer: " +
                "El Line Prefab no tiene LineRenderer."
            );

            Destroy(lineObject);
            return;
        }

        line.useWorldSpace = false;

        line.startWidth = stroke.width;
        line.endWidth = stroke.width;

        Color color;

        if (!ColorUtility.TryParseHtmlString(
                "#" + stroke.colorHex,
                out color))
        {
            Debug.LogWarning(
                "DrawingRenderer: Color inválido: " +
                stroke.colorHex
            );

            color = Color.black;
        }

        line.startColor = color;
        line.endColor = color;

        line.positionCount =
            stroke.points.Count;

        for (int i = 0;
             i < stroke.points.Count;
             i++)
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

    public void SetScale(float newScale)
    {
        scale =
            Mathf.Max(
                0.001f,
                newScale
            );

        ApplyScale();
    }

    private void ApplyScale()
    {
        transform.localScale =
            Vector3.one * scale;
    }

    private void CenterDrawing()
    {
        if (lines.Count == 0)
            return;

        bool hasPoint = false;

        Vector3 min = Vector3.zero;
        Vector3 max = Vector3.zero;

        foreach (LineRenderer line in lines)
        {
            if (line == null)
                continue;

            for (int i = 0;
                 i < line.positionCount;
                 i++)
            {
                Vector3 point =
                    line.GetPosition(i);

                if (!hasPoint)
                {
                    min = point;
                    max = point;
                    hasPoint = true;
                }
                else
                {
                    min = Vector3.Min(
                        min,
                        point
                    );

                    max = Vector3.Max(
                        max,
                        point
                    );
                }
            }
        }

        if (!hasPoint)
            return;

        Vector3 center =
            (min + max) * 0.5f;

        foreach (LineRenderer line in lines)
        {
            if (line == null)
                continue;

            for (int i = 0;
                 i < line.positionCount;
                 i++)
            {
                Vector3 point =
                    line.GetPosition(i);

                line.SetPosition(
                    i,
                    point - center
                );
            }
        }
    }

    public void FitToSize(float targetSize)
    {
        if (lines.Count == 0)
            return;

        targetSize =
            Mathf.Max(
                0.001f,
                targetSize
            );

        Bounds bounds =
            GetLocalBounds();

        float currentSize =
            Mathf.Max(
                bounds.size.x,
                bounds.size.y
            );

        if (currentSize <= 0.0001f)
            return;

        SetScale(
            targetSize / currentSize
        );
    }

    private Bounds GetLocalBounds()
    {
        bool hasPoint = false;

        Bounds bounds =
            new Bounds(
                Vector3.zero,
                Vector3.zero
            );

        foreach (LineRenderer line in lines)
        {
            if (line == null)
                continue;

            for (int i = 0;
                 i < line.positionCount;
                 i++)
            {
                Vector3 point =
                    line.GetPosition(i);

                if (!hasPoint)
                {
                    bounds =
                        new Bounds(
                            point,
                            Vector3.zero
                        );

                    hasPoint = true;
                }
                else
                {
                    bounds.Encapsulate(point);
                }
            }
        }

        return bounds;
    }

    public void Clear()
    {
        foreach (LineRenderer line in lines)
        {
            if (line != null)
                Destroy(line.gameObject);
        }

        lines.Clear();
        currentDrawing = null;
    }

    public DrawingData GetCurrentDrawing()
    {
        return currentDrawing;
    }
}