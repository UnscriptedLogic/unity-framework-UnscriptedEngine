using System;
using System.Collections.Generic;

namespace UnscriptedEngine
{
    public interface IInventory
    {
        int InventorySize { get; }
        // Sparse, zero-based slots. Missing keys represent empty slots.
        IReadOnlyDictionary<int, InventorySlot> Items { get; }
        // Raise after initialization and every content or capacity change.
        event Action Changed;
    }
}
