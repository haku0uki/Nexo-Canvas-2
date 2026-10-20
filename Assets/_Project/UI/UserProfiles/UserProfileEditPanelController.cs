using System;
using SheepyCreations.UIControl.Runtime;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements;

// Ventana para editar el perfil del usuario conectado (nickname, pronombres y dibujo).
// El teclado movil NO se maneja aqui: agrega PanelKeyboardAvoider al mismo GameObject.
//
// Se abre con Open() (o desde el boton de editar del perfil) y avisa con Saved / Closed.
public class UserProfileEditPanelController : MonoBehaviour
{
    [Header("PANEL")]
    [Tooltip("Si queda vacio se usa el UIDocumentPanel de este GameObject.")]
    [SerializeField] private UIDocumentPanel panel;

    [Header("TEXTOS (LabelTextBridge)")]
    [Tooltip("Elemento: profile-status-label")]
    [SerializeField] private LabelTextBridge statusLabel;

    [Header("BOTONES (ButtonBridge)")]
    [Tooltip("Elemento: profile-save-button")]
    [SerializeField] private ButtonBridge saveButton;
    [Tooltip("Elemento: profile-cancel-button")]
    [SerializeField] private ButtonBridge cancelButton;
    [Tooltip("Elemento: profile-change-drawing-button")]
    [SerializeField] private ButtonBridge changeDrawingButton;

    [Header("CAMPOS (por nombre, para poder enfocarlos y abrir el teclado)")]
    [SerializeField] private string nicknameFieldName = "profile-nickname-field";
    [SerializeField] private string pronounsFieldName = "profile-pronouns-field";
    [SerializeField] private string avatarContainerName = "profile-avatar-drawing";

    [Header("EDITOR DE DIBUJO (opcional)")]
    [Tooltip("Si se asigna, al pulsar Cambiar dibujo se carga el dibujo actual en este DrawTest.")]
    [SerializeField] private DrawTest drawTest;
    [Tooltip("Conectalo para abrir tu panel de dibujo. Al terminar, llama a SaveDrawingFromEditor().")]
    public UnityEvent onEditDrawingRequested;

    [Header("DIBUJO")]
    [SerializeField] private float drawingUnitsWidth = 10f;
    [SerializeField] private float drawingPadding = 8f;

    /// <summary>Se dispara cuando el nickname, los pronombres o el dibujo se guardaron.</summary>
    public event Action Saved;
    /// <summary>Se dispara cuando la ventana se cierra.</summary>
    public event Action Closed;

    private TextField nicknameField;
    private TextField pronounsField;
    private DrawingSurface avatarSurface;

    // Datos que esperan a que la UI este lista para volcarse.
    private UserProfileService.UserProfileData pendingProfile;
    private string currentDrawingJson = string.Empty;
    private bool avatarDirty;
    private string status = string.Empty;
    private bool statusDirty;
    private int loadToken;

    public bool IsVisible => panel != null && panel.IsVisible;


    // ============================================================
    // UNITY
    // ============================================================

    private void Awake()
    {
        if (panel == null)
            panel = GetComponent<UIDocumentPanel>();
    }

    private void OnEnable()
    {
        if (saveButton != null) saveButton.Clicked += Save;
        if (cancelButton != null) cancelButton.Clicked += Close;
        if (changeDrawingButton != null) changeDrawingButton.Clicked += RequestDrawingEdit;

        if (AuthManager.Instance != null)
            AuthManager.Instance.OnUserLoggedOut += HandleLoggedOut;
    }

    private void OnDisable()
    {
        if (saveButton != null) saveButton.Clicked -= Save;
        if (cancelButton != null) cancelButton.Clicked -= Close;
        if (changeDrawingButton != null) changeDrawingButton.Clicked -= RequestDrawingEdit;

        if (AuthManager.Instance != null)
            AuthManager.Instance.OnUserLoggedOut -= HandleLoggedOut;

        avatarSurface?.RemoveFromHierarchy();
        avatarSurface = null;
    }

    private void Update()
    {
        if (pendingProfile != null || avatarDirty || statusDirty)
            TryApply();
    }


    // ============================================================
    // API PUBLICA
    // ============================================================

    /// <summary>Muestra la ventana y carga el perfil del usuario conectado. Se puede enganchar a ButtonBridge.onClick.</summary>
    public void Open()
    {
        if (!ProfilePanelUtility.TryGetSessionUser(out Firebase.Auth.FirebaseUser user))
        {
            Debug.LogWarning("UserProfileEditPanelController: hay que iniciar sesion con un correo verificado.");
            return;
        }

        if (UserProfileService.Instance == null)
        {
            Debug.LogError("UserProfileEditPanelController: falta un GameObject con UserProfileService.");
            return;
        }

        ProfilePanelUtility.ShowFlowPanel(panel);
        SetStatus("Cargando perfil…");

        int token = ++loadToken;

        UserProfileService.Instance.EnsureProfileExists(user, ok =>
        {
            if (token != loadToken) return;

            if (!ok)
            {
                SetStatus("No se pudo cargar el perfil.");
                return;
            }

            UserProfileService.Instance.LoadProfile(user, profile =>
            {
                if (token != loadToken) return;

                if (profile == null)
                {
                    SetStatus("No se pudo cargar el perfil.");
                    return;
                }

                pendingProfile = profile;
                currentDrawingJson = profile.drawingJson ?? string.Empty;
                avatarDirty = true;
                SetStatus(string.Empty);
            });
        });
    }

