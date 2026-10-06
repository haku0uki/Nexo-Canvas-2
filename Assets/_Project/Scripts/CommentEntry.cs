using UnityEngine;
using UnityEngine.UIElements;

public class CommentEntry
{
    private VisualElement root;
    private Label usernameLabel;
    private Label contentLabel;

    public VisualElement Root => root;

    public CommentEntry(VisualTreeAsset commentEntryAsset, CommentData data)
    {
        root = commentEntryAsset.Instantiate();

        usernameLabel = root.Q<Label>("username-label");
        contentLabel = root.Q<Label>("content-label");

        Populate(data);
    }

    public void Populate(CommentData data)
    {
        if (usernameLabel != null) usernameLabel.text = data.authorId;
        if (contentLabel != null) contentLabel.text = data.contenido;
    }
}