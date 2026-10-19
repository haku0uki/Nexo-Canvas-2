using System;
using UnityEngine;
using UnityEngine.UIElements;
using SheepyCreations.UIControl.Runtime;

public class PostItCreationFlow : MonoBehaviour
{
    private enum FlowState { Idle, SelectingType, SelectingPosition, Writing, Drawing }

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
    [Tooltip("ButtonBridge on the type selection panel targeting the 'dibuja' element.")]
    [SerializeField] private ButtonBridge addDrawingButton;
    [Tooltip("Panel shown after pressing Add on the HUD.")]
    [SerializeField] private UIDocumentPanel typeSelectionPanel;
    [Tooltip("ButtonBridge on the type selection panel targeting the 'escribe' element.")]
    [SerializeField] private ButtonBridge addTextButton;

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

    [Header("Detail panels")]
    [Tooltip("The text and the drawing detail panels. All of them are blocked from opening during the whole flow, so tapping an existing post-it while placing a new one doesn't open its detail.")]
    [SerializeField] private PostItDetailPanel[] detailPanels;

    [Header("Behaviour")]
    [SerializeField] private float surfaceOffset = 0.01f;

    private TextField textInput;
    private FlowState state = FlowState.Idle;
    private PostItKind pendingKind = PostItKind.Text;
    private Vector3 selectedLocalPoint;
    private Vector2Int selectedCell;
    private bool isPublishing;
    private bool hudCurrentlyShown;

    private void Awake()
    {
        if (arCamera == null) arCamera = Camera.main;
    }

    private void OnEnable()
    {
        if (hud != null) hud.AddPostItRequested += ShowTypeSelection;
        if (addTextButton != null) addTextButton.Clicked += BeginTextPlacement;
        if (addDrawingButton != null) addDrawingButton.Clicked += BeginDrawingPlacement;
        if (confirmButton != null) confirmButton.Clicked += ConfirmPlacement;
        if (cancelButton != null) cancelButton.Clicked += CancelPlacement;

        if (drawEntry != null)
        {
            drawEntry.onPublished.AddListener(OnDrawingFinished);
            drawEntry.onCancelled.AddListener(OnDrawingFinished);
            drawEntry.onCellOccupied.AddListener(OnDrawingCellOccupied);
        }
    }

    private void OnDisable()
    {
        if (hud != null) hud.AddPostItRequested -= ShowTypeSelection;
        if (addTextButton != null) addTextButton.Clicked -= BeginTextPlacement;
        if (addDrawingButton != null) addDrawingButton.Clicked -= BeginDrawingPlacement;
        if (confirmButton != null) confirmButton.Clicked -= ConfirmPlacement;
        if (cancelButton != null) cancelButton.Clicked -= CancelPlacement;

        if (drawEntry != null)
        {
            drawEntry.onPublished.RemoveListener(OnDrawingFinished);
            drawEntry.onCancelled.RemoveListener(OnDrawingFinished);
            drawEntry.onCellOccupied.RemoveListener(OnDrawingCellOccupied);
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

    private void UpdateHudVisibility()
    {
        if (hudPanel == null || !hudPanel.IsInitialized) return;

        bool shouldShow = boardCanvas != null && boardCanvas.gameObject.activeInHierarchy && state == FlowState.Idle;
        if (shouldShow == hudCurrentlyShown) return;

        hudCurrentlyShown = shouldShow;
        if (shouldShow) ShowFlowPanel(hudPanel); else HideFlowPanel(hudPanel);
    }

    private void ShowTypeSelection()
    {
        if (state != FlowState.Idle || boardCanvas == null || typeSelectionPanel == null) return;

        state = FlowState.SelectingType;
        ShowFlowPanel(typeSelectionPanel);
    }

    private void BeginTextPlacement() => BeginPlacement(PostItKind.Text);

    private void BeginDrawingPlacement() => BeginPlacement(PostItKind.Drawing);

    private void BeginPlacement(PostItKind kind)
    {
        if (state != FlowState.SelectingType || boardCanvas == null) return;
        pendingKind = kind;
        state = FlowState.SelectingPosition;
        HideFlowPanel(typeSelectionPanel);
        SetDetailPanelBlocked(true);
        if (placementPreview != null) placementPreview.gameObject.SetActive(true);
    }

    private void HandlePositionSelection()
    {
        if (!PointerInput.TryGetPointerDown(out Vector2 screenPosition)) return;
        if (IsPointerOverUI(screenPosition)) return; // A tap on UI controls must never count as a board tap.
        if (!BoardRaycaster.TryGetLocalPoint(boardCanvas, arCamera, screenPosition, surfaceOffset, out Vector3 localPoint)) return;
        if (!boardCanvas.TryGetCell(localPoint, out Vector2Int cell)) return; // Ignore taps outside the paper; keep selecting.

        if (boardCanvas.IsCellOccupied(cell))
        {
            Debug.Log($"PostItCreationFlow: la celda {cell} ya está ocupada.");
            return; // Keep selecting; add visual feedback here if desired.
        }

        selectedCell = cell;
        selectedLocalPoint = boardCanvas.CellToLocalPoint(cell, surfaceOffset); // Cell center, not the raw tap.

        if (placementPreview != null)
            placementPreview.position = boardCanvas.transform.TransformPoint(selectedLocalPoint);

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
        // DrawEntry publishes the post-it at this board position, in this cell.
        drawEntry.Configure(selectedLocalPoint, boardCanvas.GetCellId(selectedCell));
        ShowFlowPanel(drawPanel);
    }

    private void OnDrawingFinished()
    {
        if (state != FlowState.Drawing) return;
        ReturnToIdle();
    }

    // Another user took the cell while this one was drawing. The drawing is kept
    // (DrawEntry does not clear it), so the user only has to pick another cell.
    private void OnDrawingCellOccupied()
    {
        if (state != FlowState.Drawing) return;

        HideFlowPanel(drawPanel);
        state = FlowState.SelectingPosition;
    }

    private void ConfirmPlacement()
    {
        if (isPublishing) return; // Ignore double taps while the transaction is in flight.

        string texto = textInput != null ? textInput.value?.Trim() : null;
        if (string.IsNullOrEmpty(texto)) return; // Nothing to publish; stay in Writing.

        // Quick local check: someone else may have taken the cell while this user was writing.
        if (boardCanvas.IsCellOccupied(selectedCell))
        {
            BackToPositionSelection();
            return;
        }

        string autorId = AuthManager.Instance.CurrentUser != null
            ? AuthManager.Instance.CurrentUser.Email
            : "anonimo";

        isPublishing = true;
        bool sent = FirestoreService.Instance.PublicarPostIt(
            autorId, "texto", texto, selectedLocalPoint,
            boardCanvas.GetCellId(selectedCell), OnPublishResult);

        if (!sent) isPublishing = false; // Firestore not ready; stay in Writing without losing the text.
    }

    private void OnPublishResult(ResultadoPublicacion resultado)
    {
        isPublishing = false;
        if (state != FlowState.Writing) return; // The user cancelled in the meantime.

        switch (resultado)
        {
            case ResultadoPublicacion.Ok:
                ReturnToIdle();
                break;

            case ResultadoPublicacion.CeldaOcupada:
                BackToPositionSelection(); // Another user won the cell: pick a different one.
                break;

            case ResultadoPublicacion.Error:
                break; // Stay in Writing so the user can retry without losing the text.
        }
    }

    private void BackToPositionSelection()
    {
        HideFlowPanel(composerPanel);
        state = FlowState.SelectingPosition;
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
        HideFlowPanel(composerPanel);
        HideFlowPanel(drawPanel);
        HideFlowPanel(typeSelectionPanel);
        hudPanel.ShowIsolatedViaController();
    }

    private void SetDetailPanelBlocked(bool blocked)
    {
        if (detailPanels == null) return;

        foreach (PostItDetailPanel detailPanel in detailPanels)
        {
            if (detailPanel != null) detailPanel.IsOpeningBlocked = blocked;
        }
    }

    private void ResolveTextInput()
    {
        if (textInput != null || composerPanel == null) return;
        composerPanel.TryGetElement(textInputName, out textInput);
    }

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
            || PanelContainsPoint(typeSelectionPanel, screenPosition)
            || PanelContainsPoint(composerPanel, screenPosition)
            || PanelContainsPoint(drawPanel, screenPosition);
    }

    private bool PanelContainsPoint(UIDocumentPanel uiPanel, Vector2 screenPosition)
    {
        if (uiPanel == null || uiPanel.RootVisualElement == null) return false;

        VisualElement root = uiPanel.RootVisualElement;
        if (root.panel == null || root.resolvedStyle.display == DisplayStyle.None) return false;

        Vector2 uiScreenPosition = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
        Vector2 panelPosition = RuntimePanelUtils.ScreenToPanel(root.panel, uiScreenPosition);
        VisualElement picked = root.panel.Pick(panelPosition);

        if (picked == null || (picked != root && !root.Contains(picked))) return false;

        for (VisualElement element = picked; element != null; element = element.parent)
        {
            if (element is Button || element is TextField) return true;
            if (element == root) break;
        }

        return false;
    }
}