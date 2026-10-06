using SheepyCreations.UIControl.Runtime;
using UnityEngine;
using UnityEngine.UIElements;

public class TextCanvasEntry : BaseCanvasEntry
{
    [SerializeField] private UIDocumentPanel panel; // Was UIPanel -- needs RootVisualElement for the click.
    [SerializeField] private LabelTextBridge usernameLabel;
    [SerializeField] private LabelTextBridge contentLabel;

    private bool clickRegistered;

    public override void Populate(PostItData data)
    {
        base.Populate(data);
        if (usernameLabel != null) usernameLabel.SetText(data.authorId);
        if (contentLabel != null) contentLabel.SetText(data.contenido);
        if (panel != null) panel.Show();

        RegisterClickOnce();
    }

    // The document root may not exist yet on the first Populate call, so this keeps retrying
    // (cheap: it only runs until it succeeds once) instead of assuming Awake-time readiness.
    private void RegisterClickOnce()
    {
        if (clickRegistered || panel == null || panel.RootVisualElement == null) return;
        panel.RootVisualElement.RegisterCallback<ClickEvent>(_ => RaiseClicked());
        clickRegistered = true;
    }
}