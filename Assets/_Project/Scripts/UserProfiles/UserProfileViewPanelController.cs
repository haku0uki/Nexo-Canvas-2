
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Visualiza cualquier perfil por UID. No ofrece edición de datos.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class UserProfileViewPanelController : MonoBehaviour
{
    [Header("Dibujo")]
    [SerializeField] private float drawingUnitsWidth = 10f;
    [SerializeField] private float drawingPadding = 8f;

    private UIDocument document;
    private VisualElement root;
    private VisualElement avatarContainer;
    private VisualElement postsContainer;

    private Label nicknameLabel;
    private Label pronounsLabel;
    private Label emailLabel;
    private Label statusLabel;

    private DrawingSurface avatarSurface;
    private string requestedUid;

    private void Awake()
    {
        document = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        InitializeUI();

        if (!string.IsNullOrWhiteSpace(requestedUid))
            LoadProfile(requestedUid);
    }

    private void OnDisable()
    {
        avatarSurface?.RemoveFromHierarchy();
        avatarSurface = null;
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

        avatarContainer =
            root.Q<VisualElement>("profile-avatar-drawing");

        postsContainer =
            root.Q<VisualElement>("profile-posts-list");

        nicknameLabel =
            root.Q<Label>("profile-nickname-label");

        pronounsLabel =
            root.Q<Label>("profile-pronouns-label");

        emailLabel =
            root.Q<Label>("profile-email-label");

        statusLabel =
            root.Q<Label>("profile-status-label");

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

            avatarContainer.Add(avatarSurface);
            avatarSurface.SetDrawing(null);
        }
    }

    /// <summary>
    /// Llamar con el UID de Firebase del perfil que se quiere mostrar.
    /// </summary>
    public void ShowProfile(string uid)
    {
        requestedUid = uid;
        InitializeUI();
        LoadProfile(uid);
    }

    public void RefreshProfile()
    {
        LoadProfile(requestedUid);
    }

    private void LoadProfile(string uid)
    {
        if (string.IsNullOrWhiteSpace(uid))
        {
            SetStatus("No se indicó el UID del perfil.");
            ClearProfile();
            return;
        }

        if (UserProfileService.Instance == null)
        {
            SetStatus(
                "No se encontró UserProfileService en la escena."
            );

            Debug.LogError(
                "UserProfileViewPanelController: falta un GameObject "
                + "con UserProfileService."
            );

            return;
        }

        SetStatus("Cargando perfil…");
        ClearPosts();

        UserProfileService.Instance.LoadProfileByUid(
            uid,
            profile =>
            {
                if (profile == null)
                {
                    ClearProfile();
                    SetStatus(
                        "No se encontró el perfil o no se pudo cargar."
                    );
                    return;
                }

                if (nicknameLabel != null)
                {
                    nicknameLabel.text =
                        string.IsNullOrWhiteSpace(profile.nickname)
                            ? "Usuario"
                            : profile.nickname;
                }

                if (pronounsLabel != null)
                    pronounsLabel.text = profile.pronouns ?? string.Empty;

                if (emailLabel != null)
                    emailLabel.text = profile.email ?? string.Empty;

                LoadAvatar(profile.drawingJson);
                SetStatus(string.Empty);

                UserProfileService.Instance.LoadUserPosts(
                    profile.email,
                    RenderPosts
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

        DrawingData drawing = null;

        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                drawing = DrawingSerializer.Deserialize(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning(
                    "Dibujo de perfil no válido: " + e.Message
                );
            }
        }

        avatarSurface.SetDrawing(drawing);
    }

    private void RenderPosts(List<PostItData> posts)
    {
        if (postsContainer == null)
            return;

        postsContainer.Clear();

        if (posts == null || posts.Count == 0)
        {
            Label empty = new Label(
                "Todavía no hay publicaciones."
            );

            empty.AddToClassList("profile-empty-posts");
            postsContainer.Add(empty);
            return;
        }

        foreach (PostItData post in posts)
        {
            VisualElement card = new VisualElement();
            card.AddToClassList("profile-post-card");

            bool isDrawing = string.Equals(
                post.tipo,
                "dibujo",
                StringComparison.OrdinalIgnoreCase
            );

            Label type = new Label(isDrawing ? "Dibujo" : "Texto");
            type.AddToClassList("profile-post-type");
            card.Add(type);

            if (isDrawing)
            {
                VisualElement box = new VisualElement();
                box.AddToClassList("profile-post-drawing");
                box.style.position = Position.Relative;
                box.style.height = 180;

                card.Add(box);

                DrawingSurface surface = new DrawingSurface
                {
                    ReadOnly = true,
                    UnitsWidth = drawingUnitsWidth,
                    FitToContent = true,
                    FitPadding = drawingPadding
                };

                surface.style.position = Position.Absolute;
                surface.style.left = 0;
                surface.style.top = 0;
                surface.style.right = 0;
                surface.style.bottom = 0;

                box.Add(surface);

                try
                {
                    surface.SetDrawing(
                        DrawingSerializer.Deserialize(post.contenido)
                    );
                }
                catch (Exception e)
                {
                    Debug.LogWarning(
                        "Dibujo de publicación no válido: " + e.Message
                    );
                }
            }
            else
            {
                Label content = new Label(
                    post.contenido ?? string.Empty
                );

                content.style.whiteSpace = WhiteSpace.Normal;
                content.AddToClassList("profile-post-content");
                card.Add(content);
            }

            postsContainer.Add(card);
        }
    }

    private void ClearProfile()
    {
        if (nicknameLabel != null)
            nicknameLabel.text = string.Empty;

        if (pronounsLabel != null)
            pronounsLabel.text = string.Empty;

        if (emailLabel != null)
            emailLabel.text = string.Empty;

        avatarSurface?.SetDrawing(null);
        ClearPosts();
    }

    private void ClearPosts()
    {
        postsContainer?.Clear();
    }

    private void SetStatus(string message)
    {
        if (statusLabel != null)
            statusLabel.text = message ?? string.Empty;
    }
}
