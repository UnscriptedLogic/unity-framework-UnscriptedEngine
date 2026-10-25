using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;
using Unity.Netcode;
using UnscriptedEngine.Item;
using Unity.Collections;

namespace UnscriptedEngine
{
    public class UInventoryComponent : UObjectComponent, IInventory
    {
        [SerializeField, Min(0), Tooltip("Number of zero-based slots available in this inventory.")]
        private int inventorySize = 10;
        private Dictionary<int, InventorySlot> slots;
        private bool loggingEnabled = true;
        private bool publishingChanges;
        [SerializeField, Tooltip("Server gameplay rule. Clients may request drops by default.")]
        private bool itemDroppingAllowed = true;
        private readonly NetworkVariable<InventorySnapshot> replicatedInventory = new();
        private readonly NetworkVariable<bool> dropsAllowed = new(true);

        public bool DropsAllowed => IsSpawned ? dropsAllowed.Value : itemDroppingAllowed;
        public void SetDropsAllowed(bool allowed)
        {
            if (IsSpawned && !IsServer) throw new InvalidOperationException("Only the server can change drop permissions.");
            itemDroppingAllowed = allowed;
            if (IsSpawned) dropsAllowed.Value = allowed;
        }
        private bool CanMutate => IsSpawned ? IsServer :
            !(Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening);

        protected override void OnSpawned()
        {
            base.OnSpawned();
            Initialize();
            replicatedInventory.OnValueChanged += ApplyReplicatedInventory;
            if (IsServer) { dropsAllowed.Value = itemDroppingAllowed; ReplicateInventory(); }
            else ApplyReplicatedInventory(default, replicatedInventory.Value);
        }

        protected override void OnDespawned()
        {
            replicatedInventory.OnValueChanged -= ApplyReplicatedInventory;
            base.OnDespawned();
        }

        private void ReplicateInventory()
        {
            if (!IsSpawned || !IsServer || slots == null) return;
            var entries = new InventoryEntry[slots.Count];
            int index = 0;
            foreach (var pair in slots)
                entries[index++] = new InventoryEntry
                {
                    Slot = pair.Key, Quantity = pair.Value.Quantity, ItemId = pair.Value.Item.NetworkId
                };
            replicatedInventory.Value = new InventorySnapshot { Capacity = inventorySize, Entries = entries };
        }

        private void ApplyReplicatedInventory(InventorySnapshot previous, InventorySnapshot current)
        {
            if (IsServer) return;
            Initialize();
            var replacement = new Dictionary<int, InventorySlot>();
            foreach (var entry in current.Entries ?? Array.Empty<InventoryEntry>())
            {
                var item = ItemNetworkCatalog.Resolve(entry.ItemId.ToString());
                if (item == null) { Debug.LogError($"Unknown network item '{entry.ItemId}'. Rebuild the item catalog.", this); return; }
                replacement.Add(entry.Slot, new InventorySlot(item, entry.Quantity));
            }
            slots.Clear();
            foreach (var pair in replacement) slots.Add(pair.Key, pair.Value);
            inventorySize = current.Capacity;
            PublishChanged();
        }

