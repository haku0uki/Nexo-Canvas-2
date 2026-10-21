using System;
using System.Collections.Generic;
using SheepyCreations.UIControl.Runtime;
using UnityEngine;
using UnityEngine.UIElements;

// Muestra el perfil de cualquier usuario (por UID) con sus publicaciones.
// No edita datos: el boton de editar solo abre UserProfileEditPanelController
// y aparece unicamente cuando el perfil mostrado es el del usuario conectado.
//
// Se usa igual que el resto del proyecto: los bridges se asignan en el Inspector,
// nada se busca con root.Q, y los metodos publicos se enganchan a un ButtonBridge.
public class UserProfileViewPanelController : MonoBehaviour
{
    [Header("PANEL")]
    [Tooltip("Si queda vacio se usa el UIDocumentPanel de este GameObject.")]
    [SerializeField] private UIDocumentPanel panel;
    [Tooltip("Opcional. Panel que vuelve a mostrarse al cerrar el perfil (por ejemplo el HUD).")]
    [SerializeField] private UIDocumentPanel returnPanel;
    [Tooltip("Opcional. Ventana de edicion; se abre con el boton de editar.")]
    [SerializeField] private UserProfileEditPanelController editPanel;

    [Header("TEXTOS (LabelTextBridge)")]
    [Tooltip("Elemento: profile-nickname-label")]
    [SerializeField] private LabelTextBridge nicknameLabel;
    [Tooltip("Elemento: profile-pronouns-label")]
    [SerializeField] private LabelTextBridge pronounsLabel;
    [Tooltip("Elemento: profile-email-label")]
    [SerializeField] private LabelTextBridge emailLabel;
    [Tooltip("Elemento: profile-status-label")]
    [SerializeField] private LabelTextBridge statusLabel;

    [Header("BOTONES (ButtonBridge)")]
    [Tooltip("Elemento: profile-editor")]
    [SerializeField] private ButtonBridge editButton;
    [Tooltip("Elemento: back-button")]
    [SerializeField] private ButtonBridge backButton;

    [Header("CONTENEDORES (por nombre)")]
    [SerializeField] private string avatarContainerName = "profile-avatar-drawing";
    [SerializeField] private string postsListName = "profile-posts-list";
    [SerializeField] private string emptyStateName = "profile-empty-state";
    [SerializeField] private string editButtonName = "profile-editor";

    [Header("POST-ITS DE LA LISTA")]
    [Tooltip("UXML del post-it de texto (TextEntry.uxml).")]
    [SerializeField] private VisualTreeAsset textPostItAsset;
    [Tooltip("UXML del post-it de dibujo (DrawEntry.uxml, el que tiene el contenedor drawing-view).")]
    [SerializeField] private VisualTreeAsset drawingPostItAsset;
    [SerializeField] private string authorLabelName = "username-label-postit";
    [SerializeField] private string contentLabelName = "content-label-postit";
    [SerializeField] private string drawingViewName = "drawing-view";

    [Header("DIBUJO")]
    [SerializeField] private float drawingUnitsWidth = 10f;
    [SerializeField] private float drawingPadding = 8f;

    // Estado que se muestra. Se vuelca a la UI cuando el panel y los bridges estan listos.
    private string currentUid;
    private string currentEmail;
    private Action returnAction;
    private string nickname = string.Empty;
    private string pronouns = string.Empty;
    private string email = string.Empty;
    private string status = string.Empty;
    private string drawingJson = string.Empty;
    private List<PostItData> posts;
    private bool postsLoaded;
    private bool needsRender;
    private int loadToken;

