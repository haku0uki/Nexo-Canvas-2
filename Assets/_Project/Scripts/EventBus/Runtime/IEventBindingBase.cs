namespace SheepyCreations.EventBus
{
    /// <summary>
    /// Non-generic base interface that lets bindings of different event types be grouped
    /// together. Used by EventGroup to clean up subscriptions without knowing the generic
    /// event type of each binding.
    /// </summary>
    public interface IEventBindingBase
    {
        /// <summary>
        /// Removes this binding from its corresponding EventBus.
        /// </summary>
        void RemoveFromBus();
    }
}
