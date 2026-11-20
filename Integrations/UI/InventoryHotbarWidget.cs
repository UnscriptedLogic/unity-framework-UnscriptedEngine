using System.Collections.Generic;
using UnityEngine;

namespace UnscriptedEngine.UI
{
    /// <summary>Passive HUD showing the first inventory slots without compacting empty slots.</summary>
    public class InventoryHotbarWidget : UWidgetComponent
    {
        [SerializeField, Tooltip("Optional inventory component. The player HUD can bind this at runtime.")]
        private Component inventoryComponent;
        [SerializeField, Min(1)] private int slotCount = 3;
        [SerializeField, Tooltip("Replace with any prefab containing InventoryHotbarSlotWidget. Uses bundled default when unassigned.")]
        private InventoryHotbarSlotWidget slotPrefab;

        private IInventory inventory;
        private ItemEquipComponent equipment;
        private readonly List<InventoryHotbarSlotWidget> cells = new();
        private InventoryHotbarSlotWidget builtPrefab;
        private InventoryHotbarSlotWidget lastSlotPrefab;
        private ItemEquipComponent[] hands;
        private int[] lastHandSlots;
        private int lastSlotCount;
        public override bool Focusable => false;
        public int SlotCount => Mathf.Max(1, slotCount);
        public int VisibleSlotCount { get; private set; }
        public IInventory Inventory => inventory;
        public InventoryHotbarSlotWidget SlotPrefab => slotPrefab;

        public void SetSlotPrefab(InventoryHotbarSlotWidget prefab)
        {
            slotPrefab = prefab;
            Refresh();
        }

        public void Bind(IInventory source, ItemEquipComponent selectedEquipment = null)
        {
            Unsubscribe();
            inventory = source;
            inventoryComponent = source as Component;
            equipment = selectedEquipment;
            hands = equipment != null ? equipment.GetComponents<ItemEquipComponent>() : null;
            lastHandSlots = hands != null ? new int[hands.Length] : null;
            if (isActiveAndEnabled && inventory != null) inventory.Changed += Refresh;
            Refresh();
        }

        public void SetSlotCount(int count)
        {
            slotCount = Mathf.Max(1, count);
            Refresh();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            var group = GetComponent<CanvasGroup>();
            group.interactable = group.blocksRaycasts = false;
            if (inventory == null) inventory = inventoryComponent as IInventory;
            Unsubscribe();
            if (inventory != null) inventory.Changed += Refresh;
            Refresh();
        }

        protected override void OnDisable() { Unsubscribe(); base.OnDisable(); }
        protected override void OnDestroy() { Unsubscribe(); base.OnDestroy(); }
        private void Unsubscribe() { if (inventory != null) inventory.Changed -= Refresh; }
        private bool HasInventory => inventory != null && (!(inventory is Object value) || value != null);

        private void LateUpdate()
        {
            if (lastSlotCount != SlotCount || lastSlotPrefab != slotPrefab || (!HasInventory && VisibleSlotCount != 0)) Refresh();
            if (hands == null) return;
            for (int i = 0; i < hands.Length; i++)
                if (lastHandSlots[i] != SelectedSlot(hands[i]))
                {
                    RefreshSelection();
                    break;
                }
        }

        private int SelectedSlot(ItemEquipComponent hand) => hand != null && hand.Inventory == inventory ? hand.SelectedSlotIndex : -1;

        public void Refresh()
        {
            if (!isActiveAndEnabled) return;
            lastSlotCount = SlotCount;
            lastSlotPrefab = slotPrefab;
            VisibleSlotCount = HasInventory ? Mathf.Min(SlotCount, Mathf.Max(0, inventory.InventorySize)) : 0;
            InventoryHotbarSlotWidget prefab = slotPrefab != null ? slotPrefab
                : Resources.Load<GameObject>("UnscriptedEngine/InventoryHotbarSlot")?.GetComponent<InventoryHotbarSlotWidget>();
            if (builtPrefab != prefab)
            {
                foreach (var cell in cells)
                {
                    if (cell == null) continue;
                    cell.gameObject.SetActive(false);
                    cell.transform.SetParent(null, false);
                    if (Application.isPlaying) Destroy(cell.gameObject);
                    else DestroyImmediate(cell.gameObject);
                }
                cells.Clear();
                builtPrefab = prefab;
            }
            if (VisibleSlotCount > 0 && prefab == null)
            {
                Debug.LogError("Inventory hotbar requires a slot prefab with InventoryHotbarSlotWidget.", this);
                VisibleSlotCount = 0;
            }
            for (int i = 0; i < VisibleSlotCount; i++)
            {
                if (i == cells.Count)
                {
                    var created = Instantiate(prefab, transform, false);
                    created.name = $"Slot {i + 1}";
                    cells.Add(created);
                }
                var cell = cells[i];
                cell.gameObject.SetActive(true);
                InventorySlot slot = null;
                if (inventory.Items != null) inventory.Items.TryGetValue(i, out slot);
                cell.SetSlot(i, slot);
            }
            for (int i = VisibleSlotCount; i < cells.Count; i++) cells[i].gameObject.SetActive(false);
            RefreshSelection();
        }

        private void RefreshSelection()
        {
            for (int i = 0; i < VisibleSlotCount; i++)
            {
                cells[i].SetSelected(i == SelectedSlot(equipment));
                cells[i].SetHandNumber(0);
            }

            if (hands == null) return;
            for (int i = 0; i < hands.Length; i++)
            {
                int slot = lastHandSlots[i] = SelectedSlot(hands[i]);
                if (hands.Length > 1 && slot >= 0 && slot < VisibleSlotCount)
                    cells[slot].SetHandNumber(i + 1);
            }
        }

        public static InventoryHotbarWidget CreateDefault(Transform parent)
        {
            var prefab = Resources.Load<GameObject>("UnscriptedEngine/InventoryHotbar");
            return prefab != null ? Instantiate(prefab, parent, false).GetComponent<InventoryHotbarWidget>() : null;
        }
    }
}
