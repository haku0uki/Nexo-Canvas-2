using Firebase.Auth;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>Permite editar únicamente el perfil del usuario autenticado.</summary>
[RequireComponent(typeof(UIDocument))]
public class UserProfileEditPanelController : MonoBehaviour
{
    [SerializeField] private float drawingUnitsWidth = 10f;
    [SerializeField] private float drawingPadding = 8f;

    private UIDocument document;
    private VisualElement root;
    private VisualElement avatarContainer;
    private TextField nicknameField;
    private TextField pronounsField;
    private Label emailLabel;
    private Label statusLabel;
    private Button saveButton;
    private Button clearDrawingButton;
    private DrawingSurface avatarSurface;
    private FirebaseUser currentUser;
    private bool eventsBound;

    private void Awake() => document = GetComponent<UIDocument>();

    private void OnEnable()
    {
        InitializeUI();
        AuthManager auth = AuthManager.Instance;
        if (auth != null)
        {
            auth.OnUserLoggedIn += HandleLogin;
            auth.OnUserLoggedOut += HandleLogout;
            currentUser = auth.CurrentUser;
            if (currentUser != null && currentUser.IsEmailVerified) LoadProfile();
            else SetStatus("Inicia sesión con un correo verificado para editar tu perfil.");
        }
        else SetStatus("No se encontró AuthManager.");
    }

    private void OnDisable()
    {
        AuthManager auth = AuthManager.Instance;
        if (auth != null) { auth.OnUserLoggedIn -= HandleLogin; auth.OnUserLoggedOut -= HandleLogout; }
        UnbindEvents();
        avatarSurface?.RemoveFromHierarchy();
        avatarSurface = null;
    }

    private void InitializeUI()
    {
        if (document == null) document = GetComponent<UIDocument>();
        if (document == null) return;
        root = document.rootVisualElement;
        if (root == null) return;
        avatarContainer = root.Q<VisualElement>("profile-avatar-drawing");
        nicknameField = root.Q<TextField>("profile-nickname-field");
        pronounsField = root.Q<TextField>("profile-pronouns-field");
        emailLabel = root.Q<Label>("profile-email-label");
        statusLabel = root.Q<Label>("profile-status-label");
        saveButton = root.Q<Button>("profile-save-button");
        clearDrawingButton = root.Q<Button>("profile-clear-drawing-button");
        if (avatarContainer != null && avatarSurface == null)
        {
            avatarSurface = new DrawingSurface { ReadOnly = true, UnitsWidth = drawingUnitsWidth, FitToContent = true, FitPadding = drawingPadding };
            avatarSurface.style.position = Position.Absolute;
            avatarSurface.style.left = 0; avatarSurface.style.top = 0;
            avatarSurface.style.right = 0; avatarSurface.style.bottom = 0;
            avatarContainer.Add(avatarSurface);
            avatarSurface.SetDrawing(null);
        }
        BindEvents();
    }

    private void BindEvents()
    {
        if (eventsBound) return;
        if (saveButton != null) saveButton.clicked += SaveProfile;
        if (clearDrawingButton != null) clearDrawingButton.clicked += ClearDrawing;
        eventsBound = true;
    }
    private void UnbindEvents()
    {
        if (!eventsBound) return;
        if (saveButton != null) saveButton.clicked -= SaveProfile;
        if (clearDrawingButton != null) clearDrawingButton.clicked -= ClearDrawing;
        eventsBound = false;
    }

    private void HandleLogin(FirebaseUser user) { currentUser = user; LoadProfile(); }
    private void HandleLogout()
    {
        currentUser = null;
        if (emailLabel != null) emailLabel.text = string.Empty;
        if (nicknameField != null) nicknameField.value = string.Empty;
        if (pronounsField != null) pronounsField.value = string.Empty;
        avatarSurface?.SetDrawing(null);
        SetStatus("Sesión cerrada.");
    }

    public void RefreshProfile() { currentUser = AuthManager.Instance != null ? AuthManager.Instance.CurrentUser : null; LoadProfile(); }

    private void LoadProfile()
    {
        if (currentUser == null || !currentUser.IsEmailVerified) { SetStatus("Inicia sesión con un correo verificado para editar tu perfil."); return; }
        if (UserProfileService.Instance == null) { SetStatus("No se encontró UserProfileService en la escena."); return; }
        SetStatus("Cargando perfil…");
        UserProfileService.Instance.EnsureProfileExists(currentUser, ok =>
        {
            if (!ok) { SetStatus("No se pudo cargar el perfil."); return; }
            UserProfileService.Instance.LoadProfile(currentUser, profile =>
            {
                if (profile == null) { SetStatus("No se pudo cargar el perfil."); return; }
                if (emailLabel != null) emailLabel.text = profile.email ?? string.Empty;
                if (nicknameField != null) nicknameField.SetValueWithoutNotify(profile.nickname ?? string.Empty);
                if (pronounsField != null) pronounsField.SetValueWithoutNotify(profile.pronouns ?? string.Empty);
                LoadAvatar(profile.drawingJson);
                SetStatus(string.Empty);
            });
        });
    }

    private void SaveProfile()
    {
        if (currentUser == null || !currentUser.IsEmailVerified || UserProfileService.Instance == null) { SetStatus("No hay una sesión válida para guardar."); return; }
        SetStatus("Guardando…");
        UserProfileService.Instance.SaveProfile(currentUser, nicknameField != null ? nicknameField.value : string.Empty, pronounsField != null ? pronounsField.value : string.Empty, ok => SetStatus(ok ? "Perfil guardado." : "No se pudo guardar el perfil."));
    }

    /// <summary>El editor de dibujo externo puede entregar aquí su JSON para guardarlo como dibujo de perfil.</summary>
    public void SaveProfileDrawingJson(string drawingJson)
    {
        if (currentUser == null || !currentUser.IsEmailVerified || UserProfileService.Instance == null) { SetStatus("No hay una sesión válida para guardar el dibujo."); return; }
        UserProfileService.Instance.SaveProfileDrawing(currentUser, drawingJson, ok =>
        {
            if (ok) { LoadAvatar(drawingJson); SetStatus("Dibujo de perfil guardado."); }
            else SetStatus("No se pudo guardar el dibujo de perfil.");
        });
    }

    private void ClearDrawing() => SaveProfileDrawingJson(string.Empty);
    private void LoadAvatar(string json)
    {
        if (avatarSurface == null) InitializeUI();
        if (avatarSurface == null) return;
        DrawingData data = null;
        if (!string.IsNullOrWhiteSpace(json))
        {
            try { data = DrawingSerializer.Deserialize(json); }
            catch (System.Exception e) { Debug.LogWarning("Dibujo de perfil no válido: " + e.Message); }
        }
        avatarSurface.SetDrawing(data);
    }
    private void SetStatus(string message) { if (statusLabel != null) statusLabel.text = message ?? string.Empty; }
}
