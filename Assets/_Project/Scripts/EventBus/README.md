# SheepyCreations EventBus

A type-safe, decoupled event system for Unity, with automatic cleanup and a UI Toolkit
Visual Debugger.

## Why

Direct references between systems (UI, audio, gameplay) get tangled fast, and manually
unregistering every event subscription is an easy way to leak memory or crash on a destroyed
object. This package solves both: events are strongly typed structs, and `EventGroup` clears
every subscription for you with a single call.

## Quick Start

```csharp
public struct PlayerDiedEvent : IEvent
{
    public int RemainingLives;
}

public class UIManager : MonoBehaviour
{
    EventGroup events = new EventGroup();

    void OnEnable() => events.Subscribe<PlayerDiedEvent>(OnPlayerDied);
    void OnDisable() => events.Clear();

    void OnPlayerDied(PlayerDiedEvent @event) =>
        Debug.Log($"Player died, {@event.RemainingLives} lives left");
}

// From anywhere:
this.RaiseEvent(new PlayerDiedEvent { RemainingLives = 2 });
```

Prefer no code at all? `Assets > Create > SheepyCreations > Event Listener` wires a `UnityEvent`
response to any event straight from the Inspector.

## Install

Package Manager > **Add package from disk...** > select this folder's `package.json`.

## Learn More

- [Full documentation](EVENTBUS_DOCUMENTATION.md) — API reference, Visual Debugger, best practices, FAQ.
- [Diagrams](EVENTBUS_MERMAID.md) — architecture and sequence diagrams.
- [Changelog](CHANGELOG.md)

## Credits

Originally based on the EventBus pattern from Adam Myhre's
[Unity-Event-Bus](https://github.com/adammyhre/Unity-Event-Bus) tutorial, used and expanded
under the MIT License. See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## License

MIT — see [LICENSE](LICENSE).
