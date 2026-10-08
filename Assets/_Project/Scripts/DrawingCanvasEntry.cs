using UnityEngine;

public class DrawingCanvasEntry : BaseCanvasEntry
{
    [Header("DRAWING")]
    [SerializeField]
    private DrawingRenderer drawingRenderer;

    public override void Populate(PostItData data)
    {
        base.Populate(data);

        if (data == null)
            return;

        if (drawingRenderer == null)
        {
            Debug.LogError(
                "DrawingCanvasEntry: " +
                "No hay DrawingRenderer asignado."
            );

            return;
        }

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
