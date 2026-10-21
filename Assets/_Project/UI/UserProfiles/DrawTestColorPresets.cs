using UnityEngine;

// Un UnityEvent no puede pasar un Color como argumento, asi que los botones de color
// llaman a SetColorByIndex(int) y este componente aplica el color de la lista.
//
// Ponlo en el mismo GameObject que el DrawTest del panel de avatar y conecta:
//   profile-color-1 ButtonBridge -> onClick -> DrawTestColorPresets.SetColorByIndex(0)
//   profile-color-2 ButtonBridge -> onClick -> DrawTestColorPresets.SetColorByIndex(1)
//   ...
// El orden de "Colors" debe coincidir con el de los botones del UXML.
public class DrawTestColorPresets : MonoBehaviour
{
    [Tooltip("Si queda vacio se usa el DrawTest de este GameObject.")]
    [SerializeField] private DrawTest drawTest;

    [SerializeField] private Color[] colors =
    {
        new Color32(19, 19, 18, 255),
        new Color32(214, 40, 40, 255),
        new Color32(0, 119, 182, 255),
        new Color32(42, 157, 143, 255),
        new Color32(247, 183, 51, 255)
    };

    private void Awake()
    {
        if (drawTest == null)
            drawTest = GetComponent<DrawTest>();
    }

    /// <summary>Elige el color del pincel y sale del modo borrador.</summary>
    public void SetColorByIndex(int index)
    {
        if (drawTest == null || colors == null) return;

        if (index < 0 || index >= colors.Length)
        {
            Debug.LogWarning("DrawTestColorPresets: indice de color fuera de rango: " + index, this);
            return;
        }

        drawTest.SetEraserMode(false);
        drawTest.SetBrushColor(colors[index]);
    }
}
