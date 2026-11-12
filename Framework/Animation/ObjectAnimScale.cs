using UnityEngine;

namespace UnscriptedEngine
{
    [AddComponentMenu("Unscripted Engine/Object Animation/Scale")]
    public sealed class ObjectAnimScale : ObjectAnimComponent
    {
        [Tooltip("Multipliers at full strength. (1.2, 0.8, 1.2) squashes the visual.")]
        [SerializeField] private Vector3 multiplier = new Vector3(1.2f, 0.8f, 1.2f);
        private Vector3 rest;
        public Vector3 Multiplier { get => multiplier; set => multiplier = value; }
        protected override void CapturePose(Transform value) => rest = value.localScale;
        protected override void ApplyPose(Transform value, float amount) =>
            value.localScale = Vector3.Scale(rest, Vector3.LerpUnclamped(Vector3.one, multiplier, amount));
    }
}
