# UI Movement Sway

## What it does

`UIMovementSway` adds movement-relative translation and pitch/yaw/roll to a canvas's UI groups, plus damped jump and landing jolts. Distance from the visible screen center controls each group's strength: centered groups stay still by default, and edge groups move most. It works with ordinary uGUI and UI Retainer content, including perspective/FOV warps.

## Setup

1. Select the root Canvas or a UI subtree and add **UnscriptedEngine > UI > Movement Sway** in Add Component.
2. Enter Play mode. A framework-owned canvas follows its owning controller's pawn. A `TaggedValueContext` instead supplies that canvas's actor. Plain canvases can discover a unique active local `CharacterMovementComponent` in the scene.
3. For an unowned canvas, multiple candidate actors, or a different source, assign **Movement Source** explicitly. **View** optionally supplies the camera-relative orientation; otherwise the canvas camera, main camera, or movement transform is used.

No movement component changes or UnityEvent wiring are needed. Pawn/context replacements reset motion sampling, preventing false jump/landing impulses. An empty framework ownership scope clears its source rather than choosing another scene actor. Ambiguous plain-canvas sources also remain unbound; assign the intended source.

## Tuning

- **Intensity** scales all motion. **Shift** uses each group's local UI units; **Tilt** and **Roll** use degrees.
- **Reference Speed** is the world speed producing full movement sway and baseline landing impact. Movement is relative to the view's horizontal heading. Forward/backward motion, strafing, vertical velocity, and turning contribute; **Look Influence** controls the turning contribution.
- **Follow Speed** controls movement smoothing. **Jump Impulse** and **Landing Impulse** control downward inertial jolts. Landing strength uses the fastest observed fall speed, even if the motor clears vertical velocity on contact. Walking off an edge does not trigger takeoff; flight transitions do not trigger jump/landing jolts.
- **Spring Frequency** and **Spring Damping** control rebound. Lower damping gives more bounce. Displacement is bounded to 20 local UI units before falloff/intensity.
- **Center Strength** defaults to zero. Raise it for some motion at screen center. **Edge Power** controls how quickly strength increases away from center. Screen edges reach full strength; corners are capped at the same maximum.

Motion uses scaled time, so it pauses with gameplay. Initial binding while already airborne does not produce a takeoff jolt. Grounded-state transitions are sampled once per rendered frame; unusually short airborne transitions entirely between frames cannot be observed.

## Automatic groups and layout

Automatic discovery traverses full-screen wrappers and zero-size containers, then moves smaller visual branches as groups. A group's visible rectangle center determines its falloff; child graphics move together so labels, fills, and backgrounds stay aligned. Full-screen backgrounds therefore usually remain stationary. This is group motion, not vertex deformation.

For finer grouping, populate **Targets** with the desired RectTransforms. Ancestor/descendant selections are collapsed to the ancestor to avoid compounded motion. **Excluded Roots** skips branches. Nested active `UIMovementSway` components own their own branches. Targets are rediscovered once per second and on direct hierarchy changes; call `RefreshTargets()` for immediate discovery after deep runtime UI changes.

No wrappers are inserted and no UI objects are reparented. Previous offsets are removed before layout, and current motion is applied after uGUI's layout pass. Disabling restores positions and rotations while retaining the layout's current pose. Ordinary pointer hit testing follows the animated transforms.

The root must belong to a rendered Canvas. Overlay and camera canvases are supported; screen-relative falloff uses the canvas camera's viewport when assigned. World-space canvases should have their event camera assigned. Arbitrary scripts that overwrite transforms after the canvas render callback, or force layout while bypassing normal canvas callbacks, need a dedicated visual child or explicit target arrangement.

## Retainers and custom integrations

Retainer content is traversed through the original retainer's `Content` reference, even while captured in another hierarchy. Falloff uses `MapFromSource` to measure the group's visible warped position. Animated retainer content requests refreshes when its pose changes, including when Refresh Rate is zero; this adds capture work while motion is active.

Captured groups are kept inside the capture viewport while swaying. Translation is corrected inward at the edges; if rotation makes a group larger than the viewport, it is uniformly scaled just enough to fit. Scale is restored alongside position and rotation when the effect is disabled. Authored mask viewports are measured instead of their hidden scrolling contents. The retainer camera also reserves depth around its UI plane so pitch/yaw does not slice through the image.

```csharp
UIMovementSway sway = canvas.GetComponent<UIMovementSway>();
sway.SetMovementSource(characterMovement);
sway.RefreshTargets();
```

This component samples `CharacterMovementComponent.Velocity`, `IsGrounded`, and `IsFlying`; it does not modify the pawn or depend on the render pipeline. For a different movement system, adapt source sampling in this component while keeping UI pose ownership and cleanup intact.
