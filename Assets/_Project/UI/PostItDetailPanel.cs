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
    [Tooltip("No bridge type exists for a generic container, so this is still resolved by name.")]
    [SerializeField] private string commentsContainerName = "comments-scrollview";
    [SerializeField] private TextFieldBridge commentInput;
    [SerializeField] private ButtonBridge publishButton;
    [SerializeField] private ButtonBridge backButton;

    private VisualElement commentsContainer;
    private readonly Dictionary<string, CommentEntry> comments = new Dictionary<string, CommentEntry>();
    private string currentPostItId;

    // Set by PostItCreationFlow for the whole placement flow, so tapping an existing post-it
    // doesn't open its detail while a new one is being placed.
    public bool IsOpeningBlocked { get; set; }

    private EventGroup eventGroup;

    private void Awake()
    {
        eventGroup = new EventGroup();
    }

    private void OnEnable()
    {
        eventGroup.Subscribe<EntryClickedEvent<PostItData>>(OnEntryClicked);

        if (publishButton != null) publishButton.Clicked += PublishComment;
        if (backButton != null) backButton.Clicked += Close;
    }

    private void OnDisable()
    {
        eventGroup.Clear();

        if (publishButton != null) publishButton.Clicked -= PublishComment;
        if (backButton != null) backButton.Clicked -= Close;
    }

    private void OnEntryClicked(EntryClickedEvent<PostItData> evt)
    {
        Open(evt.Data);
    }

    public void Open(PostItData postIt)
    {
        if (IsOpeningBlocked) return;

        // commentsContainer has no bridge type, so it's the only element still resolved by name;
        // doing it before Close() lets the first Open() clear the 8 UI Builder preview rows too.
        ResolveCommentsContainer();
        Close(); // Also handles the case where a different post-it's detail was already open.

        currentPostItId = postIt.id;
        if (authorLabel != null) authorLabel.SetText(postIt.authorId);
        if (contentLabel != null) contentLabel.SetText(postIt.contenido);

        FirestoreService.Instance.EscucharComentarios(postIt.id, OnCommentsUpdated);
        if (panel != null) panel.ShowIsolatedViaController();
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

        string texto = commentInput.Value?.Trim();
        if (string.IsNullOrEmpty(texto)) return;

        string autorId = AuthManager.Instance.CurrentUser != null
            ? AuthManager.Instance.CurrentUser.Email
            : "anonimo";

        FirestoreService.Instance.PublicarComentario(currentPostItId, autorId, texto);
        commentInput.SetValue(string.Empty);
    }

    private void ResolveCommentsContainer()
    {
        if (commentsContainer != null || panel == null || panel.RootVisualElement == null) return;
        panel.TryGetElement(commentsContainerName, out commentsContainer);
    }
}