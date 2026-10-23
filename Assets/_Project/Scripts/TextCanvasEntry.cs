using SheepyCreations.UIControl.Runtime;
using UnityEngine;
using UnityEngine.UIElements;

public class TextCanvasEntry : BaseCanvasEntry
{
    [SerializeField] private UIDocumentPanel panel;
    [SerializeField] private LabelTextBridge usernameLabel;
    [SerializeField] private LabelTextBridge contentLabel;
    [Tooltip("Nombre de la etiqueta del autor, para poner el dibujo de perfil a su lado.")]
    [SerializeField] private string authorLabelName = "username-label-postit";

    private VisualElement clickRoot;
    private bool pendingPresentation;

    public override void Populate(PostItData data)
    {
        base.Populate(data);
        pendingPresentation = true;
        TryPresentData();
    }

    private void Update()
    {
        if (pendingPresentation) TryPresentData();
    }

    private void TryPresentData()
    {
        if (Data == null || panel == null || !panel.IsInitialized) return;
        VisualElement root = panel.RootVisualElement;
        if (root == null || root.panel == null) return;
        if (usernameLabel != null && !usernameLabel.IsBound) return;
        if (contentLabel != null && !contentLabel.IsBound) return;

        if (usernameLabel != null)
        {
            ProfilePanelUtility.ShowAuthorName(Data.authorId, usernameLabel.SetText);
            ProfilePanelUtility.ShowAuthorAvatar(Data.authorId, root.Q<Label>(authorLabelName));
            ProfilePanelUtility.BindAuthorButton(root, Data.authorId, ProfilePanelUtility.OpenProfileFromBoard);
        }
        if (contentLabel != null) contentLabel.SetText(Data.contenido);
        panel.Show();

        if (clickRoot != root)
        {
            if (clickRoot != null) clickRoot.UnregisterCallback<ClickEvent>(OnEntryClick);
            clickRoot = root;
            clickRoot.RegisterCallback<ClickEvent>(OnEntryClick);
        }

        pendingPresentation = false;
    }

    private void OnEntryClick(ClickEvent evt)
    {
        RaiseClicked();
    }

    private void OnDestroy()
    {
        if (clickRoot != null) clickRoot.UnregisterCallback<ClickEvent>(OnEntryClick);
    }
}