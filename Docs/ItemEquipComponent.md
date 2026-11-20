# ItemEquipComponent

`ItemEquipComponent` observes an `IInventory`, selects a hotbar slot, creates held-item visuals, and runs one `ItemBehaviour` session for the selected item. It lives in `Framework/Components/Equipment/ItemEquipComponent.cs`; input mapping and behaviour-action authority belong to the caller. Selected-item dropping has a built-in owner-to-server request.

## 🧭 Purpose and workflow

The inventory stores item definitions and quantities. The equipment component turns the selected slot into a temporary held visual and behaviour. Slot selection or inventory changes refresh that session; the component cleans it up when the item changes or the component is disabled, despawned, or destroyed.

## 🚀 Getting started

1. Add `ItemEquipComponent` to the pawn or owning object.
2. Assign an inventory component that implements `IInventory`, or call `SetInventory` at runtime.
3. Set `hotbarSize` and, optionally, `equipAnchor`.
4. Add `UItemFragEquip` to an item's `ItemDataSO` when it needs a held visual. Add a `UItemFragBehaviour` subclass for active behaviour.
5. Connect game input to `SelectSlot`, `Use`, and `CancelUse`.

```csharp
using UnityEngine;

public sealed class EquipmentInputAdapter : MonoBehaviour
{
    [SerializeField] private ItemEquipComponent equipment;

    public bool HandlePrimary(bool pressed)
        => equipment.Use("primary", pressed);

    public void OnInputCancelled()
        => equipment.CancelUse();
}
```

`Use` returns `true` when the current behaviour handles the action. Action names are game-defined strings; this component does not bind keys or replicate actions.

Forward normal releases with `pressed: false`. Call `CancelUse` on focus loss or unpossession; do not turn cancelled input into a release that could commit an action.

## Using the component

| API / property | Behavior |
| --- | --- |
| `Inventory` | Current observed `IInventory`. |
| `VisibleSlotCount` | Smaller of `hotbarSize` and inventory capacity. |
| `SelectedSlotIndex` | Selected zero-based slot; starts at `-1`. |
| `HeldItem`, `HeldVisual` | Current definition and instantiated visual, if any. |
| `SelectSlot(index)` | Selects a visible slot and refreshes held state; returns `false` for invalid or unavailable slots. |
| `SetInventory(inventory)` | Replaces observed inventory and clears selection. |
| `Use(action, pressed)` | Forwards local use, or queues server-validated use for behaviours opting into replicated state. Client true means consumed, not server acceptance. |
| `CancelUse()` | Cancels charging/held input without committing an action. Call on focus loss or unpossession. |
| `Drop(direction, quantity = 1)` | Drops on server/offline; on an owning client, queues a validated server request. Client `true` means queued, not accepted. |

`UItemFragEquip` supplies an optional visual prefab and local position/rotation offsets. Without a visual prefab, a behaviour can still run. The first `UItemFragBehaviour` on the definition creates a fresh behaviour for each equipment session. See [Equipped item behaviours](ItemBehaviours.md) for behaviour authoring and cleanup.

## Held items on remote players

Each peer reconstructs the held prefab from replicated inventory/selection. The owner
parents its visual to `equipAnchor`. Non-owners (including the host observing a remote
player) parent theirs to an automatically created anchor beneath the active pawn's
NetworkObject. This keeps held meshes and lights visible when `DisableOnProxy` turns
off the first-person camera hierarchy containing the authored anchor.

The owner replicates anchor position and rotation relative to the pawn root. Proxy
anchors apply that pose plus the item's authored offsets, so aiming moves the remote
flashlight beam with the owner's hand. Late joiners receive the current anchor pose
and behaviour state. No extra held-item NetworkObject or prefab registration is
needed. The pawn's normal movement replication still moves its root.

Authority over use/state remains on the server; the owner-written anchor pose is
presentation only. Behaviour replication is opt-in as described in ItemBehaviours.

## Dropping selected items

Add `UItemFragDrop` to the item's `ItemDataSO` and assign its `UWorldItem` prefab. `Drop` uses `dropOrigin` when assigned, otherwise `dropOffset` relative to the equipment transform. It normalizes direction, faces the pickup along that direction, and gives a pickup with a `Rigidbody` gravity and velocity `direction * dropSpeed`.

`Drop` requires an active component, a selected item, a finite nonzero direction, an allowed drop rule, and a `UInventoryComponent`. Held-item observation supports any `IInventory`, but dropping needs the concrete component. In a live Netcode session, `UInventoryComponent.TryDropItem` is server-only. An owner of spawned equipment and inventory can call `Drop`; the component sends a request and returns `true` when it is queued. The server checks sender ownership, inventory ownership, slot, and expected item ID before spawning. Rejection is asynchronous and has no result callback, so a queued client call is not confirmation that item was dropped.

`UInventoryComponent.DropsAllowed` gates drops; it defaults to `true`. Set a server-controlled rule with `SetDropsAllowed(bool)`. The value replicates for spawned inventories.

## Q input paths

The default `DefaultPlayerPawn` handles the configured throw action on Q press. It requires the local owning pawn, unblocked gameplay input, a locked cursor, and active equipment; it calls `ItemEquipComponent.Drop(ViewForward)` and consumes the input. Owning remote clients use the component's server request. Because the request is asynchronous, input consumption does not indicate server acceptance.

The optional project-level `ThrowItemInInventoryComponent` has higher input priority. When it handles Q press, it consumes the event before the default pawn handler. It preserves hold-to-aim behavior: quick Q release still drops one item; holding past `aimHoldDelay` enters third-person aim when the pawn is not already first-person, then Q release drops one item. Escape or right-click press cancels the pending drop. If this adapter is attached and has a held item, it owns that Q flow for the pawn.

The selected item must have a configured `UItemFragDrop` world prefab, registered with the `NetworkManager` for Netcode spawning. Generic inventory operations still have no client request API; implement server gameplay requests for add, remove, and split actions. The equipment drop request does not report server acceptance back to the client.

## 🧩 Extending and limitations

- Keep selection and input policy in the pawn/game adapter. `EquipmentSelection.SelectNumber` and `EquipmentSelection.Scroll` are optional helpers for common hotbar navigation.
- Equipped behaviour is session state, not persistent item-instance state. Changing to another slot with the same definition starts a new session; quantity changes preserve the session until the selected stack becomes empty.
- Cleanup calls `CancelUse`, `Unequip`, and `Dispose` before destroying the held visual. Each cleanup callback is attempted even if another throws.
- Do not switch equipment or replace its inventory from equipment callbacks. Refresh-time selection is rejected; `SetInventory` throws during refresh/cleanup.
- Inventory slot replication and equipment drop requests are built in for spawned network objects. Save-game persistence and client requests for other inventory mutations remain game responsibilities.
- Replicated behaviours opt in through `ReplicatesState`, `HandlesAction`, `CaptureState`, and `ApplyState`. Snapshots include item, slot, and session identity, support late joiners, and reset when unequipped. See [Equipped item behaviours](ItemBehaviours.md).

## Changelog

- 2026-10-25: Documented selection, held-item sessions, drop API, and Q input paths.

## Multiple hands on the sandbox pawn

Each hand is an Item Equip Component. Place the components together on the same
GameObject, assign the same inventory and give each hand a distinct Equip Anchor.
Enable Start Empty on additional hands and add them in order to Additional Hands
on Default Player Pawn. Keep the main hand first among the equipment components.
The DefaultPawn prefab is wired with two hands. Add more by repeating the
additional-hand setup. The existing hotbar uses H1, H2, and so on to show assignments.

| Intent | Mouse and keyboard | Gamepad |
| --- | --- | --- |
| Use preferred hand, fall back to the other | Left mouse | RT or the existing Attack binding |
| Prefer off hand while held | Left Alt | |
| Move or swap the main and off-hand items | F | |
| Use off hand when aim is unconsumed | Right mouse | |
| Use off hand directly | | LT |
| Cycle focused hand | H | D-pad down |
| Use focused hand | X | RB |
| Select a slot for the focused hand | Number keys or wheel | D-pad left or right |
| Clear the focused hand | Backspace | |
| Drop from target hand | Existing drop binding | Existing drop binding |

Equip slot 1 and press F to move it to the off hand. Equip slot 2 and press F
again to swap the two assignments. Left-click tries the main hand first. Hold
Left Alt when pressing to try the off hand first. The EventBus only tries the
other hand if the preferred item leaves the event unconsumed. Releases go to
the hand and session that accepted the press. Right-click tries the off hand
after other aim handlers. Third-person aiming and throw preparation consume it.

Use `ItemBehaviour.CanStartUse` to decline a fresh press when the item cannot
act. Keep that check side-effect-free. Replicated behaviors check it on the owner
and again on the server. Sandbox weapons decline when their inventory ammo is
insufficient. A weapon with magazine and reload state can check that state here.

In the inventory grid, left-click a hotbar item to equip it to the focused hand
or right-click to equip it to the off hand.

Each inventory slot can be assigned to one hand. For two copies of an item,
put the copies in separate slots. A hand's assignment follows its inventory slot.
The off hand has **Mirror Held Pose** enabled. Give it a neutral anchor and
the item will reflect its authored position and rotation across the anchor's local
YZ plane. For a custom grip, enable **Override Left Hand Pose** in the Equip
fragment. You can also assign a **Left Hand Visual Prefab** with the components
required by the item's behavior.

To mirror a model, set **Left Hand Mirror Child Path** to a mesh-only child
such as `Visuals/Mesh`. The child is flipped on its local X axis. Keep text and
directional attachments such as beams or muzzle transforms outside that child.
A centered pose needs an explicit left-hand position if it should sit to the side.