    /// <summary>Cierra la ventana sin guardar.</summary>
    public void Close()
    {
        loadToken++; // Ignora respuestas que lleguen despues de cerrar.

        nicknameField?.Blur();
        pronounsField?.Blur();

        if (!IsVisible) return;

        ProfilePanelUtility.HideFlowPanel(panel);
        Closed?.Invoke();
    }

    /// <summary>Guarda nickname y pronombres.</summary>
    public void Save()
    {
        if (!ProfilePanelUtility.TryGetSessionUser(out Firebase.Auth.FirebaseUser user) ||
            UserProfileService.Instance == null)
        {
            SetStatus("No hay una sesion valida para guardar.");
            return;
        }

        if (!ResolveFields())
        {
            SetStatus("La ventana todavia no esta lista.");
            return;
        }

        SetStatus("Guardando…");

        UserProfileService.Instance.SaveProfile(user, nicknameField.value, pronounsField.value, ok =>
        {
            SetStatus(ok ? "Perfil guardado." : "No se pudo guardar el perfil.");
            if (ok) Saved?.Invoke();
        });
    }

    /// <summary>Guarda el dibujo que hay en el DrawTest asignado. Conectalo al boton Listo de tu editor de dibujo.</summary>
    public void SaveDrawingFromEditor()
    {
        if (drawTest == null)
        {
            Debug.LogError("UserProfileEditPanelController: no hay DrawTest asignado.");
            return;
        }

        // Un lienzo vacio se guarda como "sin dibujo".
        SaveProfileDrawingJson(drawTest.HasDrawing() ? drawTest.GetDrawingJson() : string.Empty);
    }

    /// <summary>Guarda un dibujo ya serializado con DrawingSerializer.</summary>
    public void SaveProfileDrawingJson(string drawingJson)
    {
        if (!ProfilePanelUtility.TryGetSessionUser(out Firebase.Auth.FirebaseUser user) ||
            UserProfileService.Instance == null)
        {
            SetStatus("No hay una sesion valida para guardar el dibujo.");
            return;
        }

        SetStatus("Guardando dibujo…");

        UserProfileService.Instance.SaveProfileDrawing(user, drawingJson, ok =>
        {
            if (!ok)
            {
                SetStatus("No se pudo guardar el dibujo de perfil.");
                return;
            }

            currentDrawingJson = drawingJson ?? string.Empty;
            avatarDirty = true;
            SetStatus("Dibujo de perfil guardado.");
            Saved?.Invoke();
        });
    }


    // ============================================================
    // BOTONES Y EVENTOS
    // ============================================================

    private void RequestDrawingEdit()
    {
        if (drawTest != null)
            drawTest.LoadDrawingData(ProfilePanelUtility.ParseDrawing(currentDrawingJson) ?? new DrawingData());

        onEditDrawingRequested?.Invoke();
    }

    private void HandleLoggedOut()
    {
        pendingProfile = null;
        currentDrawingJson = string.Empty;
        Close();
    }


    // ============================================================
    // UI
    // ============================================================

    private void SetStatus(string message)
    {
        status = message ?? string.Empty;
        statusDirty = true;
        TryApply();
    }

    // Busca los TextField por nombre (no hay TextFieldBridge porque ese no permite Focus()).
    private bool ResolveFields()
    {
        if (!ProfilePanelUtility.IsPanelReady(panel)) return false;

        if (nicknameField == null || nicknameField.panel == null)
            panel.TryGetElement(nicknameFieldName, out nicknameField);

        if (pronounsField == null || pronounsField.panel == null)
            panel.TryGetElement(pronounsFieldName, out pronounsField);

        return nicknameField != null && pronounsField != null;
    }

    private void TryApply()
    {
        if (!ProfilePanelUtility.IsPanelReady(panel)) return;
        if (ProfilePanelUtility.IsUnbound(statusLabel)) return;

        if (statusDirty)
        {
            if (statusLabel != null) statusLabel.SetText(status);
            statusDirty = false;
        }

        if (pendingProfile != null && ResolveFields())
        {
            nicknameField.SetValueWithoutNotify(pendingProfile.nickname ?? string.Empty);
            pronounsField.SetValueWithoutNotify(pendingProfile.pronouns ?? string.Empty);
            pendingProfile = null;
        }

        if (avatarDirty)
            RenderAvatar();
    }

    private void RenderAvatar()
    {
        if (!panel.TryGetElement(avatarContainerName, out VisualElement container)) return;

        if (avatarSurface == null || avatarSurface.parent != container)
        {
            avatarSurface?.RemoveFromHierarchy();
            avatarSurface = ProfilePanelUtility.CreateReadOnlySurface(drawingUnitsWidth, drawingPadding);
            container.Add(avatarSurface);
        }

        avatarSurface.SetDrawing(ProfilePanelUtility.ParseDrawing(currentDrawingJson));
        avatarDirty = false;
    }
}