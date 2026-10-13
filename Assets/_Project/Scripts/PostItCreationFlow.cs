using System;
using UnityEngine;
using UnityEngine.UIElements;
using SheepyCreations.UIControl.Runtime;

// Real "add a post-it" flow:
//   Idle -> SelectingPosition (tap the board) -> Writing (type text)  -> publish   [text]
//   Idle -> SelectingPosition (tap the board) -> Drawing (draw panel) -> publish   [drawing]
// Do NOT run this at the same time as PostItPlacementTester; see that file's header.
public class PostItCreationFlow : MonoBehaviour
{
    private enum FlowState { Idle, SelectingPosition, Writing, Drawing }

    private enum PostItKind { Text, Drawing }

    [Header("References")]
    [Tooltip("Optional: leave empty with AR Foundation, since BoardCanvas only exists once the tracked image prefab is instantiated.")]
    [SerializeField] private BoardCanvas boardCanvas;
    [Tooltip("Leave empty to use Camera.main (the AR camera).")]
    [SerializeField] private Camera arCamera;
    [Tooltip("Optional: shown while selecting a position, moved to the current tap point.")]
    [SerializeField] private Transform placementPreview;

    [Header("HUD -- shown only while the board is tracked and nothing else is in progress")]
    [SerializeField] private UIDocumentPanel hudPanel;
    [SerializeField] private HUD hud;
    [Tooltip("ButtonBridge on the HUD panel targeting the 'add-drawing-button' element of the HUD UXML.")]
    [SerializeField] private ButtonBridge addDrawingButton;

    [Header("Composer -- shown while writing the post-it text")]
    [SerializeField] private UIDocumentPanel composerPanel;
    [Tooltip("Resolved by name instead of through a TextFieldBridge so the field can be focused (opens the on-screen keyboard).")]
    [SerializeField] private string textInputName = "postit-text-input";
    [SerializeField] private ButtonBridge confirmButton;
    [SerializeField] private ButtonBridge cancelButton;

    [Header("Drawing -- shown while drawing the post-it")]
    [Tooltip("The DrawEntryPanel (UIDocumentPanel on the same object as DrawEntry).")]
    [SerializeField] private UIDocumentPanel drawPanel;
    [Tooltip("Publishes the drawing itself; this flow only gives it the board position and listens for Published / Cancelled.")]
    [SerializeField] private DrawEntry drawEntry;

    [Header("Detail panel")]
    [Tooltip("Blocked from opening during the whole flow, so tapping an existing post-it while placing a new one doesn't open its detail.")]
    [SerializeField] private PostItDetailPanel detailPanel;

    [Header("Behaviour")]
    [SerializeField] private float surfaceOffset = 0.01f;

    private TextField textInput;
    private FlowState state = FlowState.Idle;
    private PostItKind pendingKind = PostItKind.Text;
    private Vector3 selectedLocalPoint;
    private bool hudCurrentlyShown;

    private void Awake()
    {
        if (arCamera == null) arCamera = Camera.main;
    }

    private void OnEnable()
    {
        if (hud != null) hud.AddPostItRequested += BeginTextPlacement;
        if (addDrawingButton != null) addDrawingButton.Clicked += BeginDrawingPlacement;
        if (confirmButton != null) confirmButton.Clicked += ConfirmPlacement;
        if (cancelButton != null) cancelButton.Clicked += CancelPlacement;

        if (drawEntry != null)
        {
            drawEntry.onPublished.AddListener(OnDrawingFinished);
            drawEntry.onCancelled.AddListener(OnDrawingFinished);
        }
    }

    private void OnDisable()
    {
        if (hud != null) hud.AddPostItRequested -= BeginTextPlacement;
        if (addDrawingButton != null) addDrawingButton.Clicked -= BeginDrawingPlacement;
        if (confirmButton != null) confirmButton.Clicked -= ConfirmPlacement;
        if (cancelButton != null) cancelButton.Clicked -= CancelPlacement;

        if (drawEntry != null)
        {
            drawEntry.onPublished.RemoveListener(OnDrawingFinished);
            drawEntry.onCancelled.RemoveListener(OnDrawingFinished);
        }

        SetDetailPanelBlocked(false); // Never leave the detail panel blocked if this component is disabled mid-flow.
    }

    private void Update()
    {
        if (boardCanvas == null)
        {
            boardCanvas = FindFirstObjectByType<BoardCanvas>();
        }

        UpdateHudVisibility();

        if (state == FlowState.SelectingPosition)
        {
            HandlePositionSelection();
        }
    }

