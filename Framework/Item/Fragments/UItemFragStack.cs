using System;
using UnityEngine;
using UnscriptedEngine;

namespace UnscriptedEngine
{
    [Serializable]

    public class UItemFragStack : UItemFragment
    {
        public override string ID { get; } = "stack";

        [SerializeField, Min(1), Tooltip("Maximum number of items in one inventory slot.")]
        private int _stackSize = 1;

        public int StackSize => _stackSize;
    }
}
