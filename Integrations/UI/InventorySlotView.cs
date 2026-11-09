namespace UnscriptedEngine.UI {
using TMPro;
using UnityEngine;
using UnscriptedEngine;

public class InventorySlotView : UWidgetComponent
{
    public event System.Action<InventorySlot> SlotChanged;
    public InventorySlot Slot { get; private set; }
    [SerializeField]
    [Tooltip("Optional label displaying this slot's item name.")]
    private TMP_Text itemNameText;
    [SerializeField]
    [Tooltip("Optional label displaying this slot's rarity fragment.")]
    private TMP_Text rarityText;
    [SerializeField]
    [Tooltip("Optional label displaying this slot's item quantity.")]
    private TMP_Text quantityText;
    private int lastQuantity;
    private string quantityString;

    public void SetSlot(InventorySlot slot)
    {
        bool changed = Slot != slot;
        Slot = slot;
        ItemDataSO item = slot?.Item;
        string itemName = string.Empty;
        string rarity = string.Empty;
        if (item != null && (quantityString == null || slot.Quantity != lastQuantity))
        {
            lastQuantity = slot.Quantity;
            quantityString = slot.Quantity.ToString();
        }
        string quantity = item != null ? quantityString : string.Empty;

        if (item != null && item.fragments != null)
        {
            foreach (UItemFragment fragment in item.fragments)
            {
                switch (fragment)
                {
                    case UItemFragDetails details:
                        itemName = details.Name;
                        break;
                    case UItemFragRarity rarityFragment:
                        rarity = rarityFragment.Rarity;
                        break;
                }
            }
        }

        if (itemNameText != null && itemNameText.text != (itemName ?? string.Empty)) itemNameText.text = itemName ?? string.Empty;
        if (rarityText != null && rarityText.text != (rarity ?? string.Empty)) rarityText.text = rarity ?? string.Empty;
        if (quantityText != null && quantityText.text != quantity) quantityText.text = quantity;
        if (changed) SlotChanged?.Invoke(slot);
    }
}

}
