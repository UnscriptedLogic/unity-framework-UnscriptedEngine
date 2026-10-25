# ItemEquipComponent

`ItemEquipComponent` observes an `IInventory`, selects a hotbar slot, creates held-item visuals, and runs one `ItemBehaviour` session for the selected item. It lives in `Framework/Components/ItemEquipComponent.cs`; input mapping and behaviour-action authority belong to the caller. Selected-item dropping has a built-in owner-to-server request.

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
| `Use(action, pressed)` | Forwards a press/release to current behaviour when gameplay is ready. |
| `CancelUse()` | Cancels charging/held input without committing an action. Call on focus loss or unpossession. |
| `Drop(direction, quantity = 1)` | Drops on server/offline; on an owning client, queues a validated server request. Client `true` means queued, not accepted. |

`UItemFragEquip` supplies an optional visual prefab and local position/rotation offsets. Without a visual prefab, a behaviour can still run. The first `UItemFragBehaviour` on the definition creates a fresh behaviour for each equipment session. See [Equipped item behaviours](ItemBehaviours.md) for behaviour authoring and cleanup.

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

## Changelog

- 2026-10-25: Documented selection, held-item sessions, drop API, and Q input paths.
