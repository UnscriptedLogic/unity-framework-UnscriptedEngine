using Unity.Netcode;
using UnityEngine;

namespace UnscriptedEngine.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UInventoryComponent), typeof(UInteractableComponent))]
    public class InspectableInventory : MonoBehaviour
    {
        [SerializeField, Tooltip("Inventory UI template shown through the local player's canvas.")]
        private InventoryPresenter panel;

        private UInteractableComponent interaction;
        private UInventoryComponent inventory;
        private InventoryPresenter openedPanel;

        protected virtual void OnEnable()
        {
            interaction = GetComponent<UInteractableComponent>();
            inventory = GetComponent<UInventoryComponent>();
            interaction.InteractedLocally += Inspect;
        }

        protected virtual void OnDisable()
        {
            if (interaction != null) interaction.InteractedLocally -= Inspect;
            CloseInventory();
        }

        private void Inspect(UInteractableComponent.UInteractArgs args) => OpenInventory(args.InteractorSource, inventory);

        protected void OpenInventory(GameObject interactor, UInventoryComponent target)
        {
            if (panel == null || interactor == null || target == null) return;
            UPawn pawn = interactor.GetComponentInParent<UPawn>();
            UController owner = pawn != null ? pawn.Controller : interactor.GetComponentInParent<UController>();
            NetworkManager manager = NetworkManager.Singleton;
            if (manager != null && manager.IsListening)
            {
                NetworkObject source = interactor.GetComponentInParent<NetworkObject>();
                if (source == null || !source.IsOwner) return;
            }

            UPawn inventoryOwner = pawn != null ? pawn : owner?.Pawn;
            GameObject actor = inventoryOwner != null ? inventoryOwner.gameObject : interactor;
            UInventoryComponent player = actor.GetComponentInChildren<UInventoryComponent>();
            if (!panel.CanOpen(player, target)) return;

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
                Debug.LogWarning("Inventory inspection requires the interactor's initialized UCanvasController.", this);
                return;
            }
            if (openedPanel != null && openedPanel.Controller == ui)
            {
                openedPanel.Open(player, target);
                ui.BringToFront(openedPanel);
                return;
            }

            CloseInventory();
            target.Initialize();
            if (player != null) player.Initialize();
            openedPanel = ui.Show(panel, screen => screen.Open(player, target));
            openedPanel.Closed += HandlePanelClosed;
        }

        protected void CloseInventory()
        {
            if (openedPanel == null) return;
            InventoryPresenter current = openedPanel;
            current.Close();
            if (ReferenceEquals(openedPanel, current)) HandlePanelClosed();
        }

        private void HandlePanelClosed()
        {
            openedPanel.Closed -= HandlePanelClosed;
            openedPanel = null;
            OnInventoryClosed();
        }

        protected virtual void OnInventoryClosed() { }
    }
}
