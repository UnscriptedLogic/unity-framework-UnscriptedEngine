namespace UnscriptedEngine.UI {
using TMPro;
using UnityEngine;
using UnscriptedEngine;

public class InventorySlotView : UWidgetComponent
{
    [SerializeField] private TMP_Text itemNameText;
    [SerializeField] private TMP_Text rarityText;
    [SerializeField] private TMP_Text quantityText;
    private int lastQuantity;
    private string quantityString;

    public void SetSlot(InventorySlot slot)
    {
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
            foreach (var fragment in item.fragments)
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
    }
}

}
