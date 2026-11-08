using System;
using UnityEngine;

namespace UnscriptedEngine
{
    public enum ChestContentsMode { Predefined, Chance, Custom }

    [Serializable]
    public struct ChestItemAmount
    {
        [SerializeField]
        [Tooltip("Item definition to place in the chest.")]
        private ItemDataSO item;
        [SerializeField]
        [Min(1)]
        [Tooltip("Exact quantity to place in the chest.")]
        private int quantity;
        public ItemDataSO Item => item;
        public int Quantity => quantity;
        public ChestItemAmount(ItemDataSO item, int quantity) { this.item = item; this.quantity = quantity; }
    }
}
