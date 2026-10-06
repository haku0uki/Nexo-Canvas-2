using UnityEngine;
using System.Collections.Generic;

// 1. Estructuras de datos serializables para Unity
[System.Serializable]
public class Vector2Serialized
{
    public float x;
    public float y;
    public Vector2Serialized(float x, float y) { this.x = x; this.y = y; }
}

[System.Serializable]
public class StrokeData
{
    public string colorHex; // Color de la línea en formato Hexadecimal (#FF0000)
    public float width;     // Grosor del pincel
    public List<Vector2Serialized> points = new List<Vector2Serialized>();
}

[System.Serializable]
public class DrawingData
{
    public List<StrokeData> strokes = new List<StrokeData>();
}

// 2. El Serializador
public class DrawingSerializer : MonoBehaviour
{
    // Convierte un dibujo estructurado de Unity a una cadena de texto JSON
    public static string Serialize(DrawingData drawing)
    {
        if (drawing == null) return "";
        return JsonUtility.ToJson(drawing);
    }

    // Convierte el texto JSON de Firestore de vuelta a un dibujo estructurado de Unity
    public static DrawingData Deserialize(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        return JsonUtility.FromJson<DrawingData>(json);
    }
}
