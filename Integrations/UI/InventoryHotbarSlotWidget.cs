using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UnscriptedEngine.UI
{
    /// <summary>Prefab-owned slot presentation. Replace its layout or override rendering for a custom design.</summary>
    public class InventoryHotbarSlotWidget : UWidgetComponent
    {
        [SerializeField] private TMP_Text itemNameText;
        [SerializeField] private TMP_Text quantityText;
        [SerializeField] private TMP_Text slotNumberText;
        [SerializeField] private Image background;
        [SerializeField] private GameObject selectedVisual;
        [SerializeField] private string emptyLabel = "Empty";
        [SerializeField] private Color backgroundColor = new(.04f, .05f, .07f, .85f);
        [SerializeField] private Color selectedColor = new(.18f, .38f, .52f, .95f);

        public override bool Focusable => false;
        public int SlotIndex { get; private set; }
        public InventorySlot Slot { get; private set; }
        public bool IsSelected { get; private set; }

        protected virtual void Awake()
        {
            foreach (var label in new[] { itemNameText, quantityText, slotNumberText })
                if (label != null && label.font == null) label.font = TMP_Settings.defaultFontAsset;
            GetComponent<CanvasGroup>().interactable = false;
            GetComponent<CanvasGroup>().blocksRaycasts = false;
        }

        public virtual void SetSlot(int index, InventorySlot slot)
        {
            SlotIndex = index;
            Slot = slot;
            ItemDataSO item = slot?.Item;
            string name = item != null ? item.name : emptyLabel;
            if (item != null && item.fragments != null)
                foreach (var fragment in item.fragments)
                    if (fragment is UItemFragDetails details && !string.IsNullOrWhiteSpace(details.Name)) { name = details.Name; break; }
            if (itemNameText != null) itemNameText.text = name;
            if (quantityText != null) quantityText.text = item != null ? slot.Quantity.ToString() : string.Empty;
            if (slotNumberText != null) slotNumberText.text = (index + 1).ToString();
        }

        public virtual void SetSelected(bool selected)
        {
            IsSelected = selected;
            if (background != null) background.color = selected ? selectedColor : backgroundColor;
            if (selectedVisual != null) selectedVisual.SetActive(selected);
        }
    }
}
