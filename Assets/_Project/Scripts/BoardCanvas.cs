using System.Collections.Generic;
using SheepyCreations.EventBus;
using UnityEngine;

// Renamed from "Canvas" to avoid shadowing UnityEngine.Canvas.
// Owns every entry rendered on the board and keeps them in sync with Firestore.
public class BoardCanvas : MonoBehaviour
{
    [Header("Board area")]
    [Tooltip("Size of the paper in scene units (same width and height as the tracked image). Post-its must stay inside this rectangle.")]
    [SerializeField] private Vector2 boardSize = new Vector2(0.297f, 0.21f);

    [Header("Grid")]
    [Tooltip("Columnas (eje X) y filas (eje Z). Cada post-it ocupa exactamente una celda. Definir antes de producción: los IDs de Firestore dependen de la grilla.")]
    [SerializeField] private Vector2Int gridSize = new Vector2Int(4, 3);

    [Header("Entry prefabs by type")]
    [SerializeField] private TextCanvasEntry textEntryPrefab;
    [SerializeField] private DrawingCanvasEntry drawingEntryPrefab;

    [Header("Prompt setup")]
    [SerializeField] private PromptSuggestion promptCanvasTemplate;
    [SerializeField] private List<Prompt_SO> promptList;

    private readonly Dictionary<string, BaseCanvasEntry> entries = new Dictionary<string, BaseCanvasEntry>();
    private readonly List<PromptSuggestion> promptCanvases = new List<PromptSuggestion>();

    private bool isListening;

    public Vector2 CellSize => new Vector2(boardSize.x / Mathf.Max(1, gridSize.x), boardSize.y / Mathf.Max(1, gridSize.y));

    public void StartListening()
    {
        if (isListening) return;
        isListening = true;
        FirestoreService.Instance.EscucharPostIts(OnPostItsUpdated);
    }

    private void OnPostItsUpdated(List<PostItData> updatedEntries)
    {
        foreach (PostItData data in updatedEntries)
        {
            if (entries.TryGetValue(data.id, out BaseCanvasEntry existingEntry))
            {
                existingEntry.Populate(data); // Refresh position/content in place.
                continue;
            }

            CreateEntry(data);
        }
    }

    private void CreateEntry(PostItData data)
    {
        BaseCanvasEntry prefab = data.tipo == "dibujo" ? (BaseCanvasEntry)drawingEntryPrefab : textEntryPrefab;
        BaseCanvasEntry newEntry = Instantiate(prefab, transform);
        newEntry.Populate(data);
        newEntry.Clicked += OnEntryClicked;
        entries.Add(data.id, newEntry);
    }

    private void OnEntryClicked(PostItData data)
    {
        EventBus<EntryClickedEvent<PostItData>>.Raise(new EntryClickedEvent<PostItData>(data));
    }

    public bool IsInsideBoard(Vector3 localPoint)
    {
        return Mathf.Abs(localPoint.x) <= boardSize.x * 0.5f
            && Mathf.Abs(localPoint.z) <= boardSize.y * 0.5f;
    }

    // ============================================================
    // GRID
    // ============================================================

    // Punto local -> celda. Devuelve false si el punto cae fuera del papel.
    public bool TryGetCell(Vector3 localPoint, out Vector2Int cell)
    {
        cell = default;
        if (!IsInsideBoard(localPoint)) return false;

        Vector2 cellSize = CellSize;
        int column = Mathf.FloorToInt((localPoint.x + boardSize.x * 0.5f) / cellSize.x);
        int row = Mathf.FloorToInt((localPoint.z + boardSize.y * 0.5f) / cellSize.y);

        // Clamp por si el toque cae justo en el borde (x == boardSize.x / 2).
        cell = new Vector2Int(
            Mathf.Clamp(column, 0, gridSize.x - 1),
            Mathf.Clamp(row, 0, gridSize.y - 1));
        return true;
    }

    // Celda -> centro de la celda en coordenadas locales del tablero.
    public Vector3 CellToLocalPoint(Vector2Int cell, float surfaceOffset)
    {
        Vector2 cellSize = CellSize;
        return new Vector3(
            -boardSize.x * 0.5f + (cell.x + 0.5f) * cellSize.x,
            surfaceOffset,
            -boardSize.y * 0.5f + (cell.y + 0.5f) * cellSize.y);
    }

    // ID de documento de Firestore para una celda (garantiza una sola entrada por celda).
    public string GetCellId(Vector2Int cell) => $"celda_{cell.x}_{cell.y}";

    public bool IsCellOccupied(Vector2Int cell)
    {
        foreach (BaseCanvasEntry entry in entries.Values)
        {
            if (TryGetCell(entry.transform.localPosition, out Vector2Int entryCell) && entryCell == cell)
                return true;
        }
        return false;
    }

    private void OnDrawGizmos()
    {
        Gizmos.matrix = transform.localToWorldMatrix;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(boardSize.x, 0f, boardSize.y));

        Gizmos.color = new Color(0f, 1f, 1f, 0.35f);
        Vector2 cellSize = new Vector2(boardSize.x / Mathf.Max(1, gridSize.x), boardSize.y / Mathf.Max(1, gridSize.y));

        for (int x = 1; x < gridSize.x; x++)
        {
            float px = -boardSize.x * 0.5f + x * cellSize.x;
            Gizmos.DrawLine(new Vector3(px, 0f, -boardSize.y * 0.5f), new Vector3(px, 0f, boardSize.y * 0.5f));
        }

        for (int y = 1; y < gridSize.y; y++)
        {
            float pz = -boardSize.y * 0.5f + y * cellSize.y;
            Gizmos.DrawLine(new Vector3(-boardSize.x * 0.5f, 0f, pz), new Vector3(boardSize.x * 0.5f, 0f, pz));
        }
    }
}