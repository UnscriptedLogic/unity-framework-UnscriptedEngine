# Equipped item behaviours

Add one concrete `UItemFragBehaviour` subclass to `ItemDataSO.fragments` using the
existing fragment Inspector. Its `CreateBehaviour(ItemBehaviourContext)` must return
a fresh plain C# `ItemBehaviour`. The first behaviour fragment is used.

Keep shared gameplay settings on the fragment; keep prefab references such as muzzle,
Animator, particles, or a Light on a game-specific MonoBehaviour on the held visual.
Resolve that component once through `context.GetVisualComponent<T>()`. No required
base visual component or string-based binding registry is needed.

`ItemEquipComponent` owns the session: create visual, create behaviour, Equip, then
forward Tick/FixedTick. Cleanup calls CancelUse, Unequip, and Dispose before destroying
the visual. Each cleanup hook is attempted even if another throws. Disable, despawn,
inventory replacement, selection change, and removal of the selected stack clean up.
Re-enable starts a new session. Items without visuals can also have behaviours;
existing visual-only items need no changes.

Equip may run before network spawn, matching existing held-visual setup. Use it for
binding/presentation only. Tick, FixedTick and Use follow UObjectComponent gameplay
readiness. Shared ticks run on server and clients: game-specific behaviour must choose
authority using context.Equipment.IsServer/IsOwner. This API does not replicate actions
or inventory and does not provide server validation.

Pawn/input adapters call `equipment.Use("primary", pressed)` (or another game-defined
action name). A true result means the behaviour handled the input. Call CancelUse on
focus loss or unpossession; do not turn cancelled input into a release that fires an
action. Core has no keyboard binding or Input System dependency.

Equipment callbacks must not switch equipment or replace its inventory. Refresh-time
SelectSlot returns false; SetInventory throws. Inventory Changed callbacks also prohibit
inventory mutations. Consume items through a game-supplied command on normal use input,
check the operation result, and remember consuming the last item may synchronously
dispose the behaviour before that Use call returns.

## Example: flashlight

Place the public visual component in FlashlightVisual.cs in your game assembly:

```csharp
using UnityEngine;

public sealed class FlashlightVisual : MonoBehaviour
{
    [field: SerializeField] public Light Beam { get; private set; }
}
```

Add the fragment and behaviour to your game assembly:

```csharp
using System;
using UnityEngine;
using UnscriptedEngine;
using UnscriptedEngine.Item;

[Serializable]
public sealed class FlashlightFragment : UItemFragBehaviour
{
    [SerializeField, Min(0)] private float intensity = 2;

    public override ItemBehaviour CreateBehaviour(ItemBehaviourContext context)
    {
        var visual = context.GetVisualComponent<FlashlightVisual>();
        if (visual == null || visual.Beam == null)
            throw new InvalidOperationException("Flashlight visual requires a Light.");
        return new FlashlightBehaviour(visual.Beam, intensity);
    }
}

public sealed class FlashlightBehaviour : ItemBehaviour
{
    private readonly Light beam;
    private readonly float intensity;

    public FlashlightBehaviour(Light beam, float intensity)
    {
        this.beam = beam;
        this.intensity = intensity;
    }

    public override void Equip()
    {
        beam.intensity = intensity;
        beam.enabled = false;
    }

    public override bool Use(string action, bool pressed)
    {
        if (action != "primary" || beam == null) return false;
        if (pressed) beam.enabled = !beam.enabled;
        return true;
    }

    public override void Unequip()
    {
        if (beam != null) beam.enabled = false;
    }
}
```

Assign the held prefab in UItemFragEquip, attach FlashlightVisual and assign its Light,
then add FlashlightFragment. This example toggles local presentation; multiplayer
synchronization remains game policy.

## State lifetime

A session belongs to inventory + selected slot + definition. Moving between slots with
the same definition creates a new session. Quantity changes preserve the session until
the stack becomes empty. Context captures the slot index, not an InventorySlot snapshot.

Ammo/durability and cooldowns that must survive unequipping need persistent item-instance
data. Current inventory stores only definition and quantity. Do not mutate shared
fragments to store per-item state. Equipped behaviour is not a save-game object.