        public struct InventoryEntry : INetworkSerializable, IEquatable<InventoryEntry>
        {
            public int Slot, Quantity;
            public FixedString64Bytes ItemId;
            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref Slot);
                serializer.SerializeValue(ref Quantity);
                serializer.SerializeValue(ref ItemId);
            }
            public bool Equals(InventoryEntry other) => Slot == other.Slot && Quantity == other.Quantity && ItemId.Equals(other.ItemId);
        }

        public struct InventorySnapshot : INetworkSerializable, IEquatable<InventorySnapshot>
        {
            public int Capacity;
            public InventoryEntry[] Entries;
            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref Capacity);
                int count = Entries?.Length ?? 0;
                serializer.SerializeValue(ref count);
                if (count < 0 || count > 65536) throw new InvalidOperationException("Invalid inventory snapshot size.");
                if (serializer.IsReader) Entries = new InventoryEntry[count];
                for (int i = 0; i < count; i++) serializer.SerializeValue(ref Entries[i]);
            }
            public bool Equals(InventorySnapshot other)
            {
                if (Capacity != other.Capacity || (Entries?.Length ?? 0) != (other.Entries?.Length ?? 0)) return false;
                for (int i = 0; i < (Entries?.Length ?? 0); i++) if (!Entries[i].Equals(other.Entries[i])) return false;
                return true;
            }
        }

        public int InventorySize => inventorySize;
        public event Action Changed;
        public IReadOnlyDictionary<int, InventorySlot> Items { get; private set; }

        protected override void BeginPlay()
        {
            base.BeginPlay();
            Initialize();
        }

        public void Initialize()
        {
            if (slots != null) return;
            slots = new Dictionary<int, InventorySlot>();
            Items = new ReadOnlyDictionary<int, InventorySlot>(slots);
            PublishChanged();
        }

        // Adds the entire quantity or leaves the inventory unchanged.
        public OperationResult TryAddItem(ItemDataSO itemDataSo, int quantity = 1)
        {
            if (!CanMutate) return Failure("Inventory changes must be requested through server gameplay actions");
            if (IsSpawned && itemDataSo != null && ItemNetworkCatalog.Resolve(itemDataSo.NetworkId) != itemDataSo)
                return Failure("Item is missing from the network catalog");
            if (publishingChanges) return Failure("Inventory cannot be mutated during change notifications");
            var result = PlanAddItem(itemDataSo, quantity, out var additions);
            if (result.Success)
            {
                foreach (var addition in additions)
                {
                    int existingQuantity = slots.TryGetValue(addition.Key, out var slot) ? slot.Quantity : 0;
                    slots[addition.Key] = new InventorySlot(itemDataSo, existingQuantity + addition.Value);
                }

                result.Message = "Item added successfully";
                PublishChanged(() =>
                {
                    foreach (var addition in additions)
                    {
                        EventBus.PublishNotification(new EB_InventoryAddedItem
                        {
                            Inventory = this,
                            ItemAdded = itemDataSo,
                            SlotIndex = addition.Key,
                            Quantity = addition.Value
                        });
                    }
                });
            }

            Log(result.Message);
            return result;
        }

        // Checks availability without modifying the inventory or publishing events.
        public OperationResult CanAddItem(ItemDataSO itemDataSo, int quantity = 1)
        {
            return PlanAddItem(itemDataSo, quantity, out _, false);
        }

        private OperationResult PlanAddItem(ItemDataSO item, int quantity,
            out List<KeyValuePair<int, int>> additions, bool recordAdditions = true)
        {
            additions = null;
            if (slots == null) return Failure("Inventory is not initialized");
            if (item == null) return Failure("Item data is null");
            if (quantity <= 0) return Failure("Quantity must be greater than zero");

            bool forceSingular = IsForceSingular(item);
            if (forceSingular && HasItem(item, out int existingIndex))
                return Failure("Item already exists in inventory", existingIndex);

            int capacity = GetStackCapacity(item);
            if (forceSingular && quantity > capacity)
                return Failure("Quantity exceeds the single slot capacity");

            int remaining = quantity;
            int firstIndex = -1;
            if (recordAdditions) additions = new List<KeyValuePair<int, int>>();
            // Fill existing stacks before allocating new slots.
            for (int i = 0; i < inventorySize && remaining > 0; i++)
            {
                if (!slots.TryGetValue(i, out var slot) || slot.Item != item) continue;
                int amount = Math.Min(remaining, Math.Max(0, capacity - slot.Quantity));
                if (amount == 0) continue;
                if (firstIndex < 0) firstIndex = i;
                additions?.Add(new KeyValuePair<int, int>(i, amount));
                remaining -= amount;
            }

            for (int i = 0; i < inventorySize && remaining > 0; i++)
            {
                if (slots.ContainsKey(i)) continue;
                int amount = Math.Min(remaining, capacity);
                if (firstIndex < 0) firstIndex = i;
                additions?.Add(new KeyValuePair<int, int>(i, amount));
                remaining -= amount;
            }

            if (remaining > 0) return Failure("Not enough inventory capacity");
            return Success(firstIndex, quantity, "Item can be added");
        }

        // Removing the final item frees the slot. The default removes one item.
        public OperationResult TryRemoveItem(int slotIndex, int quantity = 1)
        {
            if (!CanMutate) return Failure("Inventory changes must be requested through server gameplay actions");
            if (publishingChanges) return Failure("Inventory cannot be mutated during change notifications");
            var result = ValidateOccupiedSlot(slotIndex, quantity);
            if (!result.Success) return result;

            var slot = slots[slotIndex];
            if (quantity == slot.Quantity)
                slots.Remove(slotIndex);
            else
                slots[slotIndex] = new InventorySlot(slot.Item, slot.Quantity - quantity);

            PublishChanged();
            return Success(slotIndex, quantity, "Items removed successfully");
        }

        /// <summary>Spawn a pickup and consume the quantity only after spawning succeeds. Server/offline only.</summary>
        public OperationResult TryDropItem(int slotIndex, Vector3 position, Quaternion rotation,
            out UWorldItem droppedItem, int quantity = 1)
        {
            droppedItem = null;
            if (!CanMutate) return Failure("Drop must run on the server through an equipment request", slotIndex);
            if (!DropsAllowed) return Failure("Item dropping is disabled", slotIndex);
            if (publishingChanges) return Failure("Inventory cannot be mutated during change notifications");
            var result = ValidateOccupiedSlot(slotIndex, quantity);
            if (!result.Success) return result;
            NetworkManager manager = IsSpawned ? NetworkManager : NetworkManager.Singleton;
            if (manager != null && manager.IsListening && !manager.IsServer)
                return Failure("Items can only be dropped by the server", slotIndex);
            ItemDataSO item = slots[slotIndex].Item;
            UWorldItem prefab = null;
            if (item.fragments != null)
                foreach (var fragment in item.fragments)
                    if (fragment is UItemFragDrop drop) { prefab = drop.WorldPrefab; break; }
            if (prefab == null) return Failure("Item has no world pickup prefab", slotIndex);
            UWorldItem instance = null;
            // Prefab lifecycle callbacks must not mutate the source inventory mid-transfer.
            publishingChanges = true;
            try
            {
                instance = Instantiate(prefab, position, rotation);
                instance.Initialize(item, quantity);
                if (manager != null && manager.IsListening)
                {
                    instance.NetworkObject.Spawn();
                    if (!instance.IsSpawned) throw new InvalidOperationException("Pickup failed to network spawn");
                }
            }
            catch (Exception exception)
            {
                if (instance != null)
                {
                    if (instance.IsSpawned) instance.NetworkObject.Despawn(true);
                    else
                    {
                        instance.gameObject.SetActive(false);
                        if (Application.isPlaying) Destroy(instance.gameObject);
                        else DestroyImmediate(instance.gameObject);
                    }
                }
                return Failure($"Could not spawn dropped item: {exception.Message}", slotIndex);
            }
            finally { publishingChanges = false; }
            result = TryRemoveItem(slotIndex, quantity);
            droppedItem = instance;
            return result;
        }

        // Moves part of a stack into a new slot without changing the total quantity.
        // SlotIndex in the result identifies the newly created stack.
        public OperationResult TrySplitStack(int sourceSlotIndex, int quantity)
        {
            if (!CanMutate) return Failure("Inventory changes must be requested through server gameplay actions");
            if (publishingChanges) return Failure("Inventory cannot be mutated during change notifications");
            var result = ValidateOccupiedSlot(sourceSlotIndex, quantity);
            if (!result.Success) return result;

            var source = slots[sourceSlotIndex];
            if (quantity == source.Quantity)
                return Failure("A split must leave items in the source slot", sourceSlotIndex);
            if (IsForceSingular(source.Item))
                return Failure("Item is restricted to one slot", sourceSlotIndex);
            if (quantity > GetStackCapacity(source.Item))
                return Failure("Quantity exceeds stack capacity", sourceSlotIndex);
            if (!HasEmptySlot(out int targetSlotIndex))
                return Failure("No empty inventory slot available");

            slots[sourceSlotIndex] = new InventorySlot(source.Item, source.Quantity - quantity);
            slots.Add(targetSlotIndex, new InventorySlot(source.Item, quantity));
            PublishChanged();
            return Success(targetSlotIndex, quantity, "Stack split successfully");
        }

        private OperationResult ValidateOccupiedSlot(int slotIndex, int quantity)
        {
            if (slots == null) return Failure("Inventory is not initialized");
            if (quantity <= 0) return Failure("Quantity must be greater than zero", slotIndex);
            if (slotIndex < 0 || slotIndex >= inventorySize)
                return Failure("Invalid slot index", slotIndex);
            if (!slots.TryGetValue(slotIndex, out var slot))
                return Failure("Inventory slot is empty", slotIndex);
            if (quantity > slot.Quantity)
                return Failure("Not enough items in the slot", slotIndex);
            return Success(slotIndex, quantity, "Operation can be performed");
        }

        private static int GetStackCapacity(ItemDataSO item)
        {
            if (item.fragments != null)
                foreach (var fragment in item.fragments)
                    if (fragment is UItemFragStack stack) return Math.Max(1, stack.StackSize);
            return 1;
        }

        private static bool IsForceSingular(ItemDataSO item)
        {
            return item.fragments != null
                && item.fragments.Exists(fragment => fragment is UItemFragForceSingular);
        }

        public bool HasItem(ItemDataSO itemDataSo) => HasItem(itemDataSo, out _);

        public bool HasItem(ItemDataSO itemDataSo, out int slotIndex)
        {
            if (itemDataSo != null && slots != null)
                for (int i = 0; i < inventorySize; i++)
                    if (slots.TryGetValue(i, out var slot) && slot.Item == itemDataSo)
                    {
                        slotIndex = i;
                        return true;
                    }

            slotIndex = -1;
            return false;
        }

        public bool HasEmptySlot(out int emptyIndex)
        {
            if (slots != null)
                for (int i = 0; i < inventorySize; i++)
                    if (!slots.ContainsKey(i))
                    {
                        emptyIndex = i;
                        return true;
                    }

            emptyIndex = -1;
            return false;
        }

        private void PublishChanged(Action afterChanged = null)
        {
            publishingChanges = true;
            try
            {
                ReplicateInventory();
                EventBus.Notify(Changed);
                EventBus.PublishNotification(new EB_InventoryChanged { Inventory = this });
                afterChanged?.Invoke();
            }
            finally
            {
                publishingChanges = false;
            }
        }

        private static OperationResult Failure(string message, int slotIndex = -1)
        {
            return new OperationResult { SlotIndex = slotIndex, Message = message };
        }

        private static OperationResult Success(int slotIndex, int quantity, string message)
        {
            return new OperationResult
            {
                Success = true, SlotIndex = slotIndex, Quantity = quantity, Message = message
            };
        }

        private void Log(string message)
        {
            if (loggingEnabled) Debug.Log($"Inventory: {message}");
        }

        public void SetLoggingEnabled(bool enabled) => loggingEnabled = enabled;

        public struct OperationResult
        {
            public bool Success;
            // First affected slot for adds/removals; destination slot for splits.
            public int SlotIndex;
            public int Quantity;
            public string Message;
        }

        public class EB_InventoryAddedItem
        {
            public UInventoryComponent Inventory;
            public ItemDataSO ItemAdded;
            public int SlotIndex;
            // Amount added to this slot, not its resulting total.
            public int Quantity;
        }

        public class EB_InventoryChanged
        {
            public UInventoryComponent Inventory;
        }
    }
}
