using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

namespace UnscriptedEngine
{
    public enum ChestOpenMode { SplitInventory, BurstOpen }

    /// <summary>Extensible container interaction. Storage and presentation remain independent.</summary>
    [RequireComponent(typeof(UInventoryComponent), typeof(UInteractableComponent))]
    public class UChest : UObject
    {
        [SerializeField]
        [Tooltip("Browse contents in two inventory views, or release physical world pickups.")]
        private ChestOpenMode openMode;
        [SerializeField]
        [Tooltip("Loot release component used by Burst Open mode.")]
        private InventoryLootRelease lootRelease;
        [SerializeField]
        [Tooltip("Called after a successful open. Connect lid animation, audio or effects.")]
        private UnityEvent onOpened = new UnityEvent();
        [Header("Contents")]
        [SerializeField]
        [Tooltip("Exact contents, independent chance rolls, or a custom loot provider.")]
        private ChestContentsMode contentsMode;
        [SerializeField]
        [Tooltip("Exact items and quantities. Duplicate definitions are combined.")]
        private List<ChestItemAmount> predefinedItems = new List<ChestItemAmount>();
        [SerializeField]
        [Tooltip("Shared table rolled once for this chest in Chance mode.")]
        private ChestLootTable lootTable;
        [SerializeField]
        [Tooltip("Custom recipe used in Custom mode. May inspect chest and interactor context.")]
        private ChestLootProvider customProvider;

        private UInventoryComponent inventory;
        private UInteractableComponent interaction;
        private bool opening;
        private bool preparingContents;
        private bool contentsInitialized;
        private List<InventorySlot> generatedContents;
        private string generationError;
        public ChestOpenMode OpenMode => openMode;
        public ChestContentsMode ContentsMode => contentsMode;
        public bool ContentsInitialized => contentsInitialized;
        public UInventoryComponent Inventory => inventory != null ? inventory : inventory = GetComponent<UInventoryComponent>();
        public event Action<GameObject, UInventoryComponent> InventoryOpenRequested;

        protected override void OnEnable()
        {
            base.OnEnable();
            interaction = GetComponent<UInteractableComponent>();
            interaction.OnButtonPressedServer -= HandleServerInteraction;
            interaction.OnButtonPressedClient -= HandleClientInteraction;
            interaction.OnButtonPressedServer += HandleServerInteraction;
            interaction.OnButtonPressedClient += HandleClientInteraction;
        }

        protected override void OnDisable()
        {
            if (interaction != null)
            {
                interaction.OnButtonPressedServer -= HandleServerInteraction;
                interaction.OnButtonPressedClient -= HandleClientInteraction;
            }
            base.OnDisable();
        }

        private void HandleServerInteraction(UInteractableComponent.UInteractArgs args)
        {
            if (openMode == ChestOpenMode.BurstOpen) TryOpen(args.InteractorSource);
            else if (CanOpen(args.InteractorSource) && !TryInitializeContents(args.InteractorSource, out string error))
                Debug.LogWarning(error, this);
        }

        private void HandleClientInteraction(UInteractableComponent.UInteractArgs args)
        {
            if (openMode == ChestOpenMode.SplitInventory) TryOpen(args.InteractorSource);
        }

        /// <summary>Called after interaction validation. Direct callers must enforce game-specific access rules.</summary>
        public bool TryOpen(GameObject interactor)
        {
            if (!isActiveAndEnabled || opening || preparingContents || !CanOpen(interactor)) return false;
            opening = true;
            try
            {
                Inventory.Initialize();
                if (HasContentsAuthority && !TryInitializeContents(interactor, out string error))
                {
                    Debug.LogWarning(error, this);
                    return false;
                }
                if (openMode == ChestOpenMode.BurstOpen)
                {
                    if (lootRelease == null || lootRelease.Release() == 0) return false;
                }
                else
                {
                    if (interactor == null || InventoryOpenRequested == null) return false;
                    InventoryOpenRequested.Invoke(interactor, Inventory);
                }
                OnOpened(interactor);
                onOpened.Invoke();
                return true;
            }
            finally { opening = false; }
        }

        protected virtual bool CanOpen(GameObject interactor) => true;
        protected virtual void OnOpened(GameObject interactor) { }

        private bool HasContentsAuthority
        {
            get
            {
                NetworkManager manager = Unity.Netcode.NetworkManager.Singleton;
                return manager == null || !manager.IsListening || (Inventory.IsSpawned && Inventory.IsServer);
            }
        }

        /// <summary>Generate once and atomically add the result. Capacity failures reuse the same roll on retry.</summary>
        public bool TryInitializeContents(GameObject interactor, out string error)
        {
            error = null;
            if (!HasContentsAuthority) { error = "Chest contents require server/offline authority."; return false; }
            if (contentsInitialized) return true;
            if (preparingContents) { error = "Chest contents are already being initialized."; return false; }
            if (generationError != null) { error = generationError; return false; }
            preparingContents = true;
            try
            {
                Inventory.Initialize();
                if (generatedContents == null)
                {
                    try
                    {
                        List<InventorySlot> output = new List<InventorySlot>();
                        GenerateContents(interactor, output);
                        generatedContents = CombineContents(output);
                    }
                    catch (Exception exception)
                    {
                        generationError = "Chest loot generation failed: " + exception.Message;
                        error = generationError;
                        return false;
                    }
                }
                UInventoryComponent.OperationResult result = Inventory.TryAddItems(generatedContents);
                if (!result.Success) { error = result.Message; return false; }
                contentsInitialized = true;
                generatedContents = null;
                return true;
            }
            finally { preparingContents = false; }
        }

        /// <summary>Override for per-instance gameplay recipes, or assign a shared ChestLootProvider asset.</summary>
        protected virtual void GenerateContents(GameObject interactor, List<InventorySlot> output)
        {
            switch (contentsMode)
            {
                case ChestContentsMode.Predefined:
                    foreach (ChestItemAmount entry in predefinedItems) output.Add(new InventorySlot(entry.Item, entry.Quantity));
                    break;
                case ChestContentsMode.Chance:
                    if (lootTable == null) throw new InvalidOperationException("Assign a chest loot table.");
                    lootTable.Generate(this, interactor, output);
                    break;
                case ChestContentsMode.Custom:
                    if (customProvider == null) throw new InvalidOperationException("Assign a custom loot provider or override GenerateContents.");
                    customProvider.Generate(this, interactor, output);
                    break;
                default: throw new InvalidOperationException("Unknown chest contents mode.");
            }
        }

        private static List<InventorySlot> CombineContents(List<InventorySlot> output)
        {
            List<InventorySlot> combined = new List<InventorySlot>();
            Dictionary<ItemDataSO, int> indices = new Dictionary<ItemDataSO, int>();
            foreach (InventorySlot entry in output)
            {
                if (entry == null || entry.Item == null || entry.Quantity < 1)
                    throw new InvalidOperationException("Loot needs valid item definitions and positive quantities.");
                if (indices.TryGetValue(entry.Item, out int index))
                    combined[index] = new InventorySlot(entry.Item, checked(combined[index].Quantity + entry.Quantity));
                else
                {
                    indices.Add(entry.Item, combined.Count);
                    combined.Add(entry);
                }
            }
            return combined;
        }
    }
}
