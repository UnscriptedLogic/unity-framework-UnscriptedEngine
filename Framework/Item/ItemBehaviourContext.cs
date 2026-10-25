using UnityEngine;

namespace UnscriptedEngine.Item
{
    /// <summary>Bindings for one equipped session, not a persistent item instance.</summary>
    public sealed class ItemBehaviourContext
    {
        public ItemEquipComponent Equipment { get; }
        public IInventory Inventory { get; }
        public ItemDataSO Item { get; }
        public int SlotIndex { get; }
        public GameObject Visual { get; }

        public ItemBehaviourContext(ItemEquipComponent equipment, IInventory inventory,
            ItemDataSO item, int slotIndex, GameObject visual)
        {
            Equipment = equipment;
            Inventory = inventory;
            Item = item;
            SlotIndex = slotIndex;
            Visual = visual;
        }

        /// <summary>Resolve a game-specific visual MonoBehaviour once during construction.</summary>
        public T GetVisualComponent<T>() where T : Component =>
            Visual != null ? Visual.GetComponentInChildren<T>(true) : null;
    }
}
