namespace UnscriptedEngine.UI {
using System.Collections.Generic;
using UnityEngine;
using UnscriptedEngine;

public class InventoryView : UCanvasComponent
{
    [SerializeField] private GameObject slotPrefab;
    [SerializeField] private Transform gridContainer;
    [SerializeField] private TMPro.TMP_Text slotCountText;

    private IInventory _inventory;
    private InventorySlotView[] _slots;

    public void Initialize(IInventory inventory)
    {
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
        var existingSlots = new List<InventorySlotView>();
        foreach (Transform child in gridContainer)
        {
            if (child.TryGetComponent<InventorySlotView>(out var existingSlot))
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
            if (_slots[i] == null)
                Debug.LogError("Inventory slot prefab requires InventorySlotView.", slot);
        }

        RefreshSlots();
    }

    private void RefreshSlots()
    {
        if (_slots == null) return;
        if (_inventory == null) { foreach (var slot in _slots) if (slot != null) slot.SetSlot(null); if (slotCountText != null) slotCountText.text = string.Empty; return; }
        if (_slots.Length != _inventory.InventorySize) { BuildGrid(); return; }

        if (slotCountText != null)
            slotCountText.text = $"{_inventory.Items?.Count ?? 0} / {_inventory.InventorySize} slots";

        foreach (var slot in _slots)
            if (slot != null) slot.SetSlot(null);

        if (_inventory.Items == null) return;
        foreach (KeyValuePair<int, InventorySlot> kvp in _inventory.Items)
            if (kvp.Key >= 0 && kvp.Key < _slots.Length && _slots[kvp.Key] != null) _slots[kvp.Key].SetSlot(kvp.Value);
    }
}

}
