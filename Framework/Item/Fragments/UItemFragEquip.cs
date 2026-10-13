using System;
using UnityEngine;

namespace UnscriptedEngine
{
    [Serializable]
    public class UItemFragEquip : UItemFragment
    {
        public override string ID => "equip";
        [SerializeField, Tooltip("Visual-only prefab instantiated while this item is held.")]
        private GameObject visualPrefab;
        [SerializeField, Tooltip("Local position of the held visual relative to the equip component's anchor.")]
        private Vector3 anchorPosition;
        [SerializeField, Tooltip("Local Euler rotation of the held visual relative to the equip component's anchor.")]
        private Vector3 anchorRotation;

        public GameObject VisualPrefab => visualPrefab;
        public Vector3 AnchorPosition => anchorPosition;
        public Vector3 AnchorRotation => anchorRotation;
    }
}
