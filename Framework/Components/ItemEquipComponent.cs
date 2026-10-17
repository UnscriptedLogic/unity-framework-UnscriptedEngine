using UnityEngine;
using UnscriptedEngine;

public class ItemEquipComponent : UObjectComponent
{
    [SerializeField, Tooltip("Component implementing IInventory to observe. Can also be assigned through SetInventory.")]
    private Component inventoryComponent;
    [SerializeField, Min(0), Tooltip("Number of inventory slots available for selection. Limited by inventory size. The pawn controls which slot is selected.")]
    private int hotbarSize = 10;
    [SerializeField, Tooltip("Parent transform for held visuals. Uses this transform when unassigned.")]
    private Transform equipAnchor;

    [Header("Debug")]
    [SerializeField, Tooltip("Log inventory observation, slot selection, and held visual changes.")]
    private bool debug;

    private IInventory inventory;
    private bool observingInventory;
    private GameObject heldVisual;
    private ItemDataSO heldItem;

    public IInventory Inventory => inventory;
    public int SelectedSlotIndex { get; private set; } = -1;
    public int VisibleSlotCount => IsInventoryAvailable()
        ? Mathf.Min(Mathf.Max(0, hotbarSize), Mathf.Max(0, inventory.InventorySize)) : 0;
    public ItemDataSO HeldItem => heldItem;

    protected override void OnEnable()
    {
        base.OnEnable();
        Log("Enabled; observing inventory.");
        
        if (inventory == null)
        {
            inventory = inventoryComponent as IInventory;
        }

        if (inventory == null && inventoryComponent != null)
        {
            inventory = inventoryComponent.GetComponent<IInventory>();
        }
        
        ObserveInventory();
        
        
        RefreshHeldItem();
    }

    protected override void OnDisable()
    {
        Log("Disabled; removing subscriptions and clearing held visual.");
        StopObservingInventory();
        ClearHeldVisual();
        base.OnDisable();
    }

    protected override void OnDestroyed()
    {
        StopObservingInventory();
        ClearHeldVisual();
        base.OnDestroyed();
    }

    protected override void BeginPlay()
    {
        base.BeginPlay();
        ObserveInventory();
        RefreshHeldItem();
    }

    protected override void EndPlay()
    {
        StopObservingInventory();
        ClearHeldVisual();
        base.EndPlay();
    }

    public void SetInventory(IInventory newInventory)
    {
        StopObservingInventory();
        inventory = newInventory;
        inventoryComponent = newInventory as Component;
        Log($"Inventory assigned: {newInventory}.");
        SelectedSlotIndex = -1;
        ClearHeldVisual();
        if (!isActiveAndEnabled) return;
        ObserveInventory();
        RefreshHeldItem();
    }

    private bool IsInventoryAvailable()
    {
        return inventory != null && (!(inventory is Object unityObject) || unityObject != null);
    }

    private void ObserveInventory()
    {
        if (observingInventory) return;
        if (!IsInventoryAvailable())
        {
            Log("Cannot observe inventory: assign a component implementing IInventory or call SetInventory.");
            return;
        }
        inventory.Changed += RefreshHeldItem;
        observingInventory = true;
        Log($"Observing inventory with {inventory.InventorySize} slots; {VisibleSlotCount} hotbar slots available.");
    }

    private void StopObservingInventory()
    {
        if (!observingInventory) return;
        inventory.Changed -= RefreshHeldItem;
        observingInventory = false;
        Log("Stopped observing inventory.");
    }

    public bool SelectSlot(int slotIndex)
    {
        if (!isActiveAndEnabled || slotIndex < 0 || slotIndex >= VisibleSlotCount)
        {
            Log($"Selection rejected: slot {slotIndex}, available slots {VisibleSlotCount}, active {isActiveAndEnabled}.");
            return false;
        }
        Log($"Selecting slot {slotIndex}; previous slot {SelectedSlotIndex}.");
        SelectedSlotIndex = slotIndex;
        RefreshHeldItem();
        return true;
    }

    private void RefreshHeldItem()
    {
        int slotCount = VisibleSlotCount;
        Log($"Refreshing held item; selected slot {SelectedSlotIndex}, available slots {slotCount}.");
        SelectedSlotIndex = slotCount == 0 ? -1 : Mathf.Clamp(SelectedSlotIndex, 0, slotCount - 1);
        ItemDataSO item = GetSelectedItem();
        if (item != null && item == heldItem && heldVisual != null) return;
        ClearHeldVisual();
        UItemFragEquip fragment = FindEquipFragment(item);
        if (fragment == null || fragment.VisualPrefab == null)
        {
            Log(item == null
                ? $"Slot {SelectedSlotIndex} has no item to display."
                : $"Item {item.name} has no equip fragment or visual prefab; no held visual created.");
            return;
        }
        Transform anchor = equipAnchor != null ? equipAnchor : transform;
        heldVisual = Instantiate(fragment.VisualPrefab, anchor, false);
        heldVisual.transform.localPosition = fragment.AnchorPosition;
        heldVisual.transform.localRotation = Quaternion.Euler(fragment.AnchorRotation);
        heldItem = item;
        Log($"Created held visual {heldVisual.name} for {item.name} at anchor {anchor.name}.");
    }

    private ItemDataSO GetSelectedItem()
    {
        if (SelectedSlotIndex < 0 || !IsInventoryAvailable() || inventory.Items == null) return null;
        return inventory.Items.TryGetValue(SelectedSlotIndex, out InventorySlot slot)
            && slot != null && slot.Quantity > 0 ? slot.Item : null;
    }

    private static UItemFragEquip FindEquipFragment(ItemDataSO item)
    {
        if (item == null || item.fragments == null) return null;
        foreach (UItemFragment fragment in item.fragments)
        {
            if (fragment is UItemFragEquip equipFragment) return equipFragment;
        }
        return null;
    }

    private void ClearHeldVisual()
    {
        if (heldVisual != null)
        {
            Log($"Clearing held visual {heldVisual.name}.");
            heldVisual.SetActive(false);
            Destroy(heldVisual);
        }
        heldVisual = null;
        heldItem = null;
    }

    private void Log(string message)
    {
        if (!debug) return;
        UnityEngine.Debug.Log($"[ItemEquipComponent] {message}", this);
    }

}

