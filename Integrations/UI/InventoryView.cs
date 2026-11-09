namespace UnscriptedEngine.UI {
using System.Collections.Generic;
using UnityEngine;
using UnscriptedEngine;

public class InventoryView : UCanvasComponent
{
    [SerializeField]
    [Tooltip("Slot visual used to build this inventory grid.")]
    private GameObject slotPrefab;
    [SerializeField]
    [Tooltip("Parent containing the inventory cells.")]
    private Transform gridContainer;
    [SerializeField]
    [Tooltip("Optional occupied-slot counter.")]
    private TMPro.TMP_Text slotCountText;

    private IInventory _inventory;
    private InventorySlotView[] _slots;
    private InventoryTransferPanel transferPanel;

    public void Initialize(IInventory inventory, InventoryTransferPanel panel = null)
    {
        transferPanel = panel;
        if (_inventory != null) _inventory.Changed -= RefreshSlots;
        _inventory = inventory;
        if (_inventory == null) { RefreshSlots(); return; }
        if (isActiveAndEnabled) _inventory.Changed += RefreshSlots;
        BuildGrid();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (_inventory != null) { _inventory.Changed -= RefreshSlots; _inventory.Changed += RefreshSlots; }
        RefreshSlots();
    }

    protected override void OnDisable()
    {
        if (_inventory != null) _inventory.Changed -= RefreshSlots;
        base.OnDisable();
    }

    private void BuildGrid()
    {
        if (gridContainer == null || slotPrefab == null) { Debug.LogError("Inventory view requires a container and slot prefab.", this); return; }
        List<InventorySlotView> existingSlots = new List<InventorySlotView>();
        foreach (Transform child in gridContainer)
        {
            if (child.TryGetComponent<InventorySlotView>(out InventorySlotView existingSlot))
            {
                existingSlot.gameObject.SetActive(false);
                existingSlots.Add(existingSlot);
            }
        }

        _slots = new InventorySlotView[_inventory.InventorySize];

        for (int i = 0; i < _inventory.InventorySize; i++)
        {
            GameObject slot = i < existingSlots.Count
                ? existingSlots[i].gameObject
                : Instantiate(slotPrefab, gridContainer);
            slot.SetActive(true);
            _slots[i] = slot.GetComponent<InventorySlotView>();
            InventorySlotDragHandler drag = slot.GetComponent<InventorySlotDragHandler>();
            if (drag == null && _inventory is UInventoryComponent) drag = slot.AddComponent<InventorySlotDragHandler>();
            if (drag != null) drag.Bind(transferPanel, _inventory as UInventoryComponent, i);
            if (_slots[i] == null)
                Debug.LogError("Inventory slot prefab requires InventorySlotView.", slot);
        }

        RefreshSlots();
    }

    private void RefreshSlots()
    {
        if (_slots == null) return;
        if (_inventory == null)
        {
            foreach (InventorySlotView slot in _slots)
            {
                if (slot == null) continue;
                slot.SetSlot(null);
                if (slot.TryGetComponent(out InventorySlotDragHandler drag)) drag.Bind(null, null, -1);
            }
            if (slotCountText != null) slotCountText.text = string.Empty;
            return;
        }
        if (_slots.Length != _inventory.InventorySize) { BuildGrid(); return; }

        if (slotCountText != null)
            slotCountText.text = $"{_inventory.Items?.Count ?? 0} / {_inventory.InventorySize} slots";

        IReadOnlyDictionary<int, InventorySlot> items = _inventory.Items;
        for (int i = 0; i < _slots.Length; i++)
        {
            InventorySlot value = null;
            if (items != null) items.TryGetValue(i, out value);
            if (_slots[i] != null) _slots[i].SetSlot(value);
        }
    }
}

}
