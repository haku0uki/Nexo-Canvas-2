# EventBus Diagrams

## 1. Architecture Overview

```mermaid
graph TD
    subgraph Publishers
        P1[Any MonoBehaviour or system]
    end

    subgraph Core
        Bus[EventBus of T]
        Binding[EventBinding of T]
        Group[EventGroup]
    end

    subgraph Subscribers
        S1[UIManager]
        S2[AudioManager]
        S3[Any other system]
    end

    P1 -->|Raise event| Bus
    Group -->|creates and tracks| Binding
    Binding -->|Register / Deregister| Bus
    Bus -->|invokes callback| S1
    Bus -->|invokes callback| S2
    Bus -->|invokes callback| S3
```

## 2. Class Diagram

```mermaid
classDiagram
    class IEvent {
        <<interface>>
    }

    class EventBus~T~ {
        +Register(EventBinding~T~)
        +Deregister(EventBinding~T~)
        +Raise(T event)
    }

    class EventBinding~T~ {
        -Action~T~ onEvent
        +Add(Action~T~)
        +Remove(Action~T~)
        +RemoveFromBus()
    }

    class EventGroup {
        -List~IEventBindingBase~ bindings
        +Subscribe~T~(Action~T~) EventGroup
        +Unsubscribe~T~()
        +Clear()
    }

    class IEventBindingBase {
        <<interface>>
        +RemoveFromBus()
    }

    IEvent <.. EventBus~T~ : constrains T
    EventBinding~T~ ..|> IEventBindingBase
    EventGroup --> IEventBindingBase : tracks
    EventBinding~T~ --> EventBus~T~ : registers with
```

## 3. Subscription Flow

```mermaid
sequenceDiagram
    participant MB as MonoBehaviour
    participant Group as EventGroup
    participant Binding as EventBinding~T~
    participant Bus as EventBus~T~

    MB->>Group: Subscribe<T>(callback)
    Group->>Binding: new EventBinding<T>(callback)
    Group->>Bus: Register(binding)
    Bus-->>Group: added to bindings set
    Group->>Group: track binding locally
    Group-->>MB: returns self (for chaining)
```

## 4. Raise Flow

```mermaid
sequenceDiagram
    participant Source as Any code
    participant Bus as EventBus~T~
    participant B1 as Binding 1
    participant B2 as Binding 2

    Source->>Bus: Raise(event)
    Bus->>Bus: snapshot current bindings
    loop for each binding still registered
        Bus->>B1: OnEvent.Invoke(event)
        Bus->>B2: OnEvent.Invoke(event)
    end
```

## 5. Cleanup Flow

```mermaid
sequenceDiagram
    participant MB as MonoBehaviour
    participant Group as EventGroup
    participant Bus as EventBus~T~

    MB->>Group: OnDisable() calls events.Clear()
    loop for each tracked binding
        Group->>Bus: binding.RemoveFromBus()
        Bus-->>Group: Deregister(binding)
    end
    Group->>Group: clear internal list
```

## 6. Observer Pattern vs Event Bus

```mermaid
graph LR
    subgraph Direct References
        A1[PlayerHealth] -->|direct reference| A2[UIManager]
        A1 -->|direct reference| A3[AudioManager]
        A1 -->|direct reference| A4[ParticleManager]
    end

    subgraph Event Bus
        B1[PlayerHealth] -->|Raise| Bus((EventBus))
        Bus -->|notifies| B2[UIManager]
        Bus -->|notifies| B3[AudioManager]
        Bus -->|notifies| B4[ParticleManager]
    end
```

Direct references couple the publisher to every subscriber, and adding a new subscriber means
editing the publisher. With an event bus, the publisher only knows about the event it raises —
subscribers can be added or removed without ever touching it.

**When to use events:** occasional, meaningful occurrences (death, level complete, settings
changed). **When to prefer a direct reference:** high-frequency data (position, velocity) updated
every frame, where the overhead and indirection of an event aren't worth it.
