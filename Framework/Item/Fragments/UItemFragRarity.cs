using System;
using UnityEngine;

namespace UnscriptedEngine
{
    [Serializable]
    public class UItemFragRarity : UItemFragment
    {
        public override string ID { get; } = "rarity";

        [field: SerializeField] public string Rarity { get; set; } = "Common";
    }
}
