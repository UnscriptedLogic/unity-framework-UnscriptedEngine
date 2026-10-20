# Canvas controllers, screens, and widgets

All three types live in `UnscriptedEngine.UI` and are local `MonoBehaviour` components.

- `UCanvasController` owns the Unity Canvas, ordered screen instances, modal backdrops, input subscriptions, focus leases, and action-to-screen bindings.
- `UCanvasComponent` is one screen. It owns screen data, lifecycle, widget membership, selection, and navigation. Its source is `Integrations/UI/Screens/UCanvasComponent.cs`.
- `UWidgetComponent` is a reusable control or composite. It registers with its nearest screen, receives focused input, and bubbles typed messages to parent widgets and the screen.

## Showing screens

Create a controller only for the local player. The caller owns its lifetime and should deactivate and destroy its GameObject during teardown.

```csharp
var ui = UCanvasController.Create(localController);
ui.BackAction = "Player/Escape";
ui.NextWidgetAction = "UI/NextWidget";
ui.PreviousWidgetAction = "UI/PreviousWidget";
ui.BindToggle("Player/Tab", inventoryPrefab, screen => screen.Initialize(inventory));
ui.BindOpen("Player/Escape", settingsPrefab);
ui.Show(hudPrefab);
```

`Show(prefab, initialize)` creates a new instance. The screen receives its controller, `OnAttached`, and initialization callback before activation. Layer and policy overrides must be configured before activation. A failed initialization removes the partial instance.

`BindOpen` and `BindToggle` retain one instance per binding. Toggle closes its focused instance; a covered instance stays covered. A hidden instance is shown again. Destroyed instances are recreated. Unrelated bindings cannot open through a modal unless explicitly registered with `allowDuringModal: true`. `Bind` accepts a factory for third-party adapters; the factory must return a screen owned by that controller. `CreateCanvas<T>` creates a screen without a prefab.

Screens expose `Show`, `Hide`, and `Close`. Close detaches and destroys the instance. `BringToFront` changes ordering within its existing layer.

## Layers and input

`CanvasLayer` orders HUD, Screen, Modal, then Overlay. Attachment order breaks ties. Passive overlays can render above a modal without taking focus.

| CanvasInputPolicy | Behavior |
| --- | --- |
| Passive | No routed input, interaction, or pointer interception |
| HandleOnly | Handles input and passes unhandled input downward |
| Modal | Blocks lower screens and gameplay, retaining its own focus lease |

The controller subscribes to `PlayerActionInputEvent` at priority 1000. Additional concrete `PlayerInputEvent` types can be registered with `RegisterInput<T>()`. Gameplay must continue respecting `InputFocus.IsBlocked(controller)` for held input state.

Input first reaches the focused widget, then parent widgets, then the screen. The controller next handles navigation, Back, and the focused screen's toggle binding. Modal boundaries consume unhandled input. Explicit global bindings run before screen routing. Open/toggle/navigation bindings react only to non-synthetic performed presses.

Back closes only the top dismissible screen and consumes that press before an underlying open binding can run. Override `Dismissible` or `HandleBack` for special behavior. Covered controls become non-interactable; a full-screen backdrop blocks pointer click-through. Closing a modal restores the prior screen selection if still valid.

Use one Unity EventSystem and the appropriate UI input module. That module owns pointer events, directional navigation, and Submit for standard controls; do not also forward those actions into a second button execution path. The custom NextWidget/PreviousWidget actions only move selection. In the sample project they use F6/F7, leaving Tab as the Inventory toggle. Bindings can be changed in the input action asset.

## Reusable widgets

Put widgets under a screen in the prefab, or instantiate them beneath it at runtime. Widgets automatically register on enable and unregister on disable/destruction. Reparenting a widget or widget composite updates ownership. Call `RefreshOwner` after other hierarchy changes that do not move a widget's own transform.

A widget is a navigation stop only when `Focusable` is true and its configured or discovered `Selectable` is active and interactable. `TabOrder` orders stops; equal values preserve registration order. `NavigationGroup` makes a composite one stop, excluding its child widgets from screen-level traversal. The composite can handle internal directional input itself.

```csharp
public readonly struct ItemChosen
{
    public ItemChosen(string id) { Id = id; }
    public string Id { get; }
}

public class ItemWidget : UWidgetComponent
{
    public void Choose(string id) => Raise(new ItemChosen(id));
}

public class InventoryScreen : UCanvasComponent
{
    protected override bool HandleMessage<T>(UWidgetComponent source, T message)
    {
        if (message is not ItemChosen chosen) return false;
        // Update details using chosen.Id.
        return true;
    }
}
```

Messages bubble locally through parent widgets until handled, then reach the screen. They do not use the global EventBus. Settings widgets emit `SettingEdited` after an accepted write. Widgets can override `HandleInput`, `OnFocusGained`, and `OnFocusLost`, or call `RequestFocus`. Screen focus and widget focus synchronize with EventSystem selection.

## Migration

The former manager `UCanvasComponent` is now `UCanvasController`; the former screen `UWidgetComponent` is now `UCanvasComponent`. Their script GUIDs follow those roles. The new reusable `UWidgetComponent` has a new GUID. Existing serialized layer, policy, and default-selection fields stay on the screen base. Update custom source subclasses to the appropriate base and replace manager `AttachWidget` calls with `Show`.

Settings, Inventory, HUD, and the console adapter use the controller. `DefaultPawnUIHandler` contains only local-player initialization, bindings, dependencies, and teardown. Generated screen and widget prefabs contain RectTransform and CanvasGroup; the controller supplies rendering and scaling. Legacy screen prefabs may retain nested Canvas components, whose sorting override is normalized when shown.

Current selection uses `EventSystem.current`; cursor state is global. Separate simultaneous local-player UI selection requires a per-player EventSystem integration. UI state is not network-replicated.
