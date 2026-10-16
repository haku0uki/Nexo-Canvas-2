using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class Vector2Serialized
{
    public float x;
    public float y;

    public Vector2Serialized(float x, float y)
    {
        this.x = x;
        this.y = y;
    }
}

[System.Serializable]
public class StrokeData
{
    public string colorHex;
    public float width;
    public List<Vector2Serialized> points =
        new List<Vector2Serialized>();
}

[System.Serializable]
public class DrawingData
{
    public List<StrokeData> strokes =
        new List<StrokeData>();
}

public class DrawingSerializer : MonoBehaviour
{
    public static string Serialize(DrawingData drawing)
    {
        if (drawing == null)
            return "";

        return JsonUtility.ToJson(drawing);
    }

    public static DrawingData Deserialize(string json)
    {
        if (string.IsNullOrEmpty(json))
            return null;

        return JsonUtility.FromJson<DrawingData>(json);
    }
}