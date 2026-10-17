using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace UnscriptedEngine
{
    public class UInventoryComponent : UObjectComponent, IInventory
    {
        [SerializeField, Min(0), Tooltip("Number of zero-based slots available in this inventory.")]
        private int inventorySize = 10;
        private Dictionary<int, InventorySlot> slots;
        private bool loggingEnabled = true;
        private bool publishingChanges;

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
            return PlanAddItem(itemDataSo, quantity, out _);
        }

        private OperationResult PlanAddItem(ItemDataSO item, int quantity,
            out List<KeyValuePair<int, int>> additions)
        {
            additions = new List<KeyValuePair<int, int>>();
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
            // Fill existing stacks before allocating new slots.
            for (int i = 0; i < inventorySize && remaining > 0; i++)
            {
                if (!slots.TryGetValue(i, out var slot) || slot.Item != item) continue;
                int amount = Math.Min(remaining, Math.Max(0, capacity - slot.Quantity));
                if (amount == 0) continue;
                additions.Add(new KeyValuePair<int, int>(i, amount));
                remaining -= amount;
            }

            for (int i = 0; i < inventorySize && remaining > 0; i++)
            {
                if (slots.ContainsKey(i)) continue;
                int amount = Math.Min(remaining, capacity);
                additions.Add(new KeyValuePair<int, int>(i, amount));
                remaining -= amount;
            }

            if (remaining > 0) return Failure("Not enough inventory capacity");
            return Success(additions[0].Key, quantity, "Item can be added");
        }

        // Removing the final item frees the slot. The default removes one item.
        public OperationResult TryRemoveItem(int slotIndex, int quantity = 1)
        {
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

        // Moves part of a stack into a new slot without changing the total quantity.
        // SlotIndex in the result identifies the newly created stack.
        public OperationResult TrySplitStack(int sourceSlotIndex, int quantity)
        {
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
