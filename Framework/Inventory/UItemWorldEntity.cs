using Unity.Netcode;
using UnityEngine;

namespace Framework.Inventory
{
    public sealed class UItemWorldEntity : NetworkBehaviour
    {
        [SerializeField] private UItemStack stack = new(null, 0);
        [SerializeField] private float pickupRadius = 1.25f;
        private readonly NetworkVariable<int> networkQuantity = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public UItemStack Stack => stack;
        public float PickupRadius => pickupRadius;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            networkQuantity.OnValueChanged += HandleQuantityChanged;

            if (!IsServer)
            {
                stack?.SetQuantity(networkQuantity.Value);
            }
        }

        public override void OnNetworkDespawn()
        {
            networkQuantity.OnValueChanged -= HandleQuantityChanged;
            base.OnNetworkDespawn();
        }

        public void Initialize(UItemStack value)
        {
            stack = value?.Clone() ?? UItemStack.Empty();
            if (IsServer)
            {
                networkQuantity.Value = stack.Quantity;
            }
        }

        public UItemStack Take(int amount)
        {
            UItemStack taken = stack.Clone(Mathf.Min(amount, stack.Quantity));
            stack.Remove(taken.Quantity);
            if (IsServer)
            {
                networkQuantity.Value = stack.Quantity;
            }
            return taken;
        }

        private void HandleQuantityChanged(int previous, int current)
        {
            if (!IsServer && stack != null)
            {
                stack.SetQuantity(current);
            }
        }
    }
}
