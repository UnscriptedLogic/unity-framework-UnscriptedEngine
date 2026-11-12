using UnityEngine;

namespace UnscriptedEngine
{
    [AddComponentMenu("Unscripted Engine/Object Animation/Move")]
    public sealed class ObjectAnimMove : ObjectAnimComponent
    {
        [SerializeField] private Vector3 offset = new Vector3(0f, 0.1f, 0f);
        private Vector3 rest;
        public Vector3 Offset { get => offset; set => offset = value; }
        protected override void CapturePose(Transform value) => rest = value.localPosition;
        protected override void ApplyPose(Transform value, float amount) => value.localPosition = rest + offset * amount;
    }
}
