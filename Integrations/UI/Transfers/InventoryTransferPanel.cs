using System;
using UnityEngine;

namespace UnscriptedEngine.UI
{
    /// <summary>Coordinates two inventory views and their transfer requests.</summary>
    public sealed class InventoryTransferPanel : InventoryPresenter
    {
        public override CanvasInputPolicy InputPolicy => CanvasInputPolicy.Modal;
        public override CanvasLayer Layer => CanvasLayer.Modal;
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

        public override bool CanOpen(UInventoryComponent playerInventory, UInventoryComponent containerInventory) =>
            playerView != null && containerView != null && playerView != containerView &&
            playerInventory != null && containerInventory != null && playerInventory != containerInventory;

        public override void Open(UInventoryComponent playerInventory, UInventoryComponent containerInventory)
        {
            if (!CanOpen(playerInventory, containerInventory)) return;
            player = playerInventory;
            container = containerInventory;
            player.Initialize();
            container.Initialize();
            gameObject.SetActive(true);
            playerView.Initialize(player, this);
            containerView.Initialize(container, this);
        }
        public override void Close()
        {
            if (playerView != null) playerView.Initialize(null);
            if (containerView != null) containerView.Initialize(null);
            player = null;
            container = null;
            base.Close();
        }
        public override bool HandleBack() { Close(); return true; }

        public override bool HandleInput(PlayerInputEvent input)
        {
            if (!IsFocused || input is not PlayerActionInputEvent action ||
                !action.IsPerformed || !action.IsPressed || action.IsSynthetic ||
                !string.Equals(action.ActionKey, Controller?.InteractAction, StringComparison.OrdinalIgnoreCase))
                return false;

            input.Cancel();
            Close();
            return true;
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

