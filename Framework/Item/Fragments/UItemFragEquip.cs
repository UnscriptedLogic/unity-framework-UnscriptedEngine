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

        [SerializeField, Tooltip("Optional alternate visual for a hand with Mirror Held Pose enabled.")]
        private GameObject leftHandVisualPrefab;
        [SerializeField, Tooltip("Use an authored left-hand pose instead of mirroring the default pose.")]
        private bool overrideLeftHandPose;
        [SerializeField] private Vector3 leftHandPosition;
        [SerializeField] private Vector3 leftHandRotation;
        [SerializeField, Tooltip("Optional path to a mesh-only child to mirror on X when this item is in a mirrored hand.")]
        private string leftHandMirrorChildPath;

        public GameObject GetVisualPrefab(bool mirroredHand) =>
            mirroredHand && leftHandVisualPrefab != null ? leftHandVisualPrefab : visualPrefab;

        public void ApplyVisualPose(Transform visual, bool mirroredHand)
        {
            Vector3 position = anchorPosition;
            Quaternion rotation = Quaternion.Euler(anchorRotation);
            if (mirroredHand)
            {
                if (overrideLeftHandPose)
                {
                    position = leftHandPosition;
                    rotation = Quaternion.Euler(leftHandRotation);
                }
                else
                {
                    position.x = -position.x;
                    // Reflect the rotation across the anchor's local YZ plane.
                    rotation = new Quaternion(rotation.x, -rotation.y, -rotation.z, rotation.w);
                }

                if (!string.IsNullOrEmpty(leftHandMirrorChildPath))
                {
                    Transform mesh = visual.Find(leftHandMirrorChildPath);
                    if (mesh != null && mesh != visual)
                    {
                        Vector3 scale = mesh.localScale;
                        scale.x = -scale.x;
                        mesh.localScale = scale;
                    }
                }
            }

            visual.SetLocalPositionAndRotation(position, rotation);
        }

        public GameObject VisualPrefab => visualPrefab;
        public Vector3 AnchorPosition => anchorPosition;
        public Vector3 AnchorRotation => anchorRotation;
    }
}
