# Inventory system

`UInventoryComponent` implements `IInventory` and stores `ItemDataSO` plus quantity in zero-based sparse slots. Operations validate capacity and report success through `UInventoryComponent.OperationResult`.

For setup, APIs, events, and world pickup/drop behavior, see [Inventory System](../../Docs/InventorySystem.md). For hotbar selection, held visuals, item behaviours, and selected-item dropping, see [ItemEquipComponent](../../Docs/ItemEquipComponent.md).

Spawned `UInventoryComponent` instances replicate slot snapshots and drop permission from server to clients. Save-game persistence and client requests for add/remove/split remain project responsibilities. Networked `UWorldItem` pickups replicate item ID and quantity; register pickup prefabs with the `NetworkManager` and keep generated item catalogs aligned across peers.
