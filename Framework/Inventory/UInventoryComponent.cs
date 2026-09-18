using System;
using Framework.Components;
using Unity.Netcode;
using UnityEngine;

namespace Framework.Inventory
{
    public sealed class UInventoryComponent : UObjectComponent
    {
        [SerializeField] private UInventoryContainer uInventory = new();
        [SerializeField, Min(0.1f)] private float pickupRange = 2f;
        [SerializeField] private int selectedSlot;
        [SerializeField, Min(1)] private int dropAmount = 1;
        [SerializeField] private float dropDistance = 1.5f;
        public event Action<int> SlotChanged;
        public UInventoryContainer UInventory => uInventory;
        public int SelectedSlot => selectedSlot;

        public void SetSelectedSlot(int slot)
        {
            selectedSlot = Mathf.Clamp(slot, 0, uInventory.Capacity - 1);
        }

        /// Finds the closest world item locally, then asks the server to validate and complete the pickup.
        public void RequestPickUp()
        {
            UItemWorldEntity target = FindClosestWorldItem();
            if (target == null || target.NetworkObject == null || !target.NetworkObject.IsSpawned)
            {
                return;
            }

            RequestPickUp(target.NetworkObject, dropAmount);
        }

        public void RequestPickUp(NetworkObject target, int amount = 1)
        {
            if (target == null || !target.IsSpawned || amount <= 0)
            {
                return;
            }

            NetworkObjectReference targetReference = new(target);
            if (IsServer)
            {
                HandlePickUp(targetReference, amount);
            }
            else
            {
                RequestPickUpServerRpc(targetReference, amount);
            }
        }

        /// Requests one stack portion from the currently selected slot to be dropped in front of the owner.
        public void RequestDrop()
        {
            RequestDrop(selectedSlot, dropAmount);
        }

        public void RequestDrop(int slot, int amount = 1)
        {
            if (amount <= 0)
            {
                return;
            }

            if (IsServer)
            {
                HandleDrop(slot, amount);
            }
            else
            {
                RequestDropServerRpc(slot, amount);
            }
        }

        public bool TryAdd(UItemStack stack, out UItemStack remainder)
        {
            bool fullyAdded = uInventory.TryAdd(stack, out remainder);
            for (int i = 0; i < uInventory.Capacity; i++) SlotChanged?.Invoke(i);
            return fullyAdded;
        }

        public UItemStack Remove(int slot, int amount)
        {
            UItemStack result = uInventory.Remove(slot, amount);
            SlotChanged?.Invoke(slot);
            return result;
        }

        public void SetSlot(int slot, UItemStack stack)
        {
            uInventory.SetSlot(slot, stack);
            SlotChanged?.Invoke(slot);
        }

        [ServerRpc]
        private void RequestPickUpServerRpc(NetworkObjectReference targetReference, int amount)
        {
            HandlePickUp(targetReference, amount);
        }

        [ServerRpc]
        private void RequestDropServerRpc(int slot, int amount)
        {
            HandleDrop(slot, amount);
        }

        private void HandlePickUp(NetworkObjectReference targetReference, int amount)
        {
            if (!targetReference.TryGet(out NetworkObject targetObject) || targetObject == null || !targetObject.IsSpawned)
            {
                return;
            }

            UItemWorldEntity worldItem = targetObject.GetComponent<UItemWorldEntity>();
            if (worldItem == null || Vector3.Distance(transform.position, worldItem.transform.position) > pickupRange + worldItem.PickupRadius)
            {
                return;
            }

            UItemStack requested = worldItem.Stack.Clone(Mathf.Min(amount, worldItem.Stack.Quantity));
            if (requested.IsEmpty)
            {
                return;
            }

            TryAdd(requested, out UItemStack remainder);
            int accepted = requested.Quantity - remainder.Quantity;
            if (accepted <= 0)
            {
                return;
            }

            worldItem.Take(accepted);
            if (worldItem.Stack.IsEmpty)
            {
                targetObject.Despawn(true);
            }
        }

        private void HandleDrop(int slot, int amount)
        {
            if (slot < 0 || slot >= uInventory.Capacity || amount <= 0)
            {
                return;
            }

            UItemStack stack = uInventory.Slots[slot];
            if (stack == null || stack.IsEmpty || stack.Definition.DroppedPrefab == null)
            {
                return;
            }

            UItemStack dropped = Remove(slot, Mathf.Min(amount, stack.Quantity));
            if (dropped.IsEmpty)
            {
                return;
            }

            GameObject droppedObject = Instantiate(dropped.Definition.DroppedPrefab, transform.position + transform.forward * dropDistance, transform.rotation);
            UItemWorldEntity worldItem = droppedObject.GetComponent<UItemWorldEntity>();
            NetworkObject networkObject = droppedObject.GetComponent<NetworkObject>();
            if (worldItem == null || networkObject == null)
            {
                Destroy(droppedObject);
                TryAdd(dropped, out _);
                return;
            }

            worldItem.Initialize(dropped);
            networkObject.Spawn();
        }

        private UItemWorldEntity FindClosestWorldItem()
        {
            Collider[] colliders = Physics.OverlapSphere(transform.position, pickupRange);
            UItemWorldEntity closest = null;
            float closestSqrDistance = float.MaxValue;

            for (int i = 0; i < colliders.Length; i++)
            {
                UItemWorldEntity candidate = colliders[i].GetComponentInParent<UItemWorldEntity>();
                if (candidate == null || candidate.Stack.IsEmpty)
                {
                    continue;
                }

                float sqrDistance = (candidate.transform.position - transform.position).sqrMagnitude;
                if (sqrDistance < closestSqrDistance)
                {
                    closest = candidate;
                    closestSqrDistance = sqrDistance;
                }
            }

            return closest;
        }
    }
}
