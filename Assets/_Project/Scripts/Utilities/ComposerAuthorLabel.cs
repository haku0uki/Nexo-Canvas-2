using SheepyCreations.UIControl.Runtime;
using UnityEngine;

// Muestra en los paneles de creacion (TextEntryComposer / DrawEntryComposer) quien esta
// publicando: el nickname del usuario con la sesion iniciada. Sin sesion muestra signedOutText.
// Va en el mismo objeto que el UIDocumentPanel del composer, junto a un LabelTextBridge
// cuyo Target Element Name es "username-label-postit".
public class ComposerAuthorLabel : MonoBehaviour
{
    [Tooltip("Si queda vacio se usa el UIDocumentPanel de este objeto.")]
    [SerializeField] private UIDocumentPanel panel;
    [Tooltip("Bridge de 'username-label-postit'. Si queda vacio se usa el primer LabelTextBridge de este objeto.")]
    [SerializeField] private LabelTextBridge authorLabel;
    [Tooltip("Texto antes del nombre, por ejemplo 'Publicando como '. Vacio = solo el nombre.")]
    [SerializeField] private string prefix = string.Empty;
    [SerializeField] private string signedOutText = "Sin sesion";

    private bool applied;
    private string appliedEmail;

    private void Awake()
    {
        if (panel == null) panel = GetComponent<UIDocumentPanel>();
        if (authorLabel == null) authorLabel = GetComponent<LabelTextBridge>();
        if (authorLabel == null) authorLabel = GetComponentInChildren<LabelTextBridge>(true);
    }

    // Se vuelve a aplicar cada vez que el panel se muestra y si cambia el usuario conectado.
    private void Update()
    {
        if (panel == null || authorLabel == null) return;

        if (!panel.IsVisible)
        {
            applied = false;
            return;
        }

        if (!panel.IsInitialized || !authorLabel.IsBound) return;

        string email = CurrentEmail();
        if (applied && email == appliedEmail) return;

        applied = true;
        appliedEmail = email;
        Apply(email);
    }

    private static string CurrentEmail()
    {
        return AuthManager.Instance != null && AuthManager.Instance.CurrentUser != null
            ? AuthManager.Instance.CurrentUser.Email
            : null;
    }

    private void Apply(string email)
    {
        if (string.IsNullOrEmpty(email))
        {
            authorLabel.SetText(signedOutText);
            return;
        }

        ProfilePanelUtility.ShowAuthorName(
            email,
            name => authorLabel.SetText(prefix + name),
            () => appliedEmail == email);
    }
}
