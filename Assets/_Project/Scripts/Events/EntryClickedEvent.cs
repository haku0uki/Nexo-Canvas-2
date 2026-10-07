using SheepyCreations.EventBus;

public struct EntryClickedEvent<TData> : IEvent
{
    public TData Data;

    public EntryClickedEvent(TData data)
    {
        Data = data;
    }
}