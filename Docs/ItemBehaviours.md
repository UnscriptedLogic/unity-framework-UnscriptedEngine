# Equipped item behaviours

For inventory selection, held visuals, input forwarding, and selected-item dropping, see [ItemEquipComponent](ItemEquipComponent.md). This guide focuses on behaviour authoring and behaviour-session lifetime.

## Hand offset presets

Expand the item's equip fragment and use **Anchor pose > Presets**, or right-click
Anchor Position/Anchor Rotation and open **Anchor Pose**. Set the desired position and
rotation once, then choose **Save Current Pose as Left Hand** or **Right Hand**. Other
items can use **Apply Left Hand** or **Apply Right Hand** to copy both values, with Undo
support. Values remain editable and are not linked to later preset changes.

Presets start unset because offsets depend on your equip anchor and artwork. They are
stored per project in `ProjectSettings/UnscriptedEngineItemPoses.asset`; commit that file
to share presets with your team. Saving again replaces that hand's preset. These are
local offsets relative to the current equip anchor; they do not select a hand socket.

## Behaviour authoring

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
authority using context.Equipment.IsServer/IsOwner. Behaviours can opt into server use
and state replication as described below; spawned UInventoryComponent contents replicate separately.

Pawn/input adapters call `equipment.Use("primary", pressed)` (or another game-defined
action name). A true result means the behaviour handled the input. Call CancelUse on
focus loss or unpossession; do not turn cancelled input into a release that fires an
action. Core has no keyboard binding or Input System dependency.

## Replicated behaviour sessions

Override `ReplicatesState => true`, `HandlesAction(string)`, `CaptureState()`, and
`ApplyState(string)` to opt in. `HandlesAction` must be a side-effect-free check so
the owning client can consume input without executing gameplay. `Use` then executes
only on the server. Existing behaviours default to local use for compatibility.

`CaptureState` returns a compact string (up to 4093 UTF-8 bytes); JSON is suitable for
structured state. The server captures after use, cancellation, refresh, and shared
Tick, so changes from Tick/FixedTick also reach clients. Netcode sends changed
snapshots. `ApplyState` updates presentation on clients, including late joiners.
Server behaviours must update their own presentation for a host. Shared Tick and
FixedTick still run on every peer; explicitly guard authoritative simulation with
`context.Equipment.IsServer`. Never mutate state inside `CaptureState`.

Use requests carry slot, item ID, and server equipment-session ID. The server checks
equipment/inventory ownership, gameplay readiness, current item/session, and supported
action. Observers cannot use another player's equipment. Client `Use` returning true
means input consumed, not server acceptance. Up to 16 input edges are queued while a
predicted selection waits for its new server session; cancellation, unequip, or another
selection discards unsent input. Stale requests are rejected by the server.
`CancelUse` forwards cancellation once for pending owner use and cancels local input.

Held prefabs are instantiated locally from replicated inventory and selection; no
NetworkObject is required on these visuals. Register item definitions in the item
catalog. The owner uses the authored equip anchor; non-owners use a separate anchor
under the active network root, so disabling an owner-only camera rig cannot hide
their held item. Owner anchor position/rotation replicate relative to the pawn root,
including the current pose for late joiners. Item fragment offsets apply on every
peer. Pawn movement still needs its normal transform replication. Equipment does not replicate arbitrary
MonoBehaviour fields or execute arbitrary code remotely.

The default project pawn routes configured `PlayerActionNames.primaryUse` (default
`Player/Attack`, already bound to left mouse button) to `Use("primary", pressed)` at
priority 10, consuming handled events before lower-priority gameplay subscribers.
Blocked input, cursor release, and unpossession cancel held use.

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
    private bool isOn;
    private bool primaryHeld;

    public override bool ReplicatesState => true;
    public override bool HandlesAction(string action) => action == "primary";
    public override string CaptureState() => isOn ? "1" : "0";
    public override void ApplyState(string state)
    {
        isOn = state == "1";
        if (beam != null) beam.enabled = isOn;
    }

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
        if (!HandlesAction(action) || beam == null) return false;
        if (pressed && !primaryHeld) beam.enabled = isOn = !isOn;
        primaryHeld = pressed;
        return true;
    }

    public override void CancelUse() => primaryHeld = false;

    public override void Unequip()
    {
        if (beam != null) beam.enabled = false;
    }
}
```

Assign the held prefab in UItemFragEquip, attach FlashlightVisual and assign its Light,
then add FlashlightFragment. Primary press toggles server state; release is consumed
without toggling. Each client applies the replicated light state.

## State lifetime

A session belongs to inventory + selected slot + definition. Moving between slots with
the same definition creates a new session. Quantity changes preserve the session until
the stack becomes empty. Context captures the slot index, not an InventorySlot snapshot.

Ammo/durability and cooldowns that must survive unequipping need persistent item-instance
data. Current inventory stores only definition and quantity. Do not mutate shared
fragments to store per-item state. Equipped behaviour is not a save-game object.
