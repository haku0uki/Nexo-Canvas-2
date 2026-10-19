
using System;
using Firebase.Auth;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Ventana modal para editar el perfil del usuario autenticado.
/// El botón Cambiar dibujo solicita abrir un editor de dibujo externo mediante un evento.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class UserProfileEditPanelController : MonoBehaviour
{
    [Header("Vista previa del dibujo")]
    [SerializeField] private float drawingUnitsWidth = 10f;
    [SerializeField] private float drawingPadding = 8f;

    [Header("Teclado móvil")]
    [SerializeField, Range(0.2f, 0.5f)]
    private float estimatedKeyboardHeightRatio = 0.32f;

    [SerializeField] private float keyboardGap = 14f;

    public event Action EditDrawingRequested;
    public event Action CloseRequested;

    private UIDocument document;
    private VisualElement root;
    private VisualElement overlay;
    private VisualElement modalWindow;
    private VisualElement avatarContainer;

    private TextField nicknameField;
    private TextField pronounsField;
    private Label statusLabel;

    private Button saveButton;
    private Button changeDrawingButton;
    private Button closeButton;
    private Button cancelButton;

    private DrawingSurface avatarSurface;
    private FirebaseUser currentUser;

    private bool eventsBound;
    private bool inputFocused;
    private float lastRootHeight;
    private bool isVisible;

    private void Awake()
    {
        document = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        InitializeUI();

        AuthManager auth = AuthManager.Instance;

        if (auth != null)
        {
            auth.OnUserLoggedIn += HandleLogin;
            auth.OnUserLoggedOut += HandleLogout;
            currentUser = auth.CurrentUser;
        }

        SetVisible(false);
    }

    private void OnDisable()
    {
        AuthManager auth = AuthManager.Instance;

        if (auth != null)
        {
            auth.OnUserLoggedIn -= HandleLogin;
            auth.OnUserLoggedOut -= HandleLogout;
        }

        UnbindEvents();

        avatarSurface?.RemoveFromHierarchy();
        avatarSurface = null;
    }

    private void Update()
    {
        if (!isVisible || modalWindow == null || root == null)
            return;

        float rootHeight = root.layout.height;

        if (rootHeight <= 0f)
            return;

        bool keyboardVisible =
            inputFocused && TouchScreenKeyboard.visible;

        // Si Android ya reduce el área de la aplicación al abrir
        // el teclado, el layout se adapta por sí mismo.
        if (keyboardVisible &&
            lastRootHeight > rootHeight + 80f)
        {
            modalWindow.style.top = 0;
            return;
        }

        if (!keyboardVisible)
        {
            modalWindow.style.top = 0;
            lastRootHeight = rootHeight;
            return;
        }

        float keyboardHeightPixels = TouchScreenKeyboard.area.height;
        float keyboardHeight;

        if (keyboardHeightPixels > 0f && Screen.height > 0)
        {
            keyboardHeight =
                keyboardHeightPixels * (rootHeight / Screen.height);
        }
        else
        {
            // Estimación alternativa para dispositivos que no
            // informan el área del teclado virtual.
            keyboardHeight =
                rootHeight * estimatedKeyboardHeightRatio;
        }

        float modalHeight = modalWindow.layout.height;
        float centeredBottom = (rootHeight + modalHeight) * 0.5f;
        float keyboardTop = rootHeight - keyboardHeight;

        float requiredShift = Mathf.Max(
            0f,
            centeredBottom - keyboardTop + keyboardGap
        );

        float maxShift = Mathf.Max(
            0f,
            (rootHeight - modalHeight) * 0.5f - 8f
        );

        float shift = Mathf.Min(requiredShift, maxShift);

        modalWindow.style.top = -shift;
    }

    /// <summary>
    /// Muestra la ventana modal y carga los datos del usuario actual.
    /// </summary>
    public void Show()
    {
        InitializeUI();

        currentUser = AuthManager.Instance != null
            ? AuthManager.Instance.CurrentUser
            : null;

        SetVisible(true);
        LoadProfile();
    }

    /// <summary>Oculta la ventana modal.</summary>
    public void Hide()
    {
        inputFocused = false;

        if (nicknameField != null)
            nicknameField.Blur();

        if (pronounsField != null)
            pronounsField.Blur();

        if (modalWindow != null)
            modalWindow.style.top = 0;

        SetVisible(false);
    }

    /// <summary>
    /// Permite al editor de dibujo devolver el JSON actualizado.
    /// </summary>
    public void SaveProfileDrawingJson(string drawingJson)
    {
        if (currentUser == null ||
            !currentUser.IsEmailVerified ||
            UserProfileService.Instance == null)
        {
            SetStatus("No hay una sesión válida para guardar el dibujo.");
            return;
        }

        SetStatus("Guardando dibujo…");

        UserProfileService.Instance.SaveProfileDrawing(
            currentUser,
            drawingJson,
            ok =>
            {
                if (ok)
                {
                    LoadAvatar(drawingJson);
                    SetStatus("Dibujo de perfil guardado.");
                }
                else
                {
                    SetStatus("No se pudo guardar el dibujo de perfil.");
                }
            }
        );
    }

    private void InitializeUI()
    {
        if (document == null)
            document = GetComponent<UIDocument>();

        if (document == null)
            return;

        root = document.rootVisualElement;

        if (root == null)
            return;

        overlay = root.Q<VisualElement>("profile-edit-overlay");
        modalWindow = root.Q<VisualElement>("profile-edit-window");
        avatarContainer = root.Q<VisualElement>("profile-avatar-drawing");

        nicknameField =
            root.Q<TextField>("profile-nickname-field");

        pronounsField =
            root.Q<TextField>("profile-pronouns-field");

        statusLabel =
            root.Q<Label>("profile-status-label");

        saveButton =
            root.Q<Button>("profile-save-button");

        changeDrawingButton =
            root.Q<Button>("profile-change-drawing-button");

        closeButton =
            root.Q<Button>("profile-close-button");

        cancelButton =
            root.Q<Button>("profile-cancel-button");

        if (avatarContainer != null && avatarSurface == null)
        {
            avatarSurface = new DrawingSurface
            {
                ReadOnly = true,
                UnitsWidth = drawingUnitsWidth,
                FitToContent = true,
                FitPadding = drawingPadding
            };

            avatarSurface.style.position = Position.Absolute;
            avatarSurface.style.left = 0;
            avatarSurface.style.top = 0;
            avatarSurface.style.right = 0;
            avatarSurface.style.bottom = 0;

            // El botón permanece por encima del dibujo.
            avatarContainer.Insert(0, avatarSurface);
            avatarSurface.SetDrawing(null);
        }

        BindEvents();

        if (root.layout.height > 0f)
            lastRootHeight = root.layout.height;
    }

    private void BindEvents()
    {
        if (eventsBound)
            return;

        if (saveButton != null)
            saveButton.clicked += SaveProfile;

        if (changeDrawingButton != null)
            changeDrawingButton.clicked += RequestDrawingEdit;

        if (closeButton != null)
            closeButton.clicked += RequestClose;

        if (cancelButton != null)
            cancelButton.clicked += RequestClose;

        if (nicknameField != null)
        {
            nicknameField.RegisterCallback<FocusInEvent>(
                HandleInputFocusIn
            );

            nicknameField.RegisterCallback<FocusOutEvent>(
                HandleInputFocusOut
            );
        }

        if (pronounsField != null)
        {
            pronounsField.RegisterCallback<FocusInEvent>(
                HandleInputFocusIn
            );

            pronounsField.RegisterCallback<FocusOutEvent>(
                HandleInputFocusOut
            );
        }

        eventsBound = true;
    }

    private void UnbindEvents()
    {
        if (!eventsBound)
            return;

        if (saveButton != null)
            saveButton.clicked -= SaveProfile;

        if (changeDrawingButton != null)
            changeDrawingButton.clicked -= RequestDrawingEdit;

        if (closeButton != null)
            closeButton.clicked -= RequestClose;

        if (cancelButton != null)
            cancelButton.clicked -= RequestClose;

        if (nicknameField != null)
        {
            nicknameField.UnregisterCallback<FocusInEvent>(
                HandleInputFocusIn
            );

            nicknameField.UnregisterCallback<FocusOutEvent>(
                HandleInputFocusOut
            );
        }

        if (pronounsField != null)
        {
            pronounsField.UnregisterCallback<FocusInEvent>(
                HandleInputFocusIn
            );

            pronounsField.UnregisterCallback<FocusOutEvent>(
                HandleInputFocusOut
            );
        }

        eventsBound = false;
    }

    private void HandleInputFocusIn(FocusInEvent evt)
    {
        inputFocused = true;
    }

    private void HandleInputFocusOut(FocusOutEvent evt)
    {
        // Comprueba si el foco pasó al otro TextField.
        inputFocused =
            nicknameField != null &&
            nicknameField.focusController.focusedElement == nicknameField;

        inputFocused |=
            pronounsField != null &&
            pronounsField.focusController.focusedElement == pronounsField;
    }

    private void RequestDrawingEdit()
    {
        EditDrawingRequested?.Invoke();
    }

    private void RequestClose()
    {
        Hide();
        CloseRequested?.Invoke();
    }

    private void SetVisible(bool visible)
    {
        isVisible = visible;

        if (overlay != null)
        {
            overlay.style.display = visible
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        if (modalWindow != null)
            modalWindow.style.top = 0;

        if (visible && root != null && root.layout.height > 0f)
            lastRootHeight = root.layout.height;
    }

    private void HandleLogin(FirebaseUser user)
    {
        currentUser = user;

        if (isVisible)
            LoadProfile();
    }

    private void HandleLogout()
    {
        currentUser = null;

        if (nicknameField != null)
            nicknameField.value = string.Empty;

        if (pronounsField != null)
            pronounsField.value = string.Empty;

        avatarSurface?.SetDrawing(null);
        SetStatus("Sesión cerrada.");
    }

    private void LoadProfile()
    {
        if (currentUser == null || !currentUser.IsEmailVerified)
        {
            SetStatus(
                "Inicia sesión con un correo verificado para editar tu perfil."
            );
            return;
        }

        if (UserProfileService.Instance == null)
        {
            SetStatus("No se encontró UserProfileService en la escena.");
            return;
        }

        SetStatus("Cargando perfil…");

        UserProfileService.Instance.EnsureProfileExists(
            currentUser,
            ok =>
            {
                if (!ok)
                {
                    SetStatus("No se pudo cargar el perfil.");
                    return;
                }

                UserProfileService.Instance.LoadProfile(
                    currentUser,
                    profile =>
                    {
                        if (profile == null)
                        {
                            SetStatus("No se pudo cargar el perfil.");
                            return;
                        }

                        if (nicknameField != null)
                        {
                            nicknameField.SetValueWithoutNotify(
                                profile.nickname ?? string.Empty
                            );
                        }

                        if (pronounsField != null)
                        {
                            pronounsField.SetValueWithoutNotify(
                                profile.pronouns ?? string.Empty
                            );
                        }

                        LoadAvatar(profile.drawingJson);
                        SetStatus(string.Empty);
                    }
                );
            }
        );
    }

    private void SaveProfile()
    {
        if (currentUser == null ||
            !currentUser.IsEmailVerified ||
            UserProfileService.Instance == null)
        {
            SetStatus("No hay una sesión válida para guardar.");
            return;
        }

        SetStatus("Guardando…");

        UserProfileService.Instance.SaveProfile(
            currentUser,
            nicknameField != null
                ? nicknameField.value
                : string.Empty,
            pronounsField != null
                ? pronounsField.value
                : string.Empty,
            ok =>
            {
                SetStatus(
                    ok
                        ? "Perfil guardado."
                        : "No se pudo guardar el perfil."
                );
            }
        );
    }

    private void LoadAvatar(string json)
    {
        if (avatarSurface == null)
            InitializeUI();

        if (avatarSurface == null)
            return;

        DrawingData data = null;

        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                data = DrawingSerializer.Deserialize(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning(
                    "Dibujo de perfil no válido: " + e.Message
                );
            }
        }

        avatarSurface.SetDrawing(data);
    }

    private void SetStatus(string message)
    {
        if (statusLabel != null)
            statusLabel.text = message ?? string.Empty;
    }
}
