using UnityEngine;
using UnityEngine.EventSystems;

namespace UnscriptedEngine.UI
{
    /// <summary>Pointer-local drag state; no persistent static state between play sessions.</summary>
    public sealed class InventorySlotDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
    {
        private InventoryTransferPanel panel;
        private UInventoryComponent inventory;
        private int index;
        private ItemDataSO draggedItem;
        private int draggedQuantity;
        public void Bind(InventoryTransferPanel owner, UInventoryComponent source, int slotIndex)
        {
            panel = owner;
            inventory = source;
            index = slotIndex;
            draggedItem = null;
            draggedQuantity = 0;
        }
        public void OnBeginDrag(PointerEventData eventData)
        {
            draggedItem = null;
            draggedQuantity = 0;
            if (inventory != null && inventory.Items != null && inventory.Items.TryGetValue(index, out InventorySlot slot))
            {
                draggedItem = slot.Item;
                draggedQuantity = slot.Quantity;
            }
        }
        public void OnDrag(PointerEventData eventData) { }
        public void OnEndDrag(PointerEventData eventData) { draggedItem = null; draggedQuantity = 0; }
        private void OnDisable() { draggedItem = null; draggedQuantity = 0; }
        public void OnDrop(PointerEventData eventData)
        {
            if (panel == null || eventData.pointerDrag == null) return;
            InventorySlotDragHandler source = eventData.pointerDrag.GetComponent<InventorySlotDragHandler>();
            if (source == null || source.panel != panel || source.draggedItem == null || source.inventory == null
                || !source.inventory.Items.TryGetValue(source.index, out InventorySlot current)
                || current.Item != source.draggedItem || current.Quantity != source.draggedQuantity) return;
            panel.Transfer(source.inventory, source.index, inventory, index, source.draggedQuantity);
        }
    }
}
