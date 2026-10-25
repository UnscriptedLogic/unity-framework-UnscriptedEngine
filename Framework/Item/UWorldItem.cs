using Unity.Netcode;
using UnityEngine;
using Unity.Collections;
using UnityEngine.Serialization;

namespace UnscriptedEngine.Item
{
    /// <summary>Server-authoritative pickup. Contents and object lifetime replicate to every peer.</summary>
    [RequireComponent(typeof(NetworkObject), typeof(UInteractableComponent))]
    public class UWorldItem : UObject
    {
        [SerializeField, FormerlySerializedAs("itemDataSo")] private ItemDataSO itemData;
        [SerializeField, Min(1)] private int quantity = 1;
        [SerializeField, Tooltip("Action performed when interaction completes. None leaves the item unchanged.")]
        private WorldItemInteraction defaultBehaviour = WorldItemInteraction.PickUp;

        private UInteractableComponent interaction;
        private bool collecting;
        private readonly NetworkVariable<FixedString64Bytes> replicatedItemId = new();
        private readonly NetworkVariable<int> replicatedQuantity = new(1);

        protected override void OnSpawned()
        {
            base.OnSpawned();
            replicatedItemId.OnValueChanged += HandleItemChanged;
            replicatedQuantity.OnValueChanged += HandleQuantityChanged;
            if (IsServer)
            {
                replicatedItemId.Value = itemData != null ? itemData.NetworkId : "";
                replicatedQuantity.Value = quantity;
            }
            else
            {
                HandleItemChanged(default, replicatedItemId.Value);
                quantity = replicatedQuantity.Value;
            }
        }

        protected override void OnDespawned()
        {
            replicatedItemId.OnValueChanged -= HandleItemChanged;
            replicatedQuantity.OnValueChanged -= HandleQuantityChanged;
            base.OnDespawned();
        }

        private void HandleItemChanged(FixedString64Bytes previous, FixedString64Bytes current)
        {
            if (!IsServer && current.Length > 0) itemData = ItemNetworkCatalog.Resolve(current.ToString());
        }
        private void HandleQuantityChanged(int previous, int current) { if (!IsServer) quantity = current; }

        public ItemDataSO ItemData => itemData;
        public int Quantity => quantity;
        public WorldItemInteraction DefaultBehaviour => defaultBehaviour;

        /// <summary>Configure an unspawned pickup before network spawning.</summary>
        public void Initialize(ItemDataSO item, int amount = 1)
        {
            if (IsSpawned || collecting) throw new System.InvalidOperationException("Cannot initialize an active pickup.");
            if (item == null) throw new System.ArgumentNullException(nameof(item));
            if (amount < 1) throw new System.ArgumentOutOfRangeException(nameof(amount));
            itemData = item;
            quantity = amount;
            replicatedItemId.Reset(item.NetworkId ?? "");
            replicatedQuantity.Reset(amount);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            interaction = GetComponent<UInteractableComponent>();
            if (interaction != null) interaction.OnButtonPressedServer += HandleInteraction;
        }

        protected override void OnDisable()
        {
            if (interaction != null) interaction.OnButtonPressedServer -= HandleInteraction;
            base.OnDisable();
        }

        private void HandleInteraction(UInteractableComponent.UInteractArgs args)
        {
            switch (defaultBehaviour)
            {
                case WorldItemInteraction.PickUp:
                    HandlePickup(args);
                    break;
                case WorldItemInteraction.None:
                default:
                    break;
            }
        }

        private void HandlePickup(UInteractableComponent.UInteractArgs args)
        {
            TryPickup(args.InteractorSource);
        }

        /// <summary>Server/offline transfer. Clients enter through UInteractableComponent's validated request.</summary>
        public bool TryPickup(GameObject source)
        {
            var manager = IsSpawned ? NetworkManager : Unity.Netcode.NetworkManager.Singleton;
            if (collecting || !isActiveAndEnabled || itemData == null || quantity < 1
                || source == null || (IsSpawned && !IsServer)
                || (manager != null && manager.IsListening && (!IsSpawned || !IsServer))) return false;
            UInventoryComponent inventory = source.GetComponentInChildren<UInventoryComponent>();
            if (inventory == null) return false;
            collecting = true;
            bool added = false;
            try { added = inventory.TryAddItem(itemData, quantity).Success; }
            finally { if (!added) collecting = false; }
            if (!added) return false;
            // Consume the world object only after the complete quantity was accepted.
            if (IsSpawned) NetworkObject.Despawn(true);
            else
            {
                gameObject.SetActive(false);
                Destroy(gameObject);
            }
            return true;
        }
    }
}
