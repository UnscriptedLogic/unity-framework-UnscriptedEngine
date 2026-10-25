using Unity.Netcode;
using UnityEngine;

namespace UnscriptedEngine.Item
{
    /// <summary>Server/offline pickup. Inventory replication remains the game's responsibility.</summary>
    [RequireComponent(typeof(NetworkObject), typeof(UInteractableComponent))]
    public class UWorldItem : UObject
    {
        [SerializeField] private ItemDataSO itemData;
        [SerializeField, Min(1)] private int quantity = 1;
        [SerializeField, Tooltip("Action performed when interaction completes. None leaves the item unchanged.")]
        private WorldItemInteraction defaultBehaviour = WorldItemInteraction.PickUp;

        private UInteractableComponent interaction;
        private bool collecting;

        public ItemDataSO ItemData => itemData;
        public int Quantity => quantity;
        public WorldItemInteraction DefaultBehaviour => defaultBehaviour;

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
            if (collecting || !isActiveAndEnabled || itemData == null || quantity < 1
                || args.InteractorSource == null || (IsSpawned && !IsServer)) return;
            UInventoryComponent inventory = args.InteractorSource.GetComponentInChildren<UInventoryComponent>();
            if (inventory == null) return;
            collecting = true;
            bool added = false;
            try { added = inventory.TryAddItem(itemData, quantity).Success; }
            finally { if (!added) collecting = false; }
            if (!added) return;
            // Consume the world object only after the complete quantity was accepted.
            if (IsSpawned) NetworkObject.Despawn(true);
            else
            {
                gameObject.SetActive(false);
                Destroy(gameObject);
            }
        }
    }
}
