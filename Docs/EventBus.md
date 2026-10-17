# EventBus

`EventBus` is a static, synchronous, typed dispatcher in the `UnscriptedEngine` namespace. Subscribers register callbacks for an event type; publishers send values of that type.

## 🧭 Purpose and design

The bus centralizes gameplay notifications and input events without requiring publishers to know their listeners. Priority order supports input interception; `ICancellableEvent` lets a listener stop later callbacks. Use `Publish` for decisions and cancellable events, and `PublishNotification` for post-commit notifications that must reach every subscriber.

## 🚀 Getting started

Define an event type, subscribe and unsubscribe with the same callback, then publish it:

```csharp
using UnityEngine;
using UnscriptedEngine;

public sealed class HealthChangedEvent
{
    public HealthChangedEvent(int current) => Current = current;
    public int Current { get; }
}

public sealed class HealthHud : MonoBehaviour
{
    private void OnEnable() => EventBus.Subscribe<HealthChangedEvent>(OnHealthChanged, priority: 10);
    private void OnDisable() => EventBus.Unsubscribe<HealthChangedEvent>(OnHealthChanged);

    private void OnHealthChanged(HealthChangedEvent message)
    {
        Debug.Log($"Health: {message.Current}");
    }
}

public sealed class HealthPublisher
{
    public void SetHealth(int current)
    {
        EventBus.Publish(new HealthChangedEvent(current));
    }
}
```

Subscriptions are cleared at `RuntimeInitializeLoadType.SubsystemRegistration` for each Play session. Pair runtime subscriptions with lifecycle cleanup; re-subscribe when enabled.

## Using the bus

```csharp
EventBus.Subscribe<T>(listener, priority = 0);
EventBus.Subscribe<T>(listener, priority, AppendStyle);
EventBus.Unsubscribe<T>(listener);
bool completed = EventBus.Publish<T>(eventData);
EventBus.PublishNotification<T>(eventData);
EventBus.Notify<T>(subscribers, value);
EventBus.Notify(subscribers);
```

- Higher priority runs first. Equal priorities keep subscription order. Default priority is `0`.
- `Publish` checks cancellation after each callback. It returns `false` when a callback cancels an `ICancellableEvent`; otherwise it returns `true`, including when no listeners exist. A pre-cancelled value still reaches the first listener because there is no check before dispatch.
- `PublishNotification` delivers to every listener and logs each listener exception. It rejects any event implementing `ICancellableEvent` with `ArgumentException`.
- `Notify` invokes a supplied delegate or delegate list directly. It is not a bus subscription operation; each callback exception is logged and remaining callbacks still run.
- Subscribing a `null` listener throws `ArgumentNullException`. Duplicate subscriptions are allowed; each registration invokes separately, and one `Unsubscribe` removes one matching registration.

### Priority placement

`AppendStyle` changes placement when a priority is occupied:

| Style | Placement rule |
| --- | --- |
| `Latest` | Search upward from requested priority for a free integer. |
| `Earliest` | Search downward from requested priority for a free integer. |
| `Force` | Shift existing priorities at or above requested priority up by one, then use requested priority. |

Dispatch still runs from highest priority to lowest. Invalid styles throw `ArgumentOutOfRangeException`; integer overflow during placement throws `OverflowException`.

## 🧩 Cancellable events and extension

Implement `ICancellableEvent` when a listener may veto an operation. Call `Cancel()` from a subscriber; `Publish` stops dispatch after that callback and returns `false`.

```csharp
using System;
using UnscriptedEngine;

public sealed class TryOpenDoorEvent : ICancellableEvent
{
    public TryOpenDoorEvent(DoorSystem door) => Door = door;
    public DoorSystem Door { get; }
    public bool IsCancelled { get; private set; }
    public void Cancel() => IsCancelled = true;
}

public sealed class DoorSystem : IDisposable
{
    private readonly bool isLocked;

    public DoorSystem(bool isLocked)
    {
        this.isLocked = isLocked;
        EventBus.Subscribe<TryOpenDoorEvent>(Validate, priority: 100);
    }

    public bool TryOpen()
    {
        if (!EventBus.Publish(new TryOpenDoorEvent(this))) return false;
        // Perform the open operation here.
        return true;
    }

    private void Validate(TryOpenDoorEvent request)
    {
        if (request.Door == this && isLocked) request.Cancel();
    }

    public void Dispose() => EventBus.Unsubscribe<TryOpenDoorEvent>(Validate);
}
```

Use a separate non-cancellable event for notifications after an operation commits. Publish those with `PublishNotification` when exceptions must not prevent later listeners from receiving the notification.

## Player action input

`PlayerController` publishes callbacks from its enabled `Player`, `UI`, and runtime equipment maps as `PlayerActionInputEvent`. Subscribe to this one event type for built-in actions and actions added to those maps; filter by map and action name instead of declaring a new event class. A new action map still needs one map-level subscription and enable call.

```csharp
using UnscriptedEngine;
using UnityEngine;

public sealed class DashComponent : MonoBehaviour
{
    private void OnEnable() => EventBus.Subscribe<PlayerActionInputEvent>(OnInput, priority: 20);
    private void OnDisable() => EventBus.Unsubscribe<PlayerActionInputEvent>(OnInput);

    private void OnInput(PlayerActionInputEvent input)
    {
        if (input.Controller != GetComponentInParent<PlayerController>()) return;
        if (!input.IsAction("Player", "Dash") || !input.IsPerformed || !input.IsPressed) return;
        if (input.IsCancelled || input.IsGameplayBlocked) return;

        Dash();
        input.Cancel(); // Stops lower-priority listeners for this action callback.
    }

    private void Dash() { }
}
```

`Value` contains the Input System action's value; prefer `TryReadValue<T>` when the action may have different control types. `IsActionCanceled` describes Input System phase, while `IsCancelled` means an EventBus listener consumed the callback. `IsGameplayBlocked` snapshots input-focus state at publication. The bus publishes `started`, `performed`, and `canceled` phases. For an Escape key delivered through `UI/Cancel`, `ActionMap` and `ActionName` use the canonical `Player/Escape` key and `SourceActionMap` / `SourceActionName` retain the original action.

Existing typed player input events remain as a compatibility bridge for current actions. New listeners should use `PlayerActionInputEvent`; adding an action to an Input Action Map then requires no event class or publisher case.

## ⚠️ Usage limits

- Event channels use the compile-time generic type `T`. Publish and subscribe with the same type. Publishing a derived object through a base-typed variable uses the base type's channel.
- Each publish takes a listener snapshot. Subscriptions added or removed during dispatch affect later publishes; removing a listener does not remove it from the current snapshot.
- Exceptions from `Publish` propagate and stop dispatch. Use `PublishNotification` or `Notify` when each callback must be isolated.
- Destroyed Unity object targets are skipped and pruned. Other subscribers need explicit lifecycle cleanup.
- The static bus has no locking. Use it from one thread, typically Unity's main thread.

## Notes and changelog

- `EventBus` dispatches synchronously; callbacks run before the publish call returns.
- `EventBus.Notify` is a helper for delegate-backed notifications, separate from typed bus channels.
- 2026-10-17: Added EventBus documentation for package version 0.2.1.
