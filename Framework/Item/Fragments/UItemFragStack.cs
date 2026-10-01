using System;
using UnityEngine;
using UnscriptedEngine;

namespace UnscriptedEngine
{
    [Serializable]

    public class UItemFragStack : UItemFragment
    {
        public override string ID { get; } = "stack";

        [SerializeField] private int _stackSize = 1;

        public int StackSize => _stackSize;
    }
}