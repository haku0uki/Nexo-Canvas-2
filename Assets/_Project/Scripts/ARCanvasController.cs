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
        trackedImage = GetComponent<ARTrackedImage>();
    }

    private void OnEnable()
    {
    }

    private void Update()
    {
        if (trackedImage == null)
        {
            Debug.Log("¡El trackedImage sigue siendo NULL!");
            return;
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

                // case BoardState.Paused: OnEnterPaused(); break;
                // case BoardState.Loading: OnEnterLoading(); break;
                // case BoardState.Error: OnEnterError(); break;
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
        boardCanvas.StartListening();
    }

    private void HideBoard()
    {
        if (boardCanvas == null) return;

        if (boardCanvas.gameObject.activeSelf)
        {
            boardCanvas.gameObject.SetActive(false);
        }
    }

    /// <summary>Devuelve el estado actual del board.</summary>
    public BoardState GetCurrentState() => currentState;

    /// <summary>True si el board está visible (Tracking o Limited).</summary>
    public bool IsBoardVisible()
        => currentState == BoardState.Tracking || currentState == BoardState.Limited;
}