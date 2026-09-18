using System;
using System.Collections.Generic;
using UnityEngine;

namespace Framework.Inventory
{
    [Serializable]
    public sealed class UItemProperty
    {
        [SerializeField] private string key;
        [SerializeField] private string value;
        public string Key => key;
        public string Value => value;
        public UItemProperty(string key, string value) { this.key = key; this.value = value; }
    }

    [Serializable]
    public sealed class UItemStack
    {
        [SerializeField] private UItemDefinition definition;
        [SerializeField, Min(0)] private int quantity;
        [SerializeField, Min(-1)] private int durability = -1;
        [SerializeField] private List<UItemProperty> properties = new();

        public UItemDefinition Definition => definition;
        public int Quantity => quantity;
        public int Durability => durability;
        public IReadOnlyList<UItemProperty> Properties => properties;
        public bool IsEmpty => definition == null || quantity <= 0;

        public UItemStack(UItemDefinition definition, int quantity, int durability = -1)
        {
            this.definition = definition;
            this.quantity = Mathf.Max(0, quantity);
            this.durability = durability;
        }

        public static UItemStack Empty() => new(null, 0);

        public UItemStack Clone(int amount = -1)
        {
            var copy = new UItemStack(definition, amount < 0 ? quantity : amount, durability);
            copy.properties.AddRange(properties);
            return copy;
        }

        public bool CanStackWith(UItemStack other)
        {
            if (IsEmpty || other == null || other.IsEmpty || definition != other.definition || durability != other.durability || properties.Count != other.properties.Count) return false;
            for (int i = 0; i < properties.Count; i++)
                if (properties[i].Key != other.properties[i].Key || properties[i].Value != other.properties[i].Value) return false;
            return true;
        }

        public int Add(int amount)
        {
            if (amount <= 0 || IsEmpty) return amount;
            int accepted = Mathf.Clamp(amount, 0, Mathf.Max(0, definition.MaxStackSize - quantity));
            quantity += accepted;
            return amount - accepted;
        }

        public int Remove(int amount)
        {
            int removed = Mathf.Min(Mathf.Max(0, amount), quantity);
            quantity -= removed;
            return removed;
        }

        public void SetQuantity(int value) => quantity = Mathf.Max(0, value);
    }
}
