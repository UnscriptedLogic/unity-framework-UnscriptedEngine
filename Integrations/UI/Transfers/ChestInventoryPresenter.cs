using UnityEngine;

namespace UnscriptedEngine.UI
{
    /// <summary>Opens the inventory after the chest handles interaction and loot initialization.</summary>
    [RequireComponent(typeof(UChest))]
    public sealed class ChestInventoryPresenter : InspectableInventory
    {
        private UChest chest;

        protected override void OnEnable()
        {
            chest = GetComponent<UChest>();
            chest.InventoryOpenRequested += OpenInventory;
        }

        protected override void OnDisable()
        {
            if (chest != null) chest.InventoryOpenRequested -= OpenInventory;
            CloseInventory();
        }

        protected override void OnInventoryClosed()
        {
            if (chest != null) chest.TryClose();
        }
    }
}
