using UnityEngine;

namespace UnscriptedEngine
{
    [AddComponentMenu("Unscripted Engine/Object Animation/Rotate")]
    public sealed class ObjectAnimRotate : ObjectAnimComponent
    {
        [SerializeField] private Vector3 degrees = new Vector3(0f, 0f, 8f);
        private Quaternion rest;
        public Vector3 Degrees { get => degrees; set => degrees = value; }
        protected override void CapturePose(Transform value) => rest = value.localRotation;
        protected override void ApplyPose(Transform value, float amount) => value.localRotation = rest * Quaternion.Euler(degrees * amount);
    }
}
