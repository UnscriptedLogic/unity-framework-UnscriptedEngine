# UCanvasComponent

`UCanvasComponent` manages runtime `UWidgetComponent` instances for one `UController`. It owns widget ordering, focus, Unity UI selection, and routing of cancellable player input events. API lives in `UnscriptedEngine.UI`.

## 🧭 Purpose and design

Widgets share one stack owner, so each panel does not need its own input subscription, focus logic, or modal bookkeeping. The canvas chooses the top interactive widget, updates its `CanvasGroup`, and sends registered input events from top to bottom. Modal widgets also acquire a controller-scoped `InputFocus` lease.

## 🚀 Getting started

1. Install `com.unity.ugui` 2.0.0 or newer to enable package UI integration.
2. Add `UCanvasComponent` to a Unity Canvas GameObject. Its required `Canvas`, `CanvasScaler`, and `GraphicRaycaster` components are added by Unity.
3. Create a prefab with a `UWidgetComponent` on its root. `RectTransform` and `CanvasGroup` are required automatically.
4. Add an `EventSystem` to the scene for keyboard/controller navigation and selected controls.
5. Initialize the canvas with the controller whose input events it should handle, then attach widget prefabs.

```csharp
canvas.Initialize(localController);
PauseMenu menu = canvas.AttachWidget(pauseMenuPrefab);
```

The controller must be initialized before attaching widgets. Canvas does not verify that controller is local; incoming events must carry the same controller instance.

## Using the stack

Layers order from lowest to highest: `HUD`, `Screen`, `Modal`, `Overlay`. Newer widgets appear above older widgets in the same layer. `BringToFront(widget)` makes a widget newest within its layer.

| `WidgetInputPolicy` | Behavior |
| --- | --- |
| `Passive` | Draws without taking focus, blocking raycasts, or receiving routed input. Useful for decorative HUD elements. |
| `HandleOnly` | Receives input. If `HandleInput` returns `true`, canvas cancels event; otherwise routing can continue to older widgets. |
| `Modal` | Blocks widgets below it, acquires an `InputFocus` lease, shows a raycast backdrop for the top visible modal, and consumes matching events even when `HandleInput` returns `false`. |

The top visible interactive widget receives focus. With `EventSystem.current`, canvas selects its last valid selection, configured default selection, or first active interactable `Selectable`. Without an EventSystem, focus callbacks still run, but Unity UI selection cannot be assigned.

Use `Show()` and `Hide()` to activate or hide an attached widget. Hiding releases its focus lease but keeps it attached. `Close()` detaches and destroys it. Disabling the canvas unsubscribes its input listeners and makes attached widgets non-interactive until re-enabled.

## 🧩 Extending widgets and input

Override `Layer` and `InputPolicy` for custom defaults. Use `HandleInput` for behavior. Lifecycle hooks include `OnAttached`, `OnDetached`, `OnShown`, `OnHidden`, `OnFocusGained`, and `OnFocusLost`.

`AttachWidget(prefab, initialize)` calls `OnAttached`, then the optional initialization callback, then activates the instance. Configure per-instance layer and policy in that callback, before canvas chooses the layer parent and refreshes focus.

Canvas registers these input event types automatically: `PlayerMoveInputEvent`, `PlayerLookInputEvent`, `PlayerSprintInputEvent`, `PlayerJumpInputEvent`, and `PlayerInteractInputEvent`. Register each custom event type explicitly; EventBus subscriptions use exact event types.

```csharp
using UnscriptedEngine;

public sealed class OpenInventoryInputEvent : PlayerInputEvent
{
    public OpenInventoryInputEvent(UController controller) : base(controller) { }
}

// After canvas.Initialize(controller):
canvas.RegisterInput<OpenInventoryInputEvent>();
EventBus.Publish(new OpenInventoryInputEvent(controller));
```

Registered events route only when canvas is active, event is not already cancelled, and `input.Controller` matches `OwningController`. Canvas listener priority is `UCanvasComponent.InputPriority` (`1000`). Gameplay code should check `InputFocus.IsBlocked(controller)` to respect an active modal lease.

## Example

This modal pause widget closes on the interact event. Its input is still consumed by the modal policy.

```csharp
using UnityEngine;
using UnscriptedEngine;
using UnscriptedEngine.UI;

public sealed class PauseMenu : UWidgetComponent
{
    public string Title { get; private set; }
    public void SetTitle(string value) => Title = value;

    public override WidgetLayer Layer => WidgetLayer.Modal;
    public override WidgetInputPolicy InputPolicy => WidgetInputPolicy.Modal;

    public override bool HandleInput(PlayerInputEvent input)
    {
        if (!(input is PlayerInteractInputEvent)) return false;
        Close();
        return true;
    }
}

public sealed class PauseMenuLauncher : MonoBehaviour
{
    [SerializeField] private UCanvasComponent canvas;
    [SerializeField] private PauseMenu pauseMenuPrefab;

    public void Initialize(UController localController)
    {
        canvas.Initialize(localController);
    }

    public void Open()
    {
        // Configure before showing; OnShown runs after this callback.
        PauseMenu menu = canvas.AttachWidget(pauseMenuPrefab, instance => instance.SetTitle("Paused"));
    }
}
```

## ⚠️ Usage limits

- Canvas routes only event types it registered and only for its owning controller. It does not discover or validate local-player ownership.
- Layer determines widget parent at attachment. Changing `Layer` afterward does not move the widget to another layer container. Configure layer before attachment.
- Policy changes are reflected when canvas next refreshes the stack. Configure policy before attachment for immediate focus and raycast state.
- Modal focus is controller-scoped for gameplay blocking, while Unity cursor state is process-wide.
- Widget state and stack are runtime UI state; canvas does not replicate them over the network.

## Notes and changelog

- Canvas creates internal layer containers and an inactive staging parent in `Awake`.
- A throwing initialization callback cleans up the partially attached widget and rethrows the exception.
- 2026-10-17: Added component documentation for package version 0.2.1.
