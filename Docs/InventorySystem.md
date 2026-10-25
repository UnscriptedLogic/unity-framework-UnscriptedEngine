# Inventory System

`UInventoryComponent` implements `IInventory` and stores item definitions and quantities in sparse, zero-based slots. Item definitions are `ItemDataSO` assets; each `InventorySlot` is an immutable snapshot. Spawned network inventories replicate from server to clients. Save-game persistence remains game-specific.

## 🧭 Purpose and design

`ItemDataSO` holds shared item metadata and `UItemFragment` authoring data. `UInventoryComponent` owns runtime slot contents. `UItemFragStack` sets per-slot capacity; without it, capacity is one. `UItemFragForceSingular` keeps a definition in one slot. Equip, behaviour, and drop fragments connect a definition to equipment and world-item systems.

This separates shared definition data from mutable quantities and gives UI and gameplay one checked API for inventory changes.

## 🚀 Getting started

1. Create an `ItemDataSO` asset and add any needed fragments.
2. Add `UInventoryComponent` to the owning object and set its slot count.
3. Add items through `TryAddItem`; inspect the returned `OperationResult`.

```csharp
using UnityEngine;
using UnscriptedEngine;

public sealed class InventoryExample : MonoBehaviour
{
    [SerializeField] private UInventoryComponent inventory;
    [SerializeField] private ItemDataSO itemData;

    public void AddFive()
    {
        var result = inventory.TryAddItem(itemData, quantity: 5);
        if (!result.Success) Debug.LogWarning(result.Message);
    }
}
```

`UInventoryComponent` initializes automatically at `BeginPlay`. Call `Initialize()` first if inventory operations must work before then. Initialization is idempotent and raises change notifications only on its first call.

## Using inventory

- `Items` exposes a read-only dictionary keyed by slot index. Missing keys are empty slots. Each `InventorySlot` contains `Item` and `Quantity`; operations replace snapshots instead of mutating them.
- `TryAddItem` adds the complete requested amount or changes nothing. It fills existing stacks first, then uses the first available slots.
- `CanAddItem` checks the same capacity rules without changing slots or firing events.
- `TryRemoveItem(slotIndex, quantity = 1)` removes the requested amount and frees the slot when empty.
- `TrySplitStack(sourceSlotIndex, quantity)` moves part of a stack to an empty slot. It cannot move the whole stack, exceed stack capacity, or split a singular item.
- `HasItem` and `HasEmptySlot` provide presence checks. `InventorySize` is the configured slot count.

`OperationResult` contains `Success`, `SlotIndex`, `Quantity`, and `Message`. For add/remove, `SlotIndex` is the first affected slot; for split, it is the new destination slot. Failed operations leave contents unchanged.

Subscribe to `Changed` for any content change. `TryAddItem` also publishes `UInventoryComponent.EB_InventoryChanged` and one `UInventoryComponent.EB_InventoryAddedItem` per affected slot. The added event's `Quantity` is the amount added to that slot, not its resulting total.

```csharp
inventory.Changed += RefreshInventoryView;
// Later, when the view is disabled or destroyed:
inventory.Changed -= RefreshInventoryView;
```

Changes commit before notifications. Mutations attempted from a change callback are rejected. Subscriber exceptions are logged and do not prevent later subscribers; update views from committed `Items` state.

## 🌍 Dropping and picking up items

Add `UItemFragDrop` to an item's `ItemDataSO` and assign its `WorldPrefab`. The prefab must contain `UWorldItem`; that component requires `NetworkObject` and `UInteractableComponent`. `UWorldItem.Initialize(item, amount)` sets pickup contents before spawn.

```csharp
using UnityEngine;
using UnscriptedEngine;
using UnscriptedEngine.Item;

public sealed class InventoryDropExample : MonoBehaviour
{
    [SerializeField] private UInventoryComponent inventory;
    [SerializeField] private Transform origin;

    public bool Drop(int slotIndex, Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.0001f) return false;
        var result = inventory.TryDropItem(slotIndex, origin.position,
            Quaternion.LookRotation(direction), out UWorldItem pickup);
        if (!result.Success) Debug.LogWarning(result.Message);
        return result.Success;
    }
}
```

`TryDropItem` spawns and initializes the pickup before removing inventory quantity. It is server/offline only. A missing prefab, invalid slot or quantity, spawn failure, or direct client call returns failure and leaves inventory unchanged. For a remote owner request, use `ItemEquipComponent.Drop`; see [ItemEquipComponent](ItemEquipComponent.md). Register pickup prefabs with the `NetworkManager` when using Netcode. Offline, the pickup is instantiated without network spawning.

`UWorldItem` defaults to `WorldItemInteraction.PickUp`. Its item ID and quantity replicate with a spawned network pickup. Client interactions go through `UInteractableComponent`; the server adds the full quantity to a `UInventoryComponent` on the interactor or a child, then despawns the pickup. Missing or full inventories leave it in the world. Offline pickups are destroyed after a successful transfer. Set behaviour to `None` for a pickup with no built-in collection action.

`ItemEquipComponent.Drop(direction, quantity)` wraps the selected-slot flow, applies configured drop origin and rigidbody speed, and requires a concrete `UInventoryComponent`. See [ItemEquipComponent](ItemEquipComponent.md) for hotbar and input integration.

## Multiplayer and permissions

- A spawned `UInventoryComponent` replicates capacity and slot snapshots (slot index, quantity, and stable item ID) to clients, including late joiners. Spawned inventories mutate on the server only. During a listening network session, unspawned inventories also reject mutations.
- `ItemEquipComponent.Drop` supplies the built-in owning-client drop request. Generic client requests for add, remove, and split are not included.
- The package generates `Assets/Resources/UnscriptedItemCatalog.asset` from `ItemDataSO` assets. Item IDs use asset GUIDs. The catalog refreshes on asset changes, before Play Mode, and before builds. Keep matching item assets/catalogs on every peer; rebuild via **Unscripted Engine > Items > Rebuild Network Catalog** if an item cannot resolve.
- The inventory's `DropsAllowed` rule defaults to `true`. Change it with `SetDropsAllowed(bool)` on the server; spawned inventories replicate the rule to clients. Direct `TryDropItem` remains server/offline only.
- Network pickup prefabs still need registration with the `NetworkManager`. Inventory save-game persistence and client requests for add/remove/split remain game responsibilities.

## ⚠️ Limitations and notes

- Scene-placed pickups require their scene in the shared build scene list when Netcode scene management is enabled. Editor scene authoring marks placed `NetworkObject`s before Play Mode and scene saves, preventing them from being treated as dynamic prefab spawns.
- A drop fragment must reference a valid world pickup prefab. A missing fragment or prefab makes drop fail without consuming the item.
- Inventory callbacks cannot mutate the same inventory during notifications. Queue follow-up changes and apply them after notification completes.
- `SetLoggingEnabled(false)` disables the component's operation log output.
- This API uses `ItemDataSO`, `InventorySlot`, `IInventory`, and `UInventoryComponent`; older names such as `ItemStack`, `InventoryContainer`, and `ItemWorldEntity` are not part of this implementation.

## Changelog

- 2026-10-25: Documented current inventory API, pickup/drop flow, and authority limits.
