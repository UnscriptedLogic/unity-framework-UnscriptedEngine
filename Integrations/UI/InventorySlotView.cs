namespace UnscriptedEngine.UI {
using TMPro;
using UnityEngine;
using UnscriptedEngine;

public class InventorySlotView : MonoBehaviour
{
    [SerializeField] private TMP_Text itemNameText;
    [SerializeField] private TMP_Text rarityText;
    [SerializeField] private TMP_Text quantityText;

    public void SetSlot(InventorySlot slot)
    {
        ItemDataSO item = slot?.Item;
        string itemName = string.Empty;
        string rarity = string.Empty;
        string quantity = item != null ? slot.Quantity.ToString() : string.Empty;

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

        if (itemNameText != null) itemNameText.text = itemName ?? string.Empty;
        if (rarityText != null) rarityText.text = rarity ?? string.Empty;
        if (quantityText != null) quantityText.text = quantity;
    }
}

}
