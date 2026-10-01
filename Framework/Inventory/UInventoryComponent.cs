using System;
using System.Collections.Generic;
using Framework;
using Unity.Netcode;
using UnityEngine;

namespace UnscriptedEngine
{
    public class UInventoryComponent : UObjectComponent
    {
        [SerializeField] private int inventorySize = 10;
        
        private Dictionary<ItemDataSO, int> itemToSlotDict;
        private InventorySlot[] inventorySlotsArray;
        private List<int> freeSlotIndexesList;

        protected override void BeginPlay()
        {
            base.BeginPlay();
            
            Initialize();
        }

        public void Initialize()
        {
            itemToSlotDict = new Dictionary<ItemDataSO, int>();
            inventorySlotsArray = new InventorySlot[inventorySize];
            
            freeSlotIndexesList = new List<int>();
            // Keep the lowest free index at the end for O(1) lookup and removal.
            for (int i = inventorySlotsArray.Length - 1; i >= 0; i--)
            {
                freeSlotIndexesList.Add(i);
            }
        }
        
        public OperationResult TryAddItem(ItemDataSO itemDataSo)
        {
            OperationResult operationResult = new OperationResult
            {
                Success = false,
                SlotIndex = -1,
                Message = "No empty inventory slot available"
            };

            if (itemDataSo == null)
            {
                operationResult.Message = "Item data is null";
                return operationResult;
            }

            if (HasItem(itemDataSo, out int existingIndex))
            {
                operationResult.SlotIndex = existingIndex;
                operationResult.Message = "Item already exists in inventory";
                return operationResult;
            }

            if (HasEmptySlot(out int emptyIndex))
            {
                freeSlotIndexesList.RemoveAt(freeSlotIndexesList.Count - 1);
                itemToSlotDict.Add(itemDataSo, emptyIndex);
                
                operationResult.Success = true;
                operationResult.SlotIndex = emptyIndex;
                operationResult.Message = "Item added successfully";

                EventBus.Publish(new EB_InventoryAddedItem()
                {
                    Inventory = this,
                    ItemAdded = itemDataSo
                });
            }
            
            return operationResult;
        }

        public bool HasItem(ItemDataSO itemDataSo)
        {
            return HasItem(itemDataSo, out _);
        }

        public bool HasItem(ItemDataSO itemDataSo, out int slotIndex)
        {
            if (itemDataSo != null && itemToSlotDict.TryGetValue(itemDataSo, out slotIndex))
            {
                return true;
            }

            slotIndex = -1;
            return false;
        }

        public int InventorySize => inventorySize;
        public IReadOnlyDictionary<ItemDataSO, int> Items => itemToSlotDict;

        public bool HasEmptySlot(out int emptyIndex)
        {
            if (freeSlotIndexesList.Count > 0)
            {
                emptyIndex = freeSlotIndexesList[^1];
                return true;
            }
            
            emptyIndex = -1;
            return false;
        }
        
        

        public struct OperationResult
        {
            public bool Success;
            public int SlotIndex;
            public string Message;
        }
        
        public class EB_InventoryAddedItem
        {
            public UInventoryComponent Inventory;
            public ItemDataSO ItemAdded;
        }
    }
}
