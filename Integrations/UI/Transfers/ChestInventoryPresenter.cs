using Unity.Netcode;
using UnityEngine;

namespace UnscriptedEngine.UI
{
    /// <summary>Optional local presentation adapter for a chest. No transfer authority is granted here.</summary>
    [RequireComponent(typeof(UChest))]
    public sealed class ChestInventoryPresenter : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Panel to open for the local interactor. May be shared between chests.")]
        private InventoryTransferPanel panel;
        private UChest chest;
        private bool openedPanel;

        private void OnEnable()
        {
            chest = GetComponent<UChest>();
            chest.InventoryOpenRequested -= OpenInventory;
            chest.InventoryOpenRequested += OpenInventory;
        }

        private void OnDisable()
        {
            if (chest != null) chest.InventoryOpenRequested -= OpenInventory;
            if (openedPanel && panel != null && chest != null && panel.Displays(chest.Inventory)) panel.Close();
            openedPanel = false;
        }

        private void OpenInventory(GameObject interactor, UInventoryComponent container)
        {
            if (panel == null || interactor == null) return;
            NetworkManager manager = NetworkManager.Singleton;
            if (manager != null && manager.IsListening)
            {
                NetworkObject source = interactor.GetComponentInParent<NetworkObject>();
                if (source == null || !source.IsOwner) return;
            }
            UInventoryComponent player = interactor.GetComponentInChildren<UInventoryComponent>();
            if (player == null || player == container) return;
            panel.Open(player, container);
            openedPanel = true;
        }
    }
}
