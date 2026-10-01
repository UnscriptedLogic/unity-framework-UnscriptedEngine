using System;
using UnityEngine;

namespace UnscriptedEngine
{
    [Serializable]
    public class UItemFragDetails : UItemFragment
    {
        public override string ID { get; } = "details";
        
        [field: SerializeField] public string Name { get; set; }
        [field: SerializeField] public string Description { get; set; }
    }
}
