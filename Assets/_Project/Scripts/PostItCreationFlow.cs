using UnityEngine;
using UnityEngine.UIElements;
using SheepyCreations.UIControl.Runtime;
using IngameDebugConsole;

public class PostItCreationFlow : MonoBehaviour
{
    private enum FlowState { Idle, SelectingPosition, Writing }

    [Header("References")]
    [Tooltip("Optional: leave empty with AR Foundation, since BoardCanvas only exists once the tracked image prefab is instantiated.")]
    [SerializeField] private BoardCanvas boardCanvas;
    [Tooltip("Leave empty to use Camera.main (the AR camera).")]
    [SerializeField] private Camera arCamera;
    [Tooltip("Optional: shown while selecting a position, moved to the current tap point.")]
    [SerializeField] private Transform placementPreview;

    [Header("UI -- adjust names to your UXML")]
    [Tooltip("Panel holding the 'Agregar nota' button. Shown while the board is tracked.")]
    [SerializeField] private UIDocumentPanel hudPanel;
    [SerializeField] private string addButtonName = "add-postit-button";
    [Tooltip("Panel with the text input and confirm/cancel buttons. Hidden until a position is picked.")]
    [SerializeField] private UIDocumentPanel composerPanel;
    [SerializeField] private string textInputName = "postit-text-input";
    [SerializeField] private string confirmButtonName = "confirm-postit-button";
    [SerializeField] private string cancelButtonName = "cancel-postit-button";

    [Header("Behaviour")]
    [SerializeField] private float surfaceOffset = 0.01f;

    private Button addButton;
    private TextField textInput;
    private Button confirmButton;
    private Button cancelButton;
    private bool elementsResolved;

    private FlowState state = FlowState.Idle;
    private Vector3 selectedLocalPoint;

    private void Awake()
    {
        if (arCamera == null) arCamera = Camera.main;

        DebugLogConsole.AddCommand("print_references_state", "Prints the current state of references in PostItCreationFlow.", PrintReferencesState);
    }

    private void OnDestroy()
    {
        DebugLogConsole.RemoveCommand("print_references_state");
    }

    private void Update()
    {
        if (boardCanvas == null)
        {
            boardCanvas = FindFirstObjectByType<BoardCanvas>();
        }

        ResolveElements();
        UpdateAddButtonAvailability();

        if (state == FlowState.SelectingPosition)
        {
            HandlePositionSelection();
        }
    }

    // Same lazy-resolve pattern used elsewhere: the document root may not exist on the first frame.
    private void ResolveElements()
    {
        if (elementsResolved) return;
        if (hudPanel == null || hudPanel.RootVisualElement == null) return;
        if (composerPanel == null || composerPanel.RootVisualElement == null) return;

        if (hudPanel.TryGetElement(addButtonName, out addButton))
            addButton.clicked += BeginPlacement;

        composerPanel.TryGetElement(textInputName, out textInput);

        if (composerPanel.TryGetElement(confirmButtonName, out confirmButton))
            confirmButton.clicked += ConfirmPlacement;

        if (composerPanel.TryGetElement(cancelButtonName, out cancelButton))
            cancelButton.clicked += CancelPlacement;

        composerPanel.Hide();
        elementsResolved = true;
    }

    private void UpdateAddButtonAvailability()
    {
        if (addButton == null) return;
        bool boardVisible = boardCanvas != null && boardCanvas.gameObject.activeInHierarchy;
        addButton.SetEnabled(boardVisible && state == FlowState.Idle);
    }

    private void BeginPlacement()
    {
        print("BeginPlacement called");
        if (state != FlowState.Idle || boardCanvas == null) { print("Cannot begin placement: " + state + " / " + boardCanvas); return; }
        state = FlowState.SelectingPosition;
        if (placementPreview != null) placementPreview.gameObject.SetActive(true);
    }

    private void HandlePositionSelection()
    {
        if (!PointerInput.TryGetPointerDown(out Vector2 screenPosition)) return;
        if (IsPointerOverUI(screenPosition)) return; // A tap on the HUD must never count as a board tap.
        if (!BoardRaycaster.TryGetLocalPoint(boardCanvas, arCamera, screenPosition, surfaceOffset, out Vector3 localPoint)) return;
        if (!boardCanvas.IsInsideBoard(localPoint)) return; // Ignore taps outside the paper; keep selecting.

        selectedLocalPoint = localPoint;
        if (placementPreview != null)
            placementPreview.position = boardCanvas.transform.TransformPoint(localPoint);

        BeginWriting();
    }

    private void BeginWriting()
    {
        state = FlowState.Writing;
        if (textInput != null) textInput.value = string.Empty;
        composerPanel.Show();
    }

    private void ConfirmPlacement()
    {
        print("ConfirmPlacement called");
        print("TextInput value: " + (textInput != null ? textInput.value : "null"));
        string texto = textInput != null ? textInput.value?.Trim() : null;
        if (string.IsNullOrEmpty(texto)) return; // Nothing to publish; stay in Writing.

        string autorId = AuthManager.Instance.CurrentUser != null
            ? AuthManager.Instance.CurrentUser.Email
            : "anonimo";

        FirestoreService.Instance.PublicarPostIt(autorId, "texto", texto, selectedLocalPoint);
        ReturnToIdle();
    }

    private void CancelPlacement()
    {
        ReturnToIdle();
    }

    private void ReturnToIdle()
    {
        state = FlowState.Idle;
        if (placementPreview != null) placementPreview.gameObject.SetActive(false);
        composerPanel.Hide();
    }

    private bool IsPointerOverUI(Vector2 screenPosition)
    {
        return PanelContainsPoint(hudPanel, screenPosition) || PanelContainsPoint(composerPanel, screenPosition);
    }

    private bool PanelContainsPoint(UIDocumentPanel uiPanel, Vector2 screenPosition)
    {
        if (uiPanel == null || uiPanel.RootVisualElement == null) return false;

        VisualElement root = uiPanel.RootVisualElement;
        if (root.panel == null || root.resolvedStyle.display == DisplayStyle.None) return false;

        // PointerInput uses bottom-left screen coordinates; UI Toolkit uses top-left.
        Vector2 uiScreenPosition = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
        Vector2 panelPosition = RuntimePanelUtils.ScreenToPanel(root.panel, uiScreenPosition);
        VisualElement picked = root.panel.Pick(panelPosition);

        // Documents can share a panel. Only this document's controls should block placement;
        // transparent full-screen containers must allow taps on the AR board.
        if (picked == null || (picked != root && !root.Contains(picked))) return false;

        for (VisualElement element = picked; element != null; element = element.parent)
        {
            if (element is Button || element is TextField) return true;
            if (element == root) break;
        }

        return false;
    }

    private void PrintReferencesState()
    {
        Debug.Log(
            "References state: " +
            "placementPreview: " + placementPreview + " \n " +
            "textInput: " + textInput + " \n " +
            "composerPanel: " + composerPanel
        );
    }
}