    private DrawingSurface avatarSurface;


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
        //if (editButton != null) editButton.Clicked += OpenEditor;
        //if (backButton != null) backButton.Clicked += Close;
        //if (editPanel != null) editPanel.Saved += RefreshProfile;
    }

    private void OnDisable()
    {
        //if (editButton != null) editButton.Clicked -= OpenEditor;
        //if (backButton != null) backButton.Clicked -= Close;
        //if (editPanel != null) editPanel.Saved -= RefreshProfile;

        avatarSurface?.RemoveFromHierarchy();
        avatarSurface = null;
    }

    private void Update()
    {
        if (needsRender)
            TryRender();
    }


    // ============================================================
    // API PUBLICA
    // ============================================================

    /// <summary>Abre el perfil del usuario conectado. Sin parametros: se puede enganchar a ButtonBridge.onClick.</summary>
    public void ShowOwnProfile()
    {
        if (!ProfilePanelUtility.TryGetSessionUser(out Firebase.Auth.FirebaseUser user))
        {
            Debug.LogWarning("UserProfileViewPanelController: hay que iniciar sesion para ver tu perfil.");
            return;
        }

        ShowProfile(user.UserId);
    }

    /// <summary>Abre el perfil del usuario con ese UID de Firebase.</summary>
    public void ShowProfile(string uid)
    {
        if (string.IsNullOrWhiteSpace(uid))
        {
            Debug.LogWarning("UserProfileViewPanelController: no se indico el UID del perfil.");
            return;
        }

        BeginShow(uid, null, null);
    }

    /// <summary>
    /// Abre el perfil del usuario con ese correo. Es lo que se conoce de un post-it,
    /// porque authorId guarda el correo y no el UID.
    /// </summary>
    /// <param name="onReturn">Opcional. Si se indica, al cerrar el perfil se ejecuta esto en lugar de mostrar Return Panel (por ejemplo, reabrir el detalle de un post-it).</param>
    public void ShowProfileByEmail(string email, Action onReturn = null)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            Debug.LogWarning("UserProfileViewPanelController: no se indico el correo del perfil.");
            return;
        }

        BeginShow(null, email, onReturn);
    }

    private void BeginShow(string uid, string email, Action onReturn)
    {
        currentUid = uid;
        currentEmail = email;
        returnAction = onReturn;
        ClearState();
        SetStatus("Cargando perfil…");

        if ((returnPanel != null || returnAction != null) && panel != null && panel.HasController)
            panel.ShowIsolatedViaController();
        else
            ProfilePanelUtility.ShowFlowPanel(panel);

        LoadProfile();
    }

    /// <summary>Vuelve a pedir los datos del perfil que esta abierto.</summary>
    public void RefreshProfile()
    {
        if (string.IsNullOrWhiteSpace(currentUid) && string.IsNullOrWhiteSpace(currentEmail)) return;
        LoadProfile();
    }

    /// <summary>Cierra el perfil y devuelve el panel de retorno, si hay uno.</summary>
    public void Close()
    {
        loadToken++; // Ignora respuestas que lleguen despues de cerrar.

        if (editPanel != null) editPanel.Close();

        // Quien abrio el perfil decide a donde se vuelve; si no, al Return Panel.
        Action onReturn = returnAction;
        returnAction = null;

        if (onReturn != null)
        {
            ProfilePanelUtility.HideFlowPanel(panel);
            onReturn();
        }
        else if (returnPanel != null)
            returnPanel.ShowIsolatedViaController();
        else
            ProfilePanelUtility.HideFlowPanel(panel);
    }


    // ============================================================
    // CARGA
    // ============================================================

    private void LoadProfile()
    {
        if (UserProfileService.Instance == null)
        {
            SetStatus("No se encontro UserProfileService en la escena.");
            Debug.LogError("UserProfileViewPanelController: falta un GameObject con UserProfileService.");
            return;
        }

        int token = ++loadToken;
        postsLoaded = false;
        posts = null;
        MarkDirty();

        void OnProfileLoaded(UserProfileService.UserProfileData profile)
        {
            if (token != loadToken) return;

            if (profile == null && !string.IsNullOrWhiteSpace(currentEmail) && string.IsNullOrWhiteSpace(currentUid))
            {
                // Quien publico todavia no tiene documento de perfil: se muestran
                // su correo y sus publicaciones.
                profile = new UserProfileService.UserProfileData { email = currentEmail };
            }

            if (profile == null)
            {
                ClearState();
                SetStatus("No se encontro el perfil o no se pudo cargar.");
                return;
            }

            currentUid = string.IsNullOrWhiteSpace(profile.uid) ? currentUid : profile.uid;
            currentEmail = string.IsNullOrWhiteSpace(profile.email) ? currentEmail : profile.email;

            nickname = string.IsNullOrWhiteSpace(profile.nickname) ? "Usuario" : profile.nickname;
            pronouns = profile.pronouns ?? string.Empty;
            email = profile.email ?? string.Empty;
            drawingJson = profile.drawingJson ?? string.Empty;
            SetStatus(string.Empty);

            UserProfileService.Instance.LoadUserPosts(profile.email, list =>
            {
                if (token != loadToken) return;

                posts = list ?? new List<PostItData>();
                postsLoaded = true;
                MarkDirty();
            });
        }

        if (!string.IsNullOrWhiteSpace(currentUid))
            UserProfileService.Instance.LoadProfileByUid(currentUid, OnProfileLoaded);
        else
            UserProfileService.Instance.LoadProfileByEmail(currentEmail, OnProfileLoaded);
    }

    //private void OpenEditor()
    //{
    //    if (editPanel != null)
    //        editPanel.Open();
    //}


    // ============================================================
    // ESTADO Y RENDER
    // ============================================================

    private void ClearState()
    {
        nickname = string.Empty;
        pronouns = string.Empty;
        email = string.Empty;
        drawingJson = string.Empty;
        posts = null;
        postsLoaded = false;
        MarkDirty();
    }

    private void SetStatus(string message)
    {
        status = message ?? string.Empty;
        MarkDirty();
    }

    private void MarkDirty()
    {
        needsRender = true;
        TryRender();
    }

    private bool IsReady()
    {
        return ProfilePanelUtility.IsPanelReady(panel)
            && !ProfilePanelUtility.IsUnbound(nicknameLabel)
            && !ProfilePanelUtility.IsUnbound(pronounsLabel)
            && !ProfilePanelUtility.IsUnbound(emailLabel)
            && !ProfilePanelUtility.IsUnbound(statusLabel);
    }

    private void TryRender()
    {
        if (!IsReady()) return;

        if (nicknameLabel != null) nicknameLabel.SetText(nickname);
        if (pronounsLabel != null) pronounsLabel.SetText(pronouns);
        if (emailLabel != null) emailLabel.SetText(email);
        if (statusLabel != null) statusLabel.SetText(status);

        RenderEditButton();
        RenderAvatar();
        RenderPosts();

        needsRender = false;
    }

    // Solo el dueño del perfil ve el boton de editar.
    private void RenderEditButton()
    {
        if (!panel.TryGetElement(editButtonName, out VisualElement element)) return;

        Firebase.Auth.FirebaseUser me = AuthManager.Instance != null ? AuthManager.Instance.CurrentUser : null;

        bool isOwn = me != null && (
            (!string.IsNullOrEmpty(currentUid) && me.UserId == currentUid) ||
            string.Equals(me.Email, currentEmail, System.StringComparison.OrdinalIgnoreCase));

        element.style.display = isOwn ? DisplayStyle.Flex : DisplayStyle.None;
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

        avatarSurface.SetDrawing(ProfilePanelUtility.ParseDrawing(drawingJson));
    }

    private void RenderPosts()
    {
        if (!panel.TryGetElement(postsListName, out VisualElement list)) return;

        list.Clear();

        if (panel.TryGetElement(emptyStateName, out VisualElement empty))
        {
            bool showEmpty = postsLoaded && (posts == null || posts.Count == 0);
            empty.style.display = showEmpty ? DisplayStyle.Flex : DisplayStyle.None;
        }

        if (!postsLoaded || posts == null) return;

        foreach (PostItData post in posts)
            list.Add(BuildPostIt(post));
    }

    // Cada publicacion usa el mismo UXML que el tablero (TextEntry / DrawEntry). El tamano y la
    // grilla los define el USS (clase profile-postit), no este script.
    private VisualElement BuildPostIt(PostItData post)
    {
        bool isDrawing = string.Equals(post.tipo, "dibujo", System.StringComparison.OrdinalIgnoreCase);
        VisualTreeAsset asset = isDrawing ? drawingPostItAsset : textPostItAsset;

        VisualElement card = new VisualElement();
        card.AddToClassList("profile-postit");

        if (asset == null)
        {
            // Sin plantilla asignada: tarjeta simple para no perder la publicacion.
            Label plain = new Label(isDrawing ? "Dibujo" : post.contenido ?? string.Empty);
            plain.style.whiteSpace = WhiteSpace.Normal;
            card.Add(plain);
            return card;
        }

        TemplateContainer instance = asset.Instantiate();
        instance.style.flexGrow = 1;

        foreach (VisualElement child in instance.Children())
            child.style.flexGrow = 1;

        card.Add(instance);

        // El autor vive dentro de un template anidado (autor-user-profile). Si no se encuentra
        // por nombre, se usa la primera etiqueta que no sea la del contenido.
        Label author = instance.Q<Label>(authorLabelName);

        if (author == null)
        {
            foreach (Label candidate in instance.Query<Label>().ToList())
            {
                if (candidate.name == contentLabelName) continue;

                author = candidate;
                break;
            }
        }

        if (author != null)
            author.text = nickname;
        else
            Debug.LogWarning("UserProfileViewPanelController: no se encontro la etiqueta del autor en el post-it. " +
                             "Revisa el campo Author Label Name y que el template autor-user-profile exista en el build.");

        if (isDrawing)
        {
            VisualElement view = instance.Q<VisualElement>(drawingViewName);

            if (view != null)
            {
                DrawingSurface surface = ProfilePanelUtility.CreateReadOnlySurface(drawingUnitsWidth, drawingPadding);
                view.Add(surface);
                surface.SetDrawing(ProfilePanelUtility.ParseDrawing(post.contenido));
            }
        }
        else
        {
            Label content = instance.Q<Label>(contentLabelName);
            if (content != null) content.text = post.contenido ?? string.Empty;
        }

        return card;
    }
}