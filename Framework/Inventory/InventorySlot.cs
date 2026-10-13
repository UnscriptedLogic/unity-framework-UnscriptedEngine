namespace UnscriptedEngine
{
    // Immutable runtime snapshot. Inventory operations replace it when quantity changes.
    public sealed class InventorySlot
    {
        public ItemDataSO Item { get; }
        public int Quantity { get; }

        public InventorySlot(ItemDataSO item, int quantity)
        {
            Item = item;
            Quantity = quantity;
        }
    }
}
