using System;
using UnityEngine;

namespace UnscriptedEngine.UI
{
    /// <summary>Coordinates two inventory views and their transfer requests.</summary>
    public sealed class InventoryTransferPanel : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("View displaying the player's inventory.")]
        private InventoryView playerView;
        [SerializeField]
        [Tooltip("View displaying the opened container.")]
        private InventoryView containerView;
        private UInventoryComponent player;
        private UInventoryComponent container;

        // Optional authority adapter. When assigned, it replaces direct local execution.
        public Action<UInventoryComponent, int, UInventoryComponent, int, int> TransferRequested { get; set; }
        public event Action<UInventoryComponent.OperationResult> TransferCompleted;
        public bool Displays(UInventoryComponent inventory) => inventory != null && container == inventory;

        public void Open(UInventoryComponent playerInventory, UInventoryComponent containerInventory)
        {
            if (playerView == null || containerView == null || playerView == containerView
                || playerInventory == null || containerInventory == null) return;
            player = playerInventory;
            container = containerInventory;
            player.Initialize();
            container.Initialize();
            gameObject.SetActive(true);
            playerView.Initialize(player, this);
            containerView.Initialize(container, this);
        }
        public void Close()
        {
            if (playerView != null) playerView.Initialize(null);
            if (containerView != null) containerView.Initialize(null);
            player = null;
            container = null;
            gameObject.SetActive(false);
        }
        public void Transfer(UInventoryComponent source, int sourceSlot, UInventoryComponent destination, int targetSlot, int quantity)
        {
            if (!isActiveAndEnabled || player == null || container == null
                || (source != player && source != container) || (destination != player && destination != container)) return;
            if (TransferRequested != null) TransferRequested(source, sourceSlot, destination, targetSlot, quantity);
            else
            {
                UInventoryComponent.OperationResult result = source.TryTransferItem(sourceSlot, destination, targetSlot, quantity);
                TransferCompleted?.Invoke(result);
            }
        }
    }
}

