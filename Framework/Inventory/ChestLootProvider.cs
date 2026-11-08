using System.Collections.Generic;
using UnityEngine;

namespace UnscriptedEngine
{
    /// <summary>Shared loot recipe. Append results; never mutate inventory or keep per-chest state here.</summary>
    public abstract class ChestLootProvider : ScriptableObject
    {
        public abstract void Generate(UChest chest, GameObject interactor, List<InventorySlot> output);
    }
}
