# Tagged value UI

Create controls with **Tools > UnscriptedEngine > UI > Tagged Value**: Slider, Text,
Filled Image, or Event Binding. Select an exact gameplay tag on `TaggedFloatBinding`.
Put the same tag on an actor's `TaggedFloatValue`. Search includes actor root and
inactive children. Duplicate matches warn and clear the binding.

Within `UCanvasComponent`, bindings automatically follow `OwningController.Pawn`,
including replacement pawns. For arbitrary actors, call `binding.SetSource(actor)`.
For an entire UI prefab, add `TaggedValueContext` to its root:

```csharp
GameObject instance = Instantiate(uiPrefab, uiParent);
instance.GetComponent<TaggedValueContext>().SetSource(gameObject);
```

Child bindings notice context/pawn changes in LateUpdate. Explicit per-binding
sources take precedence over contexts. A context with no source clears its UI.
Call `Rebind()` after adding/removing values or changing tags on the same actor;
call `Refresh()` after changing bounds without a value change.

`TaggedFloatDisplay` supports any combination of Slider, filled Image, and TMP
text. Slider range is normalized 0–1; text uses raw value. Display adapters never
write back to gameplay values. Keep informational sliders non-interactive.
Text placeholders: `{0}` value, `{1}` minimum, `{2}` maximum, `{3}` normalized.
Unclamped values still use configured minimum/maximum for normalization.

For effects, use `TaggedFloatBinding` alone. Read `Value`, `NormalizedValue`,
`ResponseValue`, and `IsBound`; subscribe to `Changed` or Inspector float events.
Subscribe on enable and unsubscribe on disable. Subclasses can override `Publish`
(call `base.Publish()`) for custom behavior.

The response curve defaults to 1 at empty health, 0 at full health. Connect
**On Response Changed** to your vignette component's public `SetIntensity(float)`
method, or another float property. Edit curve for threshold/ramp behavior.
No render-pipeline dependency is imposed. Missing sources output zero, including
effect response, so a missing/dead actor does not leave an effect stuck on screen.

Bindings scan only on source changes or explicit rebind. Each LateUpdate compares
cached source references; value updates are event-driven, including replicated
network updates. Disable releases subscriptions; no static runtime state is used.

## Reactive animations

Select a UI element and use **Tools > UnscriptedEngine > UI > Reactive Animations**
to add Hide or Shake. Both find a `TaggedFloatBinding` on themselves or parents;
assign Binding explicitly to animate a different UI element. Multiple effects can
share one binding. Percentages are relative to the source's minimum/maximum range.

- **Minimum, 25%**: threshold reached at or below 25%. Strength increases from zero
  at 25% to one at minimum.
- **Maximum, 75%**: threshold reached at or above 75%. Strength increases from zero
  at 75% to one at maximum. Endpoint thresholds (0% minimum, 100% maximum) produce
  full strength only at that endpoint.

`UIAnimReactive_Hide` fades its CanvasGroup out when the threshold is reached.
Disable **Hide When Reached** to fade in instead. It keeps its GameObject active,
allowing value events to show it again. Fade Duration zero snaps immediately.
Hidden UI stops intercepting raycasts; disabling the effect restores original
alpha and input settings. Missing bindings count as threshold not reached.

`UIAnimReactive_Shake` applies smooth noise with progressive amplitude, controlled
by an intensity curve. Put it on a visual child beneath layout-controlled objects;
layout or Animator components must not overwrite that child's anchored position.
It removes its previous offset before applying another and restores position on
disable or when strength reaches zero. Selected gizmo shows maximum displacement.
Both effects default to unscaled time and clean up subscriptions on disable.
