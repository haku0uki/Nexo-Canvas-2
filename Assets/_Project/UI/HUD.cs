using System;
using SheepyCreations.UIControl.Runtime;
using UnityEngine;
using UnityEngine.UIElements;

// HUD panel behaviour: forwards the "add post-it" button as a plain event, so PostItCreationFlow
// doesn't need to know anything about bridges or UI Toolkit elements.
// Tambien refleja el estado del tablero: el boton de anadir solo se ve con el tablero en
// seguimiento, y con el tablero perdido se muestra el aviso (lost-overlay).
public class HUD : PanelBehaviour
{
    [SerializeField] private ButtonBridge addPostItButton;

    [Header("Estado del tablero")]
    [Tooltip("Nombre del boton de anadir en el UXML. Es el que se oculta fuera de Tracking.")]
    [SerializeField] private string addPostItElementName = "add-postit-button";
    [Tooltip("Contenedor del aviso de tablero perdido (imagen translucida + texto).")]
    [SerializeField] private string lostOverlayName = "lost-overlay";
    [Header("Espectador")]
    [Tooltip("Nombre del bot�n de ver perfil en el UXML del HUD.")]
    [SerializeField] private string profileButtonName = "profile-button";

    private VisualElement profileButtonElement;

    public event Action AddPostItRequested;
    public event Action PanelShown;

    private UIDocumentPanel documentPanel;
    private VisualElement addPostItElement;
    private VisualElement lostOverlay;

    // Estado pedido por ARCanvasController. Se guarda hasta que el UXML esta listo y se
    // vuelve a aplicar cada vez que el panel se muestra (el arbol puede reconstruirse).
    private bool isTracking;
    // Antes del primer escaneo (estado None) el aviso ya debe verse: arranca visible.
    private bool isLost = true;
    private bool stateDirty = true;

    private UIDocumentPanel Doc
    {
        get
        {
            if (documentPanel == null) documentPanel = GetComponent<UIDocumentPanel>();
            return documentPanel;
        }
    }

    private void OnEnable()
    {
        if (addPostItButton != null) addPostItButton.Clicked += RaiseAddPostItRequested;

        ARCanvasController.StateChanged += OnBoardStateChanged;
        OnBoardStateChanged(CurrentBoardState()); // Estado actual (None si todavia no se escanea el tablero).
        stateDirty = true;
    }

    private void OnDisable()
    {
        if (addPostItButton != null) addPostItButton.Clicked -= RaiseAddPostItRequested;
        ARCanvasController.StateChanged -= OnBoardStateChanged;
    }

    // Tracking: tablero fijo y en seguimiento -> boton de anadir visible (si hay sesion) y aviso oculto.
    // None (aun no se escanea), Limited y Lost -> aviso visible y boton de anadir oculto.
    private void OnBoardStateChanged(ARCanvasController.BoardState state)
    {
        bool tracking = state == ARCanvasController.BoardState.Tracking;
        SetBoardState(tracking, !tracking);
    }

    // LastState es estatico y sobrevive a la recarga de la escena: si en esta escena el tablero aun
    // no se coloco, el estado real es None aunque LastState conserve el de la sesion anterior.
    private static ARCanvasController.BoardState CurrentBoardState()
    {
        if (BoardWorldAnchor.Instance != null && !BoardWorldAnchor.Instance.IsPlaced)
            return ARCanvasController.BoardState.None;

        return ARCanvasController.LastState;
    }

    private void Update()
    {
        if (stateDirty) TryApplyBoardState();
    }

    public override void OnPanelShowed(UIPanel ownerPanel)
    {
        base.OnPanelShowed(ownerPanel);
        stateDirty = true;
        TryApplyBoardState();
        PanelShown?.Invoke();
    }

    /// <summary>
    /// Llamado cuando cambia el estado del tablero. isTracking = en seguimiento (muestra el boton
    /// de anadir); isLost = muestra el aviso de escanear/recuperar el tablero (None, Limited o Lost).
    /// </summary>
    public void SetBoardState(bool tracking, bool lost)
    {
        if (isTracking == tracking && isLost == lost && !stateDirty) return;

        isTracking = tracking;
        isLost = lost;
        stateDirty = true;
        TryApplyBoardState();
    }

    private void TryApplyBoardState()
    {
        UIDocumentPanel doc = Doc;
        if (!ProfilePanelUtility.IsPanelReady(doc)) return;

        // Resolver referencias del UXML si cambiaron
        if (addPostItElement == null || addPostItElement.panel == null)
            doc.TryGetElement(addPostItElementName, out addPostItElement);

        if (lostOverlay == null || lostOverlay.panel == null)
            doc.TryGetElement(lostOverlayName, out lostOverlay);

        if (profileButtonElement == null || profileButtonElement.panel == null)
            doc.TryGetElement(profileButtonName, out profileButtonElement);

        // DETERMINAR SI ES ESPECTADOR (No tiene sesi�n verificada)
        bool esEspectador = AuthManager.Instance == null || !AuthManager.Instance.HasAuthenticatedUser;

        // El bot�n de a�adir solo se muestra si el tablero est� en Tracking Y el usuario NO es espectador
        if (addPostItElement != null)
        {
            addPostItElement.style.display = (isTracking && !esEspectador) ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // El bot�n de ver perfil se oculta por completo si es espectador
        if (profileButtonElement != null)
        {
            profileButtonElement.style.display = esEspectador ? DisplayStyle.None : DisplayStyle.Flex;
        }

        if (lostOverlay != null)
            lostOverlay.style.display = isLost ? DisplayStyle.Flex : DisplayStyle.None;

        stateDirty = false;
    }


    private void RaiseAddPostItRequested()
    {
        // Escudo de seguridad: si es espectador, no disparar el flujo de creaci�n
        if (AuthManager.Instance == null || !AuthManager.Instance.HasAuthenticatedUser)
        {
            Debug.LogWarning("[HUD] Intento de publicaci�n bloqueado para un usuario Espectador.");
            return;
        }

        AddPostItRequested?.Invoke();
    }

}