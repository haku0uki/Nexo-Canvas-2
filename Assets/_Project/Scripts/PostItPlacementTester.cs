using UnityEngine;

// Temporary test tool: tap (or click) on the board plane to publish a text post-it at that spot.
// No UI involved -- the text comes from the Inspector.
// Do NOT run this at the same time as PostItCreationFlow: both read raw taps each frame and
// would fight over the same tap. Keep this for quick dev testing in a scene without the real UI.
public class PostItPlacementTester : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Optional: leave empty with AR Foundation, since BoardCanvas only exists once the tracked image prefab is instantiated.")]
    [SerializeField] private BoardCanvas boardCanvas;
    [Tooltip("Leave empty to use Camera.main (the AR camera).")]
    [SerializeField] private Camera arCamera;
    [Tooltip("Optional object moved to the last tapped point, for visual feedback.")]
    [SerializeField] private Transform hitMarker;

    [Header("Test data")]
    [SerializeField] private string testAuthorId = "test_user_unity";
    [SerializeField, TextArea(2, 4)] private string testText = "Test post-it";
    [Tooltip("Small lift along the board normal (local Y) so the post-it does not clip into the paper.")]
    [SerializeField] private float surfaceOffset = 0.01f;

    [Header("Behaviour")]
    [Tooltip("When enabled, taps are ignored while the board is hidden (target not tracked).")]
    [SerializeField] private bool requireBoardVisible = true;
    [Tooltip("When disabled, the tap is only logged and the marker moves; nothing is sent to Firestore.")]
    [SerializeField] private bool publishToFirestore = true;

    private void Awake()
    {
        if (arCamera == null) arCamera = Camera.main;
    }

    private void Update()
    {
        if (boardCanvas == null)
        {
            boardCanvas = FindFirstObjectByType<BoardCanvas>();
            if (boardCanvas == null) return; // Target not detected yet.
        }

        if (!PointerInput.TryGetPointerDown(out Vector2 screenPosition)) return;
        if (requireBoardVisible && !boardCanvas.gameObject.activeInHierarchy) return;
        if (!BoardRaycaster.TryGetLocalPoint(boardCanvas, arCamera, screenPosition, surfaceOffset, out Vector3 localPoint)) return;
        if (!boardCanvas.IsInsideBoard(localPoint)) return; // Ignore taps outside the paper.

        Debug.Log($"[PostItPlacementTester] Local point on board: {localPoint}");

        if (hitMarker != null)
        {
            hitMarker.position = boardCanvas.transform.TransformPoint(localPoint);
        }

        if (!publishToFirestore) return;

        if (FirestoreService.Instance == null)
        {
            Debug.LogError("[PostItPlacementTester] FirestoreService is missing from the scene.");
            return;
        }

        FirestoreService.Instance.PublicarPostIt(testAuthorId, "texto", testText, localPoint);
    }
}