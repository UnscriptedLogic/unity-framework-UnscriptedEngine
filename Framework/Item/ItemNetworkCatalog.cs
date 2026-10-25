using System;
using UnityEngine;

namespace UnscriptedEngine.Item
{
    /// <summary>Generated Resources catalog keeps stable item IDs resolvable on every peer, including late joiners.</summary>
    public sealed class ItemNetworkCatalog : ScriptableObject
    {
        [SerializeField] private ItemDataSO[] items = Array.Empty<ItemDataSO>();
        public static ItemDataSO Resolve(string id)
        {
            var catalog = Resources.Load<ItemNetworkCatalog>("UnscriptedItemCatalog");
            if (catalog == null || string.IsNullOrEmpty(id)) return null;
            foreach (var item in catalog.items)
                if (item != null && item.NetworkId == id) return item;
            return null;
        }
    }
}
