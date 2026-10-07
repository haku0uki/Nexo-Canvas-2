using System.Collections.Generic;
using SheepyCreations.EventBus;
using SheepyCreations.UIControl.Runtime;
using UnityEngine;
using UnityEngine.UIElements;

// Opened when a post-it is tapped (BoardCanvas raises EntryClickedEvent<PostItData> via EventBus).
// Shows the post-it enlarged, its comment thread, and lets the user publish a new comment.
public class PostItDetailPanel : MonoBehaviour
{
    [Header("Enlarged post-it")]
    [SerializeField] private UIDocumentPanel panel;
    [SerializeField] private LabelTextBridge authorLabel;
    [SerializeField] private LabelTextBridge contentLabel;

    [Header("Comments")]
    [Tooltip("UXML template for a single comment row (matches CommentEntry's constructor).")]
    [SerializeField] private VisualTreeAsset commentEntryAsset;
    [SerializeField] private string commentsContainerName = "comments-scrollview";
    [SerializeField] private string commentInputName = "comment-input";
    [SerializeField] private string publishButtonName = "publish-comment-button";
    [SerializeField] private string backButtonName = "back-button";

    private VisualElement commentsContainer;
    private TextField commentInput;
    private Button publishButton;
    private Button backButton;
    private bool elementsResolved;

    private readonly Dictionary<string, CommentEntry> comments = new Dictionary<string, CommentEntry>();
    private string currentPostItId;

    private EventGroup eventGroup;

    private void Awake()
    {
        eventGroup = new EventGroup();
    }

    private void OnEnable()
    {
        eventGroup.Subscribe<EntryClickedEvent<PostItData>>(OnEntryClicked);
    }

    private void OnDisable()
    {
        eventGroup.Clear();
    }

    private void OnEntryClicked(EntryClickedEvent<PostItData> evt)
    {
        Open(evt.Data);
    }

    public void Open(PostItData postIt)
    {
        // Resolve elements FIRST: the UXML ships 8 preview comment-entry instances (UI Builder
        // authoring aid), so Close() needs a valid commentsContainer reference to actually clear
        // them on this very first call -- otherwise they'd render alongside the real Firestore data.
        ResolveElements();
        Close(); // Also handles the case where a different post-it's detail was already open.

        currentPostItId = postIt.id;
        if (authorLabel != null) authorLabel.SetText(postIt.authorId);
        if (contentLabel != null) contentLabel.SetText(postIt.contenido);

        FirestoreService.Instance.EscucharComentarios(postIt.id, OnCommentsUpdated);
        if (panel != null) panel.Show();
    }

    public void Close()
    {
        if (currentPostItId != null)
        {
            FirestoreService.Instance.DejarDeEscucharComentarios(currentPostItId);
        }

        comments.Clear();
        commentsContainer?.Clear();
        currentPostItId = null;

        if (panel != null) panel.Hide();
    }

    private void OnCommentsUpdated(List<CommentData> updatedComments)
    {
        foreach (CommentData data in updatedComments)
        {
            if (comments.ContainsKey(data.id)) continue; // Append-only for now, same as post-its.

            CommentEntry entry = new CommentEntry(commentEntryAsset, data);
            commentsContainer?.Add(entry.Root);
            comments.Add(data.id, entry);
        }
    }

    private void PublishComment()
    {
        if (currentPostItId == null || commentInput == null) return;

        string texto = commentInput.value?.Trim();
        if (string.IsNullOrEmpty(texto)) return;

        string autorId = AuthManager.Instance.CurrentUser != null
            ? AuthManager.Instance.CurrentUser.Email
            : "anonimo";

        FirestoreService.Instance.PublicarComentario(currentPostItId, autorId, texto);
        commentInput.value = string.Empty;
    }

    // The document root may not exist on the very first Open() call, so this resolves lazily
    // and only once -- same pattern used for the click registration in TextCanvasEntry.
    private void ResolveElements()
    {
        if (elementsResolved || panel == null || panel.RootVisualElement == null) return;

        panel.TryGetElement(commentsContainerName, out commentsContainer);
        panel.TryGetElement(commentInputName, out commentInput);

        if (panel.TryGetElement(publishButtonName, out publishButton))
            publishButton.clicked += PublishComment;

        if (panel.TryGetElement(backButtonName, out backButton))
            backButton.clicked += Close;

        elementsResolved = true;
    }
}