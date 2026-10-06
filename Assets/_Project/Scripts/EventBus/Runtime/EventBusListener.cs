using UnityEngine;
using UnityEngine.Events;

namespace SheepyCreations.EventBus
{
    /// <summary>
    /// Base component that raises a UnityEvent, wireable in the Inspector, whenever the given
    /// event type fires. Lets anyone connect a response to an event without writing code.
    ///
    /// Unity cannot add a generic component directly, so create one small subclass per event type:
    /// <code>
    /// public class PlayerDiedListener : EventBusListener&lt;PlayerDiedEvent&gt; { }
    /// </code>
    ///
    /// Then drag the response into onEventRaised in the Inspector.
    /// </summary>
    public abstract class EventBusListener<T> : MonoBehaviour where T : IEvent
    {
        [SerializeField] UnityEvent onEventRaised = new UnityEvent();

        readonly EventGroup events = new EventGroup();

        /// <summary>
        /// The UnityEvent invoked whenever T is raised. Exposed so listeners can also be
        /// added from code, in addition to the Inspector.
        /// </summary>
        public UnityEvent OnEventRaised => onEventRaised;

        void OnEnable()
        {
            events.Subscribe<T>(() => onEventRaised.Invoke());
        }

        void OnDisable()
        {
            events.Clear();
        }
    }
}
