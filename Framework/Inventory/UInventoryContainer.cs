using System;
using System.Collections.Generic;
using UnityEngine;

namespace Framework.Inventory
{
    [Serializable]
    public sealed class UInventoryContainer
    {
        [SerializeField, Min(1)] private int capacity = 36;
        [SerializeField] private List<UItemStack> slots = new();
        public int Capacity => capacity;
        public IReadOnlyList<UItemStack> Slots => slots;

        public UInventoryContainer(int capacity = 36)
        {
            this.capacity = Mathf.Max(1, capacity);
            EnsureCapacity();
        }

        public int Count(UItemDefinition definition)
        {
            int result = 0;
            for (int i = 0; i < slots.Count; i++) if (slots[i]?.Definition == definition) result += slots[i].Quantity;
            return result;
        }

        public bool TryAdd(UItemStack incoming, out UItemStack remainder)
        {
            remainder = incoming?.Clone() ?? UItemStack.Empty();
            if (remainder.IsEmpty) return true;
            EnsureCapacity();
            for (int i = 0; i < slots.Count && !remainder.IsEmpty; i++)
            {
                if (slots[i] == null || !slots[i].CanStackWith(remainder)) continue;
                int left = slots[i].Add(remainder.Quantity);
                remainder.SetQuantity(left);
            }
            for (int i = 0; i < slots.Count && !remainder.IsEmpty; i++)
            {
                if (slots[i] != null && !slots[i].IsEmpty) continue;
                int amount = Mathf.Min(remainder.Quantity, remainder.Definition.MaxStackSize);
                slots[i] = remainder.Clone(amount);
                remainder.SetQuantity(remainder.Quantity - amount);
            }
            return remainder.IsEmpty;
        }

        public UItemStack Remove(int slot, int amount)
        {
            EnsureCapacity();
            if (slot < 0 || slot >= slots.Count || slots[slot] == null || slots[slot].IsEmpty || amount <= 0) return UItemStack.Empty();
            UItemStack result = slots[slot].Clone(Mathf.Min(amount, slots[slot].Quantity));
            slots[slot].Remove(result.Quantity);
            return result;
        }

        public void SetSlot(int slot, UItemStack stack)
        {
            EnsureCapacity();
            if (slot < 0 || slot >= slots.Count) throw new ArgumentOutOfRangeException(nameof(slot));
            slots[slot] = stack?.Clone() ?? UItemStack.Empty();
        }

        private void EnsureCapacity()
        {
            capacity = Mathf.Max(1, capacity);
            while (slots.Count < capacity) slots.Add(UItemStack.Empty());
            while (slots.Count > capacity) slots.RemoveAt(slots.Count - 1);
        }
    }
}
