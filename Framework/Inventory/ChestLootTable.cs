using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnscriptedEngine
{
    [CreateAssetMenu(menuName = "UnscriptedEngine/Inventory/Chest Loot Table")]
    public sealed class ChestLootTable : ChestLootProvider
    {
        [Serializable]
        public sealed class Entry
        {
            [SerializeField]
            [Tooltip("Item definition awarded when this entry succeeds.")]
            private ItemDataSO item;
            [SerializeField]
            [Range(0f, 100f)]
            [Tooltip("Independent chance in percent. 0 never drops; 100 always drops.")]
            private float chancePercent = 100f;
            [SerializeField]
            [Min(1)]
            [Tooltip("Smallest awarded quantity, inclusive.")]
            private int minimumQuantity = 1;
            [SerializeField]
            [Min(1)]
            [Tooltip("Largest awarded quantity, inclusive. Must be below int.MaxValue.")]
            private int maximumQuantity = 1;

            internal void Validate()
            {
                if (item == null || minimumQuantity < 1 || maximumQuantity < minimumQuantity
                    || maximumQuantity == int.MaxValue || float.IsNaN(chancePercent)
                    || chancePercent < 0f || chancePercent > 100f)
                    throw new InvalidOperationException("Loot table entry needs an item, valid quantities and 0–100 chance.");
            }
            internal void Roll(List<InventorySlot> output)
            {
                if (chancePercent <= 0f || (chancePercent < 100f && UnityEngine.Random.value >= chancePercent / 100f)) return;
                output.Add(new InventorySlot(item, UnityEngine.Random.Range(minimumQuantity, maximumQuantity + 1)));
            }
        }

        [SerializeField]
        [Tooltip("Each entry rolls once, independently. Results can contain no items or multiple items.")]
        private List<Entry> entries = new List<Entry>();

        public override void Generate(UChest chest, GameObject interactor, List<InventorySlot> output)
        {
            if (entries == null) throw new InvalidOperationException("Loot table entries are missing.");
            foreach (Entry entry in entries)
            {
                if (entry == null) throw new InvalidOperationException("Loot table contains an empty entry.");
                entry.Validate();
            }
            foreach (Entry entry in entries) entry.Roll(output);
        }
    }
}
