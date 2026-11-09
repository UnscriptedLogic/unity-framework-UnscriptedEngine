using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

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
        private RectTransform preview;
        private Canvas previewCanvas;
        public void Bind(InventoryTransferPanel owner, UInventoryComponent source, int slotIndex)
        {
            CancelDrag();
            panel = owner;
            inventory = source;
            index = slotIndex;
            draggedItem = null;
            draggedQuantity = 0;
        }
        public void OnBeginDrag(PointerEventData eventData)
        {
            CancelDrag();
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (inventory != null && inventory.Items != null && inventory.Items.TryGetValue(index, out InventorySlot slot))
            {
                draggedItem = slot.Item;
                draggedQuantity = slot.Quantity;
                CreatePreview(eventData);
            }
        }
        public void OnDrag(PointerEventData eventData) => MovePreview(eventData);
        public void OnEndDrag(PointerEventData eventData) => CancelDrag();
        private void OnDisable() => CancelDrag();
        public void OnDrop(PointerEventData eventData)
        {
            if (inventory == null || eventData.pointerDrag == null) return;
            InventorySlotDragHandler source = eventData.pointerDrag.GetComponent<InventorySlotDragHandler>();
            if (source == null || source.panel != panel || source.draggedItem == null || source.inventory == null
                || !source.inventory.Items.TryGetValue(source.index, out InventorySlot current)
                || current.Item != source.draggedItem || current.Quantity != source.draggedQuantity) return;
            if (panel != null) panel.Transfer(source.inventory, source.index, inventory, index, source.draggedQuantity);
            else if (source.inventory == inventory)
                source.inventory.TryTransferItem(source.index, inventory, index, source.draggedQuantity);
        }

        private void CreatePreview(PointerEventData eventData)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;
            previewCanvas = canvas.rootCanvas;
            GameObject visual = new GameObject("Inventory Drag Preview", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            preview = (RectTransform)visual.transform;
            preview.SetParent(previewCanvas.transform, false);
            preview.anchorMin = preview.anchorMax = ((RectTransform)previewCanvas.transform).pivot;
            preview.sizeDelta = ((RectTransform)transform).rect.size;
            CanvasGroup group = visual.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            group.alpha = 0.9f;
            Image background = visual.GetComponent<Image>();
            background.color = Color.black;
            background.raycastTarget = false;
            Outline border = visual.AddComponent<Outline>();
            border.effectColor = Color.white;
            border.effectDistance = new Vector2(1f, -1f);
            foreach (TMP_Text source in GetComponentsInChildren<TMP_Text>())
            {
                GameObject labelObject = new GameObject("Drag Label", typeof(RectTransform));
                RectTransform labelRect = (RectTransform)labelObject.transform;
                labelRect.SetParent(preview, false);
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                // Preserve nested prefab text placement in slot-local coordinates.
                RectTransform sourceRect = source.rectTransform;
                Vector3[] corners = new Vector3[4];
                sourceRect.GetWorldCorners(corners);
                RectTransform slotRect = (RectTransform)transform;
                Vector3 minimum = slotRect.InverseTransformPoint(corners[0]);
                Vector3 maximum = slotRect.InverseTransformPoint(corners[2]);
                labelRect.offsetMin = (Vector2)minimum - slotRect.rect.min;
                labelRect.offsetMax = (Vector2)maximum - slotRect.rect.max;
                TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
                label.font = source.font;
                label.fontSize = source.fontSize;
                label.fontStyle = source.fontStyle;
                label.alignment = source.alignment;
                label.color = source.color;
                label.text = source.text;
                label.raycastTarget = false;
            }
            MovePreview(eventData);
        }

        private void MovePreview(PointerEventData eventData)
        {
            if (preview == null || previewCanvas == null) return;
            Camera camera = previewCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : eventData.pressEventCamera;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)previewCanvas.transform,
                eventData.position, camera, out Vector2 position)) preview.anchoredPosition = position;
        }

        private void CancelDrag()
        {
            draggedItem = null;
            draggedQuantity = 0;
            if (preview != null)
            {
                preview.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(preview.gameObject);
                else DestroyImmediate(preview.gameObject);
            }
            preview = null;
            previewCanvas = null;
        }
    }
}
