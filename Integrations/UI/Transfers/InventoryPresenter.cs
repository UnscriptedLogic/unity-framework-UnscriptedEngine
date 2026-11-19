using System;

namespace UnscriptedEngine.UI
{
    /// <summary>Root component for a UI prefab that presents an inventory.</summary>
    public abstract class InventoryPresenter : UCanvasComponent
    {
        public event Action Closed;

        public virtual bool CanOpen(UInventoryComponent player, UInventoryComponent inventory) => inventory != null;

        public abstract void Open(UInventoryComponent player, UInventoryComponent inventory);

        public new virtual void Close()
        {
            if (Controller != null) base.Close();
            else gameObject.SetActive(false);
        }

        public override bool HandleBack()
        {
            if (!Dismissible) return false;
            Close();
            return true;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            Closed?.Invoke();
        }
    }
}