    // The HUD only makes sense while the board is tracked and nothing else is in progress.
    private void UpdateHudVisibility()
    {
        if (hudPanel == null || !hudPanel.IsInitialized) return;

        bool shouldShow = boardCanvas != null && boardCanvas.gameObject.activeInHierarchy && state == FlowState.Idle;
        if (shouldShow == hudCurrentlyShown) return;

        hudCurrentlyShown = shouldShow;
        if (shouldShow) ShowFlowPanel(hudPanel); else HideFlowPanel(hudPanel);
    }

    // Both HUD buttons start the same placement step; only what happens after the tap differs.
    private void BeginTextPlacement() => BeginPlacement(PostItKind.Text);

    private void BeginDrawingPlacement() => BeginPlacement(PostItKind.Drawing);

    private void BeginPlacement(PostItKind kind)
    {
        if (state != FlowState.Idle || boardCanvas == null) return;
        pendingKind = kind;
        state = FlowState.SelectingPosition;
        SetDetailPanelBlocked(true);
        if (placementPreview != null) placementPreview.gameObject.SetActive(true);
    }

    private void HandlePositionSelection()
    {
        if (!PointerInput.TryGetPointerDown(out Vector2 screenPosition)) return;
        if (IsPointerOverUI(screenPosition)) return; // A tap on UI controls must never count as a board tap.
        if (!BoardRaycaster.TryGetLocalPoint(boardCanvas, arCamera, screenPosition, surfaceOffset, out Vector3 localPoint)) return;
        if (!boardCanvas.IsInsideBoard(localPoint)) return; // Ignore taps outside the paper; keep selecting.

        selectedLocalPoint = localPoint;
        if (placementPreview != null)
            placementPreview.position = boardCanvas.transform.TransformPoint(localPoint);

        if (pendingKind == PostItKind.Drawing) BeginDrawing(); else BeginWriting();
    }

    private void BeginWriting()
    {
        state = FlowState.Writing;
        ResolveTextInput();
        if (textInput != null) textInput.value = string.Empty;

        ShowFlowPanel(composerPanel, () =>
        {
            if (state == FlowState.Writing) textInput?.Focus();
        });
    }

    private void BeginDrawing()
    {
        if (drawEntry == null || drawPanel == null)
        {
            Debug.LogError("PostItCreationFlow: Draw Entry / Draw Panel are not assigned.");
            ReturnToIdle();
            return;
        }

        state = FlowState.Drawing;
        drawEntry.Configure(selectedLocalPoint); // DrawEntry publishes the post-it at this board position.
        ShowFlowPanel(drawPanel);
    }

    // DrawEntry already published (or cancelled) by itself; the flow only has to close the panel.
    private void OnDrawingFinished()
    {
        if (state != FlowState.Drawing) return;
        ReturnToIdle();
    }

    private void ConfirmPlacement()
    {
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
        SetDetailPanelBlocked(false);
        if (placementPreview != null) placementPreview.gameObject.SetActive(false);
        HideFlowPanel(composerPanel); // The HUD comes back on its own via UpdateHudVisibility.
        HideFlowPanel(drawPanel);
        hudPanel.ShowIsolatedViaController();
    }

    private void SetDetailPanelBlocked(bool blocked)
    {
        if (detailPanel != null) detailPanel.IsOpeningBlocked = blocked;
    }

    private void ResolveTextInput()
    {
        if (textInput != null || composerPanel == null) return;
        composerPanel.TryGetElement(textInputName, out textInput);
    }

    // Route visibility through the owner so controller rules and events stay consistent.
    // Standalone panels still work without an extra Inspector reference.
    private static void ShowFlowPanel(UIPanel panel, Action onComplete = null)
    {
        if (panel == null) return;

        if (panel.HasController)
            panel.Controller.ShowPanel(panel, onComplete: onComplete);
        else
            panel.Show(onComplete);
    }

    private static void HideFlowPanel(UIPanel panel, Action onComplete = null)
    {
        if (panel == null) return;

        if (panel.HasController)
            panel.Controller.HidePanel(panel, fadeDurationSeconds: 0f, onComplete: onComplete);
        else
            panel.Hide(0f, onComplete);
    }

    private bool IsPointerOverUI(Vector2 screenPosition)
    {
        return PanelContainsPoint(hudPanel, screenPosition)
            || PanelContainsPoint(composerPanel, screenPosition)
            || PanelContainsPoint(drawPanel, screenPosition);
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

        // Documents can share a panel, and a visible UIDocumentPanel root can pick the whole screen,
        // so only actual controls block placement; empty areas must let the tap reach the AR board.
        if (picked == null || (picked != root && !root.Contains(picked))) return false;

        for (VisualElement element = picked; element != null; element = element.parent)
        {
            if (element is Button || element is TextField) return true;
            if (element == root) break;
        }

        return false;
    }
}