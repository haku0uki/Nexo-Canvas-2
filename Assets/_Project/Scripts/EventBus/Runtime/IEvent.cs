namespace SheepyCreations.EventBus
{
    /// <summary>
    /// Marker interface every event must implement to be used with EventBus, EventBinding
    /// and EventGroup. Carries no members on its own; implement it on your own event
    /// types (usually structs) to define new events.
    /// </summary>
    public interface IEvent
    {
    }
}
