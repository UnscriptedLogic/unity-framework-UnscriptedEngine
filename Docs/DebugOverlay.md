# Debug overlay

Minecraft F3-style live values: light text over dark translucent panels in the top-left and top-right corners. Automatically creates a persistent, non-interactive canvas. No scene component, prefab, pawn or EventSystem setup needed. Requires uGUI and the Input System packages.

```csharp
using UnscriptedEngine.UI;

private void Update()
{
    DebugOverlay.Show(this, nameof(speed), speed);
    DebugOverlay.Show(this, "Position", transform.position, DebugOverlaySide.TopRight);
}
```

Call `Show` on Unity's main thread from `Update`, `LateUpdate`, or `FixedUpdate`. Owner plus name identifies a row; repeated calls update it. Same name on different owners creates separate rows. Use a stable name, not a changing value. Reusing a name with a different side moves that row.

Values expire after 0.5 seconds without an update. Destroyed owners and inactive Component owners are pruned on the next display refresh. Passing `this` makes lifetime follow the publishing component. `DebugOverlay.Remove(this)` optionally removes its rows immediately on the next refresh. Keep publishing while needed; a one-time call is intentionally temporary.

Press **F3**, or enable **Debug / Debug overlay (F3)** in the generated settings panel. Both controls update the same saved local-user setting, `DebugOverlay.VisibleSetting`. The automatic driver registers it for its application lifetime. No General Settings Provider changes needed. Keyboard toggle works even while menus are open. Browser or operating-system shortcuts may intercept F3; use settings in that case.

```csharp
SettingsRuntime.Service.TrySet(DebugOverlay.VisibleSetting, true);
```

Text refreshes at 10 Hz using unscaled time, including when paused. Each column is limited to 48% of screen width; long text wraps and excess vertical content clips. This is a compact debug view, not a scrollable log. Does not capture input, change cursor state or pause gameplay. Uses Unity's built-in font for browser compatibility. No built-in gameplay statistics are published; each system chooses its own values.

Hidden overlays skip storing and formatting values. C# still evaluates arguments before `Show`, so avoid expensive expressions or preformatted interpolated strings; pass existing values instead. Visible `Show` calls format their values on each call. For costly data, publish at a lower rate, keeping intervals below 0.5 seconds. Application statics reset at subsystem registration for disabled domain reload. Runtime hosts dispose settings registrations and subscriptions on disable. Batch-mode players skip automatic creation.

Editor tests cover owner isolation, repeated updates, column changes, owner cleanup, expiry and skipped hidden formatting. In Play Mode, also verify F3 and settings stay synchronized, pause behavior, scene transitions, screen resizing, and consecutive sessions with domain reload disabled.

## Entity inspection, HUD labels and world text

Add **Unscripted Engine / Debug / Debug Entity** to an NPC, chest or other entity root. This opts it into debugging without making it interactable. Add one or more components implementing `IDebugInfoSource` on the **same GameObject**:

```csharp
using UnityEngine;
using UnscriptedEngine.UI;

public sealed class ChestDebugInfo : MonoBehaviour, IDebugInfoSource
{
    [SerializeField, Tooltip("Chest component whose state this adapter reads.")]
    private Chest chest;

    public void CollectDebugInfo(DebugInfoWriter writer)
    {
        writer.Add("Locked", chest.IsLocked);
        writer.Add("Items", chest.ItemCount);
    }
}
```

`Chest`, `IsLocked` and `ItemCount` above represent your own gameplay API. Existing gameplay scripts can implement the interface directly instead of using an adapter. Providers append values with `writer.Add(name, value)`; all three presentations use the same output. Entity name, runtime entity ID and position are included automatically. Disabled providers are skipped; provider exceptions appear as a debug error value. Keep providers read-only: collection must not modify gameplay or entity registration.

- **F3 + look at a collider:** target details appear in a separate section of the right-hand overlay column. A child collider resolves the nearest parent `DebugEntity`. Solid geometry blocks inspection; trigger colliders are ignored. Entities without colliders can still have HUD/world labels.
- **F4:** pin the looked-at target, then inspect it while looking elsewhere. Press again to release. Pinning ignores distance and occlusion for the overlay, but does not bypass label limits. Disabled or destroyed targets release automatically. Pin persists while F3 is hidden, until released or the debug driver stops.
- **Debug / Entities / Entity HUD labels:** show screen-space text following nearby entities. Off by default.
- **Debug / Entities / Entity world labels:** show camera-facing world-space text at each entity's label point. Off by default. Enable either or both label settings as needed.

All entity views require the F3 overlay to be visible. The inspector is enabled by default. Each `DebugEntity` has Inspector/Hud/World permissions, an optional display name, and a local-space label offset. Configure the offset above the object's collider to keep text clear of the object. HUD text stays constant in screen size; world text uses a fixed world size and shrinks with distance. Neither captures cursor or UI focus.

Settings also control maximum distance (default 30 units), nearest candidate count (default 12, maximum 64), and wall occlusion (on by default). Only enabled views collect data. Candidate selection scans the registered entity set at 10 Hz, retaining at most the nearest N on-screen candidates. Physics checks are bounded to those candidates; occluded candidates can leave fewer than N labels. Screen/behind-camera/distance checks and label movement run every frame. Occlusion can lag by up to a refresh interval. Labels are pooled; no per-frame scene search or per-refresh object creation after the pool warms up. One bounded snapshot per shown entity is reused across all views during that refresh.

HUD panels have a fixed width and cap their height; world panels use the same layout at 0.005 world units per UI unit. Text is plain, bounded to 24 values/4096 characters and clipped if the panel fills. There is no scrolling or overlap avoidance. Nearby labels can overlap; reduce distance/count or disable one label mode when needed. Entity registration recovers once at startup when both domain and scene reload are disabled, then follows component enable/disable lifetimes.

The default camera is `Camera.main`, which works with the actual camera rendering a Cinemachine view. Use `DebugEntities.CameraOverride` for an untagged or alternate rendering camera. This implementation supports one application-wide debug camera and 3D physics, not per-player split-screen inspection or Physics2D targeting. Camera changes and scene transitions are resolved automatically through the main-camera fallback.

```csharp
DebugEntities.CameraOverride = renderingCamera;
DebugEntities.PhysicsLayers = debugRaycastLayers; // Exclude local-player colliders if they obstruct the view.
DebugEntities.TogglePin();
DebugEntities.ClearPin();
DebugEntity inspected = DebugEntities.Inspected;
```

View settings are exposed as `DebugEntities.InspectSetting`, `HudSetting`, `WorldSetting`, `DistanceSetting`, `LabelLimitSetting` and `OcclusionSetting` for custom settings UI. The automatic debug driver registers them beside the overlay setting.

Additional editor tests cover registry/pin lifetime, child-collider targeting, distance limits, solid/trigger behavior, provider collection, display permissions, local offsets, bounded output and label occlusion. Visual checks should include all modes together, moving cameras/entities, walls, resolution changes, despawns and consecutive Play sessions.
