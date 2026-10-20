using System.Collections.Generic;
using SheepyCreations.EventBus;
using SheepyCreations.UIControl.Runtime;
using UnityEngine;
using UnityEngine.UIElements;

// Opened when a post-it is tapped (BoardCanvas raises EntryClickedEvent<PostItData> via EventBus).
// Shows the post-it enlarged, its comment thread, and lets the user publish a new comment.
public class PostItDetailPanel : MonoBehaviour
{
    public enum DetailKind { Text, Drawing }

    [Header("Kind")]
    [Tooltip("Which post-its this panel opens for. Use one panel per kind: text details and drawing details have different UXML.")]
    [SerializeField] private DetailKind kind = DetailKind.Text;

    [Header("Enlarged post-it")]
    [SerializeField] private UIDocumentPanel panel;
    [SerializeField] private LabelTextBridge authorLabel;
    [SerializeField] private LabelTextBridge contentLabel;

    [Header("Drawing post-its")]
    [Tooltip("Drawing kind only. Paints the strokes in the 'drawing-view' element of the drawing details UXML. Canvas Width Units must match DrawTest.")]
    [SerializeField] private DrawingRenderer drawingRenderer;

    [Header("Comments")]
    [Tooltip("UXML template for a single comment row (matches CommentEntry's constructor).")]
    [SerializeField] private VisualTreeAsset commentEntryAsset;
    [Tooltip("No bridge type exists for a generic container, so this is still resolved by name.")]
    [SerializeField] private string commentsContainerName = "comments-scrollview";
    [SerializeField] private TextFieldBridge commentInput;
    [SerializeField] private ButtonBridge publishButton;
    [SerializeField] private ButtonBridge backButton;

    [Header("Author profile")]
    [Tooltip("Optional. Panel that shows the profile of the post-it's author.")]
    [SerializeField] private UserProfileViewPanelController profilePanel;
    [Tooltip("Optional. Button 'author-profile-button' in the details UXML.")]
    [SerializeField] private ButtonBridge authorProfileButton;
    [Tooltip("Name of the author Label inside each comment row (comment-entry.uxml). Tapping it opens that user's profile.")]
    [SerializeField] private string commentAuthorElementName = "username-label";

    private VisualElement commentsContainer;
    private readonly Dictionary<string, CommentEntry> comments = new Dictionary<string, CommentEntry>();
    private string currentPostItId;
    private PostItData currentPostIt;

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
        if (authorProfileButton != null) authorProfileButton.Clicked += OpenAuthorProfile;
    }

    private void OnDisable()
    {
        eventGroup.Clear();

        if (publishButton != null) publishButton.Clicked -= PublishComment;
        if (backButton != null) backButton.Clicked -= Close;
        if (authorProfileButton != null) authorProfileButton.Clicked -= OpenAuthorProfile;
    }

    private void OnEntryClicked(EntryClickedEvent<PostItData> evt)
    {
        // Same rule BoardCanvas uses to pick the entry prefab: "dibujo" is a drawing, anything else is text.
        bool isDrawing = evt.Data.tipo == "dibujo";
        if (isDrawing != (kind == DetailKind.Drawing)) return;

        Open(evt.Data);
    }

    public void Open(PostItData postIt)
    {
        if (IsOpeningBlocked) return;

        ResolveCommentsContainer();
        Close();

        currentPostItId = postIt.id;
        currentPostIt = postIt;
        bool isDrawingPanel = kind == DetailKind.Drawing;
        if (authorLabel != null) authorLabel.SetText(postIt.authorId);
        if (contentLabel != null) contentLabel.SetText(isDrawingPanel ? string.Empty : postIt.contenido);
        ShowDrawing(isDrawingPanel ? postIt.contenido : null);

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

    // The author is stored as an email, so the profile is looked up by email.
    public void OpenAuthorProfile()
    {
        if (currentPostIt != null) OpenProfileOf(currentPostIt.authorId);
    }

    // Opens a profile and, when the user goes back, reopens this same post-it detail.
    private void OpenProfileOf(string authorEmail)
    {
        if (profilePanel == null || string.IsNullOrEmpty(authorEmail)) return;

        PostItData postIt = currentPostIt;
        Close(); // Stop the comments listener before another panel takes over.

        profilePanel.ShowProfileByEmail(authorEmail, () =>
        {
            if (postIt != null) Open(postIt);
        });
    }

    private void RegisterCommentAuthorClick(VisualElement row, string authorEmail)
    {
        if (profilePanel == null || row == null) return;

        Label authorLabel = row.Q<Label>(commentAuthorElementName);
        if (authorLabel == null) return;

        authorLabel.AddToClassList("profile-link");
        authorLabel.RegisterCallback<ClickEvent>(evt => OpenProfileOf(authorEmail));
    }

    private void ShowDrawing(string json)
    {
        if (drawingRenderer == null) return;

        if (string.IsNullOrEmpty(json))
        {
            drawingRenderer.Clear();
            return;
        }

        try
        {
            drawingRenderer.SetDrawing(DrawingSerializer.Deserialize(json));
        }
        catch (System.ArgumentException exception)
        {
            drawingRenderer.Clear();
            Debug.LogWarning($"PostItDetailPanel: invalid drawing JSON. {exception.Message}", this);
        }
    }

    private void OnCommentsUpdated(List<CommentData> updatedComments)
    {
        foreach (CommentData data in updatedComments)
        {
            if (comments.ContainsKey(data.id)) continue;

            CommentEntry entry = new CommentEntry(commentEntryAsset, data);
            commentsContainer?.Add(entry.Root);
            RegisterCommentAuthorClick(entry.Root, data.authorId);
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