using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Temporary test tool: tap (or click) on the board plane to publish a text post-it at that spot.
// No UI involved -- the text comes from the Inspector.
// Attach this to an object that is always active (NOT a child of the board, which starts disabled).
public class PostItPlacementTester : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Optional: leave empty with AR Foundation, since BoardCanvas only exists once the tracked image prefab is instantiated.")]
    [SerializeField] private BoardCanvas boardCanvas;
    [Tooltip("Leave empty to use Camera.main (the Vuforia AR camera).")]
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

        if (boardCanvas == null)
        {
            boardCanvas = FindFirstObjectByType<BoardCanvas>();
        }

        if (arCamera == null)
        {
            arCamera = FindFirstObjectByType<Camera>();
            if (arCamera == null)
                Debug.LogError("[PostItPlacementTester] No AR camera found. Please assign one in the Inspector.");
        }
    }

    private void Update()
    {
        if (boardCanvas == null)
        {
            // BoardCanvas is instantiated at runtime (the ARTrackedImageManager's Tracked Image
            // Prefab), so it won't exist to drag into the Inspector until the target is found.
            boardCanvas = FindFirstObjectByType<BoardCanvas>();
            if (boardCanvas == null) return; // Target not detected yet.
        }

        if (!TryGetPointerDown(out Vector2 screenPosition)) return;
        if (requireBoardVisible && !boardCanvas.gameObject.activeInHierarchy) return;
        if (!TryGetLocalPoint(screenPosition, out Vector3 localPoint)) return;
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

    // Casts a ray through the screen point and intersects it with the board plane (pure math, no collider needed).
    private bool TryGetLocalPoint(Vector2 screenPosition, out Vector3 localPoint)
    {
        Transform boardTransform = boardCanvas.transform;
        Plane boardPlane = new Plane(boardTransform.up, boardTransform.position);
        Ray ray = arCamera.ScreenPointToRay(screenPosition);

        if (boardPlane.Raycast(ray, out float distance))
        {
            Vector3 worldPoint = ray.GetPoint(distance);
            localPoint = boardTransform.InverseTransformPoint(worldPoint);
            localPoint.y += surfaceOffset;
            return true;
        }

        localPoint = default;
        return false;
    }

    // Works with either input backend; a touch counts as a pointer press on mobile.
    private bool TryGetPointerDown(out Vector2 screenPosition)
    {
#if ENABLE_INPUT_SYSTEM
        Pointer pointer = Pointer.current;
        if (pointer != null && pointer.press.wasPressedThisFrame)
        {
            screenPosition = pointer.position.ReadValue();
            return true;
        }
#else
        if (Input.GetMouseButtonDown(0))
        {
            screenPosition = Input.mousePosition;
            return true;
        }
#endif
        screenPosition = default;
        return false;
    }
}