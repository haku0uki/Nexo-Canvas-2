using System;
using UnityEngine;

// Base type for anything that can appear as an entry on the board (text, drawing).
public abstract class BaseCanvasEntry : MonoBehaviour
{
    // Raised when the user taps this entry. BoardCanvas listens to this to open PostItDetailPanel.
    public event Action<PostItData> Clicked;

    public string Id { get; private set; }
    protected PostItData Data { get; private set; }

    // Called by BoardCanvas right after instantiation, and again whenever Firestore reports an update.
    public virtual void Populate(PostItData data)
    {
        Data = data;
        Id = data.id;
        transform.localPosition = new Vector3(data.posX, data.posY, data.posZ);
    }

    public virtual void RefreshAuthorProfile(string authorId)
    {
    }

    protected void RaiseClicked()
    {
        if (Data != null) Clicked?.Invoke(Data);
    }
}