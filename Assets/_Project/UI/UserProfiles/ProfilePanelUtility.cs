using System;
using SheepyCreations.UIControl.Runtime;
using UnityEngine;
using UnityEngine.UIElements;

// Utilidades compartidas por los paneles de perfil.
// Mismo criterio que PostItCreationFlow: la visibilidad pasa por el controller
// del panel cuando existe, para que sus reglas y eventos se mantengan.
public static class ProfilePanelUtility
{
    public static void ShowFlowPanel(UIPanel panel, Action onComplete = null)
    {
        if (panel == null) return;

        if (panel.HasController)
            panel.Controller.ShowPanel(panel, onComplete: onComplete);
        else
            panel.Show(onComplete);
    }

    public static void HideFlowPanel(UIPanel panel, Action onComplete = null)
    {
        if (panel == null) return;

        if (panel.HasController)
            panel.Controller.HidePanel(panel, fadeDurationSeconds: 0f, onComplete: onComplete);
        else
            panel.Hide(0f, onComplete);
    }

    // El panel y su UXML ya estan listos para recibir datos.
    public static bool IsPanelReady(UIDocumentPanel panel)
    {
        if (panel == null || !panel.IsInitialized) return false;

        VisualElement root = panel.RootVisualElement;
        return root != null && root.panel != null;
    }

    // Un bridge asignado que todavia no resolvio su elemento.
    public static bool IsUnbound(LabelTextBridge bridge)
    {
        return bridge != null && !bridge.IsBound;
    }

    // Devuelve null si el JSON esta vacio o no es valido.
    public static DrawingData ParseDrawing(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            return DrawingSerializer.Deserialize(json);
        }
        catch (Exception e)
        {
            Debug.LogWarning("ProfilePanelUtility: dibujo no valido: " + e.Message);
            return null;
        }
    }

    // Superficie de solo lectura que llena a su contenedor.
    public static DrawingSurface CreateReadOnlySurface(float unitsWidth, float padding)
    {
        DrawingSurface surface = new DrawingSurface
        {
            ReadOnly = true,
            UnitsWidth = unitsWidth,
            FitToContent = true,
            FitPadding = padding
        };

        // Solo se mira: el toque debe llegar al boton del autor que esta encima.
        surface.pickingMode = PickingMode.Ignore;

        surface.style.position = Position.Absolute;
        surface.style.left = 0;
        surface.style.top = 0;
        surface.style.right = 0;
        surface.style.bottom = 0;

        return surface;
    }

    // Muestra el nickname del autor en lugar de su correo. Primero pone el nombre en cache
    // (o el de respaldo) y, si hace falta, lo reemplaza cuando Firestore responde.
    // isStillCurrent evita escribir en un elemento que ya muestra otra cosa.
    public static void ShowAuthorName(string email, Action<string> setText, Func<bool> isStillCurrent = null)
    {
        if (setText == null) return;

        UserProfileService service = UserProfileService.Instance;

        if (service == null)
        {
            setText(UserProfileService.FallbackName(email));
            return;
        }

        if (service.TryGetDisplayName(email, out string cached))
        {
            setText(cached);
            return;
        }

        setText(UserProfileService.FallbackName(email));

        service.ResolveDisplayName(email, name =>
        {
            if (isStillCurrent == null || isStillCurrent())
                setText(name);
        });
    }

    public const string AuthorButtonName = "autor-user-profile";

    private sealed class AuthorButtonState
    {
        public string email;
        public Action<string> open;
    }

    // Hace tocable el boton del autor (autor-user-profile) dentro de root: abre el perfil de ese
    // correo. Se puede llamar en cada Populate; el callback se registra una sola vez y lee el
    // correo vigente. StopPropagation evita que el toque tambien abra el detalle del post-it.
    public static void BindAuthorButton(VisualElement root, string email, Action<string> open)
    {
        if (root == null) return;

        VisualElement button = root.Q<VisualElement>(AuthorButtonName);
        if (button == null) return;

        if (button.userData is AuthorButtonState state)
        {
            state.email = email;
            state.open = open;
            return;
        }

        state = new AuthorButtonState { email = email, open = open };
        button.userData = state;

        button.RegisterCallback<ClickEvent>(evt =>
        {
            evt.StopPropagation();

            AuthorButtonState current = (AuthorButtonState)button.userData;
            if (!string.IsNullOrEmpty(current.email)) current.open?.Invoke(current.email);
        });
    }

    // Para los post-its del tablero: busca el panel de perfil en la escena.
    public static void OpenProfileFromBoard(string email)
    {
        UserProfileViewPanelController view =
            UnityEngine.Object.FindFirstObjectByType<UserProfileViewPanelController>(UnityEngine.FindObjectsInactive.Include);

        if (view != null) view.ShowProfileByEmail(email);
        else UnityEngine.Debug.LogWarning("ProfilePanelUtility: no hay UserProfileViewPanelController en la escena.");
    }

    public const string AuthorAvatarName = "author-avatar";

    // Pone el dibujo de perfil del autor a la izquierda de su nombre. Si el UXML ya trae un
    // elemento llamado "author-avatar" junto a la etiqueta, lo usa; si no, envuelve la etiqueta
    // en una fila [avatar][nombre]. Es idempotente: llamarlo otra vez solo repinta.
    public static void ShowAuthorAvatar(string email, VisualElement nameLabel, float size = 64f,
        float unitsWidth = 10f, float padding = 8f, Func<bool> isStillCurrent = null)
    {
        if (nameLabel == null || nameLabel.parent == null) return;

        VisualElement avatar = nameLabel.parent.Q<VisualElement>(AuthorAvatarName);

        if (avatar == null)
        {
            // El tamano sigue al de la letra del nombre (el proyecto usa fuentes grandes).
            float fontSize = nameLabel.resolvedStyle.fontSize;
            if (fontSize > 0f) size = fontSize * 1.4f;

            VisualElement parent = nameLabel.parent;
            int index = parent.IndexOf(nameLabel);

            VisualElement row = new VisualElement();
            row.pickingMode = PickingMode.Ignore;
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.flexGrow = nameLabel.resolvedStyle.flexGrow;

            avatar = new VisualElement { name = AuthorAvatarName };
            avatar.AddToClassList("author-avatar");
            avatar.pickingMode = PickingMode.Ignore;
            avatar.style.width = size;
            avatar.style.height = size;
            avatar.style.minWidth = size;
            avatar.style.marginRight = 6;

            parent.Insert(index, row);
            row.Add(avatar);
            row.Add(nameLabel);
        }

        UserProfileService service = UserProfileService.Instance;
        if (service == null) return;

        VisualElement target = avatar;

        void Paint(string json)
        {
            if (isStillCurrent != null && !isStillCurrent()) return;
            if (target.panel == null && target.parent == null) return;

            target.Clear();
            if (string.IsNullOrEmpty(json)) return;

            DrawingSurface surface = CreateReadOnlySurface(unitsWidth, padding);
            target.Add(surface);
            surface.SetDrawing(ParseDrawing(json));
        }

        if (service.TryGetAvatarDrawing(email, out string cached))
            Paint(cached);
        else
            service.ResolveAvatarDrawing(email, Paint);
    }

    // Quien este conectado, solo si ya verifico su correo.
    public static bool TryGetSessionUser(out Firebase.Auth.FirebaseUser user)
    {
        user = AuthManager.Instance != null ? AuthManager.Instance.CurrentUser : null;
        return user != null && user.IsEmailVerified;
    }
}