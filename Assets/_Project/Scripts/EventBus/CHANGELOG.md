# Changelog

All notable changes to this package are documented here.

## [1.0.0] - 2026-09-17

### Added
- Type-safe `EventBus<T>`, `EventBinding<T>` and `EventGroup` for decoupled, leak-safe event subscriptions.
- Extension methods: `RaiseEvent`, `SubscribeOnce`, `SubscribeIf`, `SubscribeMultiple`, `SubscribeWithLogging`, `SubscribeIfActive`, `SubscribeWithDebugLog`.
- `EventBusListener<T>`, an Inspector-friendly component that raises a `UnityEvent` when an event fires.
- Visual Debugger (`Tools > SheepyCreations > EventBus > Visual Debugger`), built with UI Toolkit, showing live subscriptions per event type, with a manual Raise button and a History tab of recently raised events.
- Event and Event Listener creation wizards under `Assets > Create > SheepyCreations`.
- Edit Mode test suite covering the core API, plus Play Mode tests for `EventBusListener<T>`.
- UPM package structure (`Runtime`/`Editor` asmdefs, `package.json`).

### Fixed
- `EventBusUtil.cs` had an unguarded `using UnityEditor;`, which would have broken compilation in a standalone Player build. Now scoped to `#if UNITY_EDITOR`.
