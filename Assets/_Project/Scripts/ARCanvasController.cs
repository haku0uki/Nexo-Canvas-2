using System;
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

    // Estado del tablero para quien lo necesite (el HUD, por ejemplo) sin referencias de escena:
    // este componente vive en un prefab que crea AR Foundation, que no puede apuntar a objetos de la escena.
    public static event Action<BoardState> StateChanged;
    public static BoardState LastState { get; private set; } = BoardState.None;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        StateChanged = null;
        LastState = BoardState.None;
    }

    [Header("Referencias")]
    [SerializeField] private BoardCanvas boardCanvas;
    private ARTrackedImage trackedImage;
    private BoardState currentState = BoardState.None;

    private void Awake()
    {
        // Keep the lazy lookup so the controller also tolerates prefabs instantiated before
        // AR Foundation has finished attaching their trackable component.
    }

    private void Update()
    {
        if (trackedImage == null)
        {
            trackedImage = GetComponent<ARTrackedImage>();
            if (trackedImage == null) return; // Not attached yet; retry next frame.
        }

        // Con BoardWorldAnchor en la escena, el tablero queda fijo en el espacio y la imagen solo
        // sirve para colocarlo y realinearlo; el estado lo decide el ancla.
        if (BoardWorldAnchor.Instance != null)
        {
            BoardWorldAnchor.Instance.ReportImage(boardCanvas, transform, trackedImage.trackingState);
            return;
        }

        BoardState desiredState = EvaluateDesiredState();

        if (desiredState != currentState)
        {
            TransitionTo(desiredState);
        }
    }

    // Llamado por BoardWorldAnchor cuando el estado del tablero cambia.
    public static void PublishState(BoardState newState)
    {
        LastState = newState;
        StateChanged?.Invoke(newState);
    }

    private void OnDisable()
    {
        // Con el tablero anclado al espacio, perder o destruir la imagen no lo oculta.
        if (BoardWorldAnchor.Instance != null) return;

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

        PublishState(newState);
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

    public BoardState GetCurrentState() => BoardWorldAnchor.Instance != null ? LastState : currentState;

    public bool IsBoardVisible()
        => GetCurrentState() == BoardState.Tracking || GetCurrentState() == BoardState.Limited;
}