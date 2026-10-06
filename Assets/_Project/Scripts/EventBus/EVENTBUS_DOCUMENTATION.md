# SheepyCreations.EventBus

A type-safe, decoupled event system for Unity. Components communicate through events instead of
direct references, without manual bookkeeping to avoid memory leaks.

## Table of Contents

1. [Requirements](#requirements)
2. [Installation](#installation)
3. [Quick Start](#quick-start)
4. [Core Concepts](#core-concepts)
5. [API Reference](#api-reference)
6. [Visual Debugger](#visual-debugger)
7. [Best Practices](#best-practices)
8. [FAQ](#faq)
9. [Credits](#credits)

## Requirements

The package is self-contained — it has no dependencies outside Unity's own `UnityEngine` and
`UnityEditor` assemblies. Requires Unity 2021.3 or newer.

Includes Edit Mode tests (`Tests/Editor/`) covering `EventBus`, `EventBinding`, `EventGroup`
and the extension methods, and Play Mode tests (`Tests/Runtime/`) covering `EventBusListener<T>`'s
Unity lifecycle. Run them from `Window > General > Test Runner`.

## Installation

This is a UPM package (it has its own `package.json`). In the Package Manager window, use
**Add package from disk...** and select this folder's `package.json`, or add it as a Git
dependency in your `manifest.json` if it lives in its own repository.

## Quick Start

**1. Define an event.** Right-click in the Project window and use
`Create > SheepyCreations > Event` to generate a new event struct from a template (same
workflow as creating a new C# script), or write it by hand — any type that implements `IEvent`:

```csharp
public struct PlayerDiedEvent : IEvent
{
    public int RemainingLives;
}
```

**2. Subscribe, using an `EventGroup` to track the subscription:**

```csharp
public class UIManager : MonoBehaviour
{
    EventGroup events = new EventGroup();

    void OnEnable()
    {
        events.Subscribe<PlayerDiedEvent>(OnPlayerDied);
    }

    void OnDisable()
    {
        events.Clear();
    }

    void OnPlayerDied(PlayerDiedEvent @event)
    {
        Debug.Log($"Player died, {@event.RemainingLives} lives left");
    }
}
```

**3. Raise the event from anywhere:**

```csharp
EventBus<PlayerDiedEvent>.Raise(new PlayerDiedEvent { RemainingLives = 2 });

// Or with the extension method:
this.RaiseEvent(new PlayerDiedEvent { RemainingLives = 2 });
```

`events.Clear()` in `OnDisable` removes every subscription this component made, in one line —
no risk of a callback firing on a destroyed object.

## Core Concepts

### IEvent

An empty marker interface. Implement it on any struct or class to make it usable with the bus:

```csharp
public struct ScoreChangedEvent : IEvent
{
    public int NewScore;
}
```

### EventBus\<T\>

A static, type-safe channel for one event type. Every `IEvent` type gets its own independent bus —
subscribing to `PlayerDiedEvent` never receives a `ScoreChangedEvent`.

### EventBinding\<T\>

Wraps a callback so it can be registered and removed from an `EventBus<T>`. Accepts a callback
with the event data (`Action<T>`) or without it (`Action`), for cases where you only care that
something happened. Created automatically by `EventGroup.Subscribe` — you rarely need to
construct one directly.

### EventGroup

Groups multiple subscriptions so they can all be removed with a single `Clear()` call. This is
the recommended way to subscribe from any `MonoBehaviour` or manager: create one `EventGroup`
per owner, subscribe in `OnEnable`, clear in `OnDisable`.

### EventBusListener\<T\>

Lets you respond to an event from the Inspector, with no code. Create one small subclass per
event type:

```csharp
public class PlayerDiedListener : EventBusListener<PlayerDiedEvent> { }
```

Or use `Assets > Create > SheepyCreations > Event Listener`, pick the event type from the
dropdown, and the class is generated for you.

Add it to any GameObject and drag a response into its `On Event Raised` UnityEvent. Useful for
wiring simple reactions (play a sound, trigger an animation) without touching C#.

## API Reference

### EventBus\<T\>

| Member | Description |
|---|---|
| `Register(EventBinding<T> binding)` | Adds a binding to the bus. Prefer `EventGroup.Subscribe`. |
| `Deregister(EventBinding<T> binding)` | Removes a binding from the bus. |
| `Raise(T @event)` | Invokes every subscribed callback with the given event. |

### EventGroup

| Member | Description |
|---|---|
| `Subscribe<T>(Action<T> callback)` | Subscribes with the event data. Returns the group for chaining. |
| `Subscribe<T>(Action callback)` | Subscribes without the event data. Returns the group for chaining. |
| `Unsubscribe<T>()` | Removes every subscription of one event type from this group. |
| `Clear()` | Removes every subscription in this group. |
| `Count` | Number of active subscriptions in this group. |
| `HasSubscriptions` | True if `Count > 0`. |

### EventBusExtensions

| Member | Description |
|---|---|
| `binding.Unsubscribe()` | Removes a binding created by hand, outside an `EventGroup`. |
| `this.RaiseEvent(@event)` | Same as `EventBus<T>.Raise(@event)`, callable from any object. |
| `this.RaiseEvent<T>()` | Raises an empty struct event without instantiating it yourself. |
| `events.SubscribeWithLogging(this, callback)` | Subscribes and logs the exception if the callback throws. |
| `events.SubscribeIfActive(this, callback)` | Subscribes, but skips the callback while the GameObject is inactive. |
| `events.SubscribeMultiple<T1, T2>(callback)` | Subscribes the same no-args callback to 2 to 4 event types. |
| `events.SubscribeWithDebugLog(message, callback)` | Subscribes and logs every time the event fires. |
| `events.SubscribeOnce<T>(callback)` | Subscribes, unsubscribes itself right after the first time it fires. |
| `events.SubscribeIf(condition, callback)` | Subscribes, but only invokes the callback while `condition()` is true. |

### EventBusUtil

Handles automatic discovery and lifecycle of every `EventBus<T>` in the project. You will rarely
call this directly — `ClearAllBuses()` is the one method useful from your own code, mainly for
tests or a manual "reset" button.

### EventBusListener\<T\>

| Member | Description |
|---|---|
| `onEventRaised` | `UnityEvent`, assigned in the Inspector, invoked whenever `T` is raised. |

## Visual Debugger

`Tools > SheepyCreations > EventBus > Visual Debugger`

Shows, in real time during Play Mode, every event type currently registered and how many
subscribers it has. Expand an entry to see exactly which methods are subscribed, and use its
**Raise** button to manually fire that event with default values — useful for testing a
subscriber's reaction without having to reach that situation in gameplay. The row color gives a
quick read on subscriber count: neutral (0), green (1-2), yellow (3-5), red (6+) — a high count
is often a sign of a leaked subscription. Use "Clear All Buses" to reset every bus by hand.

The **History** tab shows the last 100 raised events, most recent first, with how long ago each
fired — useful for diagnosing order-of-execution issues. History resets when exiting Play Mode.

## Best Practices

**Do:**
- Subscribe through an `EventGroup` and call `Clear()` in `OnDisable`/`OnDestroy`.
- Keep event structs small and focused on what happened, not how to handle it.
- Use events for occasional, cross-system notifications (a death, a level completed, a setting
  changed).

**Avoid:**
- Subscribing without an `EventGroup` (or another explicit unsubscribe plan) — this is how leaks
  happen.
- Raising events every frame for high-frequency data (position, velocity). A direct reference or
  a serialized field is cheaper and clearer for that.
- Putting complex logic inside the callback itself — extract it to a named method so the
  Visual Debugger's method names stay meaningful.

**Validation:** passing a `null` callback throws `ArgumentNullException` immediately, instead of
silently doing nothing or failing later when the event fires. `EventBus` is also not thread-safe —
calling `Register`, `Deregister` or `Raise` from a background thread logs a warning.

## FAQ

**Do I need to unregister events manually?**
Not if you use `EventGroup.Clear()`. If you created an `EventBinding<T>` by hand, call
`binding.Unsubscribe()`.

**What happens if I raise an event with no subscribers?**
Nothing — `Raise` simply has nothing to iterate over.

**Does this work with Assembly Definitions (asmdefs)?**
Yes. Event types are discovered across every loaded assembly, not just `Assembly-CSharp`.

**Can two different event types share the same bus?**
No — each `IEvent` type gets its own independent `EventBus<T>`.

## Credits

Originally based on the EventBus pattern from Adam Myhre's
[Unity-Event-Bus](https://github.com/adammyhre/Unity-Event-Bus) tutorial, used and expanded
under the MIT License. See `THIRD-PARTY-NOTICES.md`.
