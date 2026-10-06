using System;
using System.Collections.Generic;
using SheepyCreations.EventBus;
using SheepyCreations.UIControl.Runtime;
using UnityEngine;
using UnityEngine.UIElements;

public class PostItDetailPanel : PanelBehaviour
{
    [Header("Enlarged post-it")]
    [SerializeField] private LabelTextBridge authorLabel;
    [SerializeField] private LabelTextBridge contentLabel;

    [Header("Comments")]
    [SerializeField] private string commentsScrollViewName = "comments-scrollview";
    [SerializeField] private VisualTreeAsset commentEntryAsset;

    private readonly Dictionary<string, CommentEntry> comments = new Dictionary<string, CommentEntry>();
    private string currentPostItId;

    private VisualElement commentsContainer;

    private EventGroup eventGroup;

    private void Start()
    {
        eventGroup = new EventGroup();
        eventGroup.Subscribe<EntryClickedEvent<PostItData>>(OnEntryClicked);
    }

    private void OnEntryClicked(EntryClickedEvent<PostItData> @event)
    {
        Open(@event.Data);
    }


    public void Open(PostItData postIt)
    {
        Close(); // Limpiar estado anterior si había otro post-it abierto

        currentPostItId = postIt.id;

        if (authorLabel != null) authorLabel.SetText(postIt.authorId);
        if (contentLabel != null) contentLabel.SetText(postIt.contenido);

        if (panel is UIDocumentPanel documentPanel)
        {
            var scrollView = documentPanel.RootVisualElement.Q<ScrollView>(commentsScrollViewName);
            if (scrollView != null)
            {
                commentsContainer = scrollView.contentContainer;

                commentsContainer.Clear();
            }
        }

        FirestoreService.Instance.EscucharComentarios(postIt.id, OnCommentsUpdated);
        ShowPanel();
    }

    public void Close()
    {
        if (currentPostItId != null)
        {
            FirestoreService.Instance.DejarDeEscucharComentarios(currentPostItId);
        }

        if (commentsContainer != null)
        {
            commentsContainer.Clear();
        }
        comments.Clear();
        currentPostItId = null;

        HidePanel();
    }

    private void OnCommentsUpdated(List<CommentData> updatedComments)
    {
        if (commentsContainer == null) return;

        foreach (CommentData data in updatedComments)
        {
            if (comments.ContainsKey(data.id)) continue;

            var newCommentEntry = new CommentEntry(commentEntryAsset, data);

            comments[data.id] = newCommentEntry;

            commentsContainer.Add(newCommentEntry.Root);
        }
    }
}