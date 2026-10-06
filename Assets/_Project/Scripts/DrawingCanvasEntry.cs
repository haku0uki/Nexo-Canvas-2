using UnityEngine;

public class DrawingCanvasEntry : BaseCanvasEntry
{
    public override void Populate(PostItData data)
    {
        base.Populate(data);

        DrawingData drawing = DrawingSerializer.Deserialize(data.contenido);
        // TODO: render each stroke in 'drawing.strokes' (e.g. one LineRenderer per stroke).
        // Left out on purpose for now -- separate piece of work.
    }
}