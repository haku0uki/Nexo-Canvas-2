// Plain data for a comment. Lives under postits/{postItId}/comentarios in Firestore.
// No position: comments are listed inside a PostItDetailPanel, not placed on the board.
[System.Serializable]
public class CommentData
{
    public string id;
    public string postItId;
    public string authorId;
    public string contenido;
}
