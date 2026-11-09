using Unity.Netcode;
using UnityEngine;

namespace UnscriptedEngine.UI
{
    /// <summary>Optional local presentation adapter for a chest. No transfer authority is granted here.</summary>
    [RequireComponent(typeof(UChest))]
    public sealed class ChestInventoryPresenter : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Panel template shown as a managed modal copy through the local player's UCanvasController.")]
        private InventoryTransferPanel panel;
        private UChest chest;
        private InventoryTransferPanel openedPanel;

        private void OnEnable()
        {
            chest = GetComponent<UChest>();
            chest.InventoryOpenRequested -= OpenInventory;
            chest.InventoryOpenRequested += OpenInventory;
        }

        private void OnDisable()
        {
            if (chest != null) chest.InventoryOpenRequested -= OpenInventory;
            if (openedPanel != null) openedPanel.Close();
            openedPanel = null;
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
            UPawn pawn = interactor.GetComponentInParent<UPawn>();
            UController owner = pawn != null ? pawn.Controller : interactor.GetComponentInParent<UController>();
            UCanvasController ui = null;
            if (owner != null)
                foreach (UCanvasController candidate in FindObjectsByType<UCanvasController>(FindObjectsInactive.Exclude))
                    if (candidate.OwningController == owner && candidate.isActiveAndEnabled)
                    {
                        ui = candidate;
                        break;
                    }
            if (ui == null)
            {
                Debug.LogWarning("Chest inventory requires the interactor's initialized UCanvasController.", this);
                return;
            }
            if (openedPanel != null && openedPanel.Controller == ui)
            {
                openedPanel.Open(player, container);
                ui.BringToFront(openedPanel);
                return;
            }
            if (openedPanel != null) openedPanel.Close();
            openedPanel = ui.Show(panel, screen => screen.Open(player, container));
        }
    }
}
