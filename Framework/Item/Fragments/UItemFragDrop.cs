using System;
using UnityEngine;
using UnscriptedEngine.Item;

namespace UnscriptedEngine
{
    [Serializable]
    public class UItemFragDrop : UItemFragment
    {
        public override string ID => "drop";
        [SerializeField, Tooltip("Pickup prefab spawned when this item leaves inventory. Register it with NetworkManager for multiplayer.")]
        private UWorldItem worldPrefab;
        public UWorldItem WorldPrefab => worldPrefab;
    }
}
