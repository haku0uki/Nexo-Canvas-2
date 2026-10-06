using SheepyCreations.EventBus;
public struct EntryClickedEvent<PostItData> : IEvent
{
    public PostItData Data;

    public EntryClickedEvent(PostItData data)
    {
        Data = data;
    }
}
