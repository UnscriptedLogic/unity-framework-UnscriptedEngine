# Inventory foundation

`ItemStack` is the canonical transferable runtime value. `ItemDefinition` contains static authoring data; `InventoryContainer` owns slots; `InventoryComponent` exposes the container and slot-change events; `ItemWorldEntity` represents a dropped stack; and `PlacedItem` represents a placed item.

Recommended flow: remove a stack, spawn its dropped prefab, and call `Initialize`. For placement, consume one stack, spawn the placed prefab, and call `PlacedItem.Initialize`. Breaking a placed object calls `CreatePickupStack` and adds the result to an inventory.

For Netcode, replicate a stable `ItemDefinition.ItemId` plus stack state rather than a Unity object reference. Resolve IDs through a project-level item database on each peer; the database and addressable strategy are intentionally project-specific.
