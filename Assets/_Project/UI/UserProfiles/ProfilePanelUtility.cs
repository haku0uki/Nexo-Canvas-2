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

    // Quien este conectado, solo si ya verifico su correo.
    public static bool TryGetSessionUser(out Firebase.Auth.FirebaseUser user)
    {
        user = AuthManager.Instance != null ? AuthManager.Instance.CurrentUser : null;
        return user != null && user.IsEmailVerified;
    }
}