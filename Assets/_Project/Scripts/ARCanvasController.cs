using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class ARCanvasController : MonoBehaviour
{
    public enum BoardState
    {
        None,
        Tracking,
        Limited,
        Lost,
    }

    [Header("Referencias")]
    [SerializeField] private BoardCanvas boardCanvas;
    private ARTrackedImage trackedImage;
    private BoardState currentState = BoardState.None;

    private void Awake()
    {
        // Intentionally not fetched here: when this object is the ARTrackedImageManager's
        // Tracked Image Prefab, AR Foundation attaches ARTrackedImage right after Instantiate --
        // i.e. after this Awake already ran. See the lazy fetch in Update().
    }

    private void Update()
    {
        if (trackedImage == null)
        {
            trackedImage = GetComponent<ARTrackedImage>();
            if (trackedImage == null) return; // Not attached yet; retry next frame.
        }

        BoardState desiredState = EvaluateDesiredState();

        if (desiredState != currentState)
        {
            TransitionTo(desiredState);
        }
    }

    private void OnDisable()
    {
        TransitionTo(BoardState.Lost);
    }

    private BoardState EvaluateDesiredState()
    {
        switch (trackedImage.trackingState)
        {
            case TrackingState.Tracking:
                return BoardState.Tracking;

            case TrackingState.Limited:
                return BoardState.Limited;

            case TrackingState.None:
            default:
                return BoardState.Lost;
        }
    }

    private void TransitionTo(BoardState newState)
    {
        BoardState previousState = currentState;
        currentState = newState;

        Debug.Log($"[ARCanvasController] {previousState} -> {newState}");

        switch (newState)
        {
            case BoardState.Tracking:
                OnEnterTracking();
                break;

            case BoardState.Limited:
                OnEnterLimited();
                break;

            case BoardState.Lost:
                OnEnterLost();
                break;
        }
    }

    private void OnEnterTracking()
    {
        ShowBoard();
    }

    private void OnEnterLimited()
    {
        ShowBoard();
    }

    private void OnEnterLost()
    {
        HideBoard();
    }

    private void ShowBoard()
    {
        if (boardCanvas == null) return;

        if (!boardCanvas.gameObject.activeSelf)
        {
            boardCanvas.gameObject.SetActive(true);
        }
        boardCanvas.StartListening(); // Now a no-op if already listening -- see BoardCanvas.
    }

    private void HideBoard()
    {
        if (boardCanvas == null) return;

        if (boardCanvas.gameObject.activeSelf)
        {
            boardCanvas.gameObject.SetActive(false);
        }
    }

    public BoardState GetCurrentState() => currentState;

    public bool IsBoardVisible()
        => currentState == BoardState.Tracking || currentState == BoardState.Limited;
}