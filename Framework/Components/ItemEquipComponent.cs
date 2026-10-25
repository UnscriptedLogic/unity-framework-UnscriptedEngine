using UnityEngine;
using UnscriptedEngine;
using UnscriptedEngine.Item;

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
    private ItemBehaviour heldBehaviour;
    private int heldSlotIndex = -1;
    private bool refreshing;
    private bool clearing;

    public IInventory Inventory => inventory;
    public int SelectedSlotIndex { get; private set; } = -1;
    public int VisibleSlotCount => IsInventoryAvailable()
        ? Mathf.Min(Mathf.Max(0, hotbarSize), Mathf.Max(0, inventory.InventorySize)) : 0;
    public ItemDataSO HeldItem => heldItem;
    public GameObject HeldVisual => heldVisual;

    /// <summary>Input mapping and network authority belong to the caller/game behaviour.</summary>
    public bool Use(string action, bool pressed)
    {
        if (!isActiveAndEnabled || !CanTick || refreshing || clearing || string.IsNullOrEmpty(action)) return false;
        return heldBehaviour?.Use(action, pressed) ?? false;
    }

    /// <summary>Call on input focus loss or unpossession, even when gameplay input is blocked.</summary>
    public void CancelUse() => heldBehaviour?.CancelUse();

    protected override void Tick(float deltaTime) => heldBehaviour?.Tick(deltaTime);
    protected override void FixedTick(float deltaTime) => heldBehaviour?.FixedTick(deltaTime);

    protected override void OnDespawned()
    {
        StopObservingInventory();
        ClearHeldVisual();
        base.OnDespawned();
    }

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
        if (refreshing || clearing) throw new System.InvalidOperationException("Cannot replace inventory during equipment callbacks.");
        StopObservingInventory();
        inventory = newInventory;
        inventoryComponent = newInventory as Component;
        if (debug) Log($"Inventory assigned: {newInventory}.");
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
        if (debug) Log($"Observing inventory with {inventory.InventorySize} slots; {VisibleSlotCount} hotbar slots available.");
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
        if (refreshing || clearing || !isActiveAndEnabled || slotIndex < 0 || slotIndex >= VisibleSlotCount)
        {
            if (debug) Log($"Selection rejected: slot {slotIndex}, available slots {VisibleSlotCount}, active {isActiveAndEnabled}.");
            return false;
        }
        if (debug) Log($"Selecting slot {slotIndex}; previous slot {SelectedSlotIndex}.");
        SelectedSlotIndex = slotIndex;
        RefreshHeldItem();
        return true;
    }

    private void RefreshHeldItem()
    {
        if (refreshing || clearing || !isActiveAndEnabled) return;
        refreshing = true;
        try { RefreshHeldItemInternal(); }
        finally { refreshing = false; }
    }

    private void RefreshHeldItemInternal()
    {
        int slotCount = VisibleSlotCount;
        if (debug) Log($"Refreshing held item; selected slot {SelectedSlotIndex}, available slots {slotCount}.");
        SelectedSlotIndex = slotCount == 0 ? -1 : Mathf.Clamp(SelectedSlotIndex, 0, slotCount - 1);
        ItemDataSO item = GetSelectedItem();
        if (item != null && item == heldItem && heldSlotIndex == SelectedSlotIndex
            && (heldVisual != null || FindEquipFragment(item)?.VisualPrefab == null)) return;
        ClearHeldVisual();
        if (!isActiveAndEnabled || item == null) return;
        UItemFragEquip fragment = FindEquipFragment(item);
        if (fragment != null && fragment.VisualPrefab != null)
        {
            Transform anchor = equipAnchor != null ? equipAnchor : transform;
            heldVisual = Instantiate(fragment.VisualPrefab, anchor, false);
            heldVisual.transform.localPosition = fragment.AnchorPosition;
            heldVisual.transform.localRotation = Quaternion.Euler(fragment.AnchorRotation);
        }
        heldItem = item;
        heldSlotIndex = SelectedSlotIndex;
        try
        {
            if (item.fragments != null)
                foreach (UItemFragment candidate in item.fragments)
                    if (candidate is UItemFragBehaviour factory)
                    {
                        heldBehaviour = factory.CreateBehaviour(new ItemBehaviourContext(
                            this, inventory, item, heldSlotIndex, heldVisual));
                        heldBehaviour?.Equip();
                        break;
                    }
        }
        catch
        {
            ClearHeldVisual();
            throw;
        }
        if (debug) Log($"Equipped {item.name} from slot {heldSlotIndex}.");
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
        if (clearing) return;
        clearing = true;
        try { ClearHeldVisualInternal(); }
        finally { clearing = false; }
    }

    private void ClearHeldVisualInternal()
    {
        // Detach first: callbacks can disable/destroy this component recursively.
        ItemBehaviour behaviour = heldBehaviour;
        GameObject visual = heldVisual;
        heldBehaviour = null;
        heldVisual = null;
        heldItem = null;
        heldSlotIndex = -1;
        if (behaviour != null)
        {
            Cleanup(behaviour.CancelUse);
            Cleanup(behaviour.Unequip);
            Cleanup(behaviour.Dispose);
        }
        if (visual == null) return;
        visual.SetActive(false);
        if (Application.isPlaying) Destroy(visual);
        else DestroyImmediate(visual);
    }

    private void Cleanup(System.Action callback)
    {
        try { callback(); }
        catch (System.Exception exception) { UnityEngine.Debug.LogException(exception, this); }
    }

    private void Log(string message)
    {
        if (!debug) return;
        UnityEngine.Debug.Log($"[ItemEquipComponent] {message}", this);
    }

}

