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

    [Header("Entry prefabs by type")]
    [SerializeField] private TextCanvasEntry textEntryPrefab;
    [SerializeField] private DrawingCanvasEntry drawingEntryPrefab;

    [Header("Prompt setup")]
    [SerializeField] private PromptSuggestion promptCanvasTemplate;
    [SerializeField] private List<Prompt_SO> promptList;

    private readonly Dictionary<string, BaseCanvasEntry> entries = new Dictionary<string, BaseCanvasEntry>();
    private readonly List<PromptSuggestion> promptCanvases = new List<PromptSuggestion>();

    private bool isListening;

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

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(boardSize.x, 0f, boardSize.y));
    }
}