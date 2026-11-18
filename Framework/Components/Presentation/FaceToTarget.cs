using UnityEngine;

namespace UnscriptedEngine
{
    /// <summary>Explicitly driven facing: the caller owns target selection and update timing.</summary>
    [DisallowMultipleComponent]
    public sealed class FaceToTarget : MonoBehaviour
    {
        [SerializeField, Tooltip("Pivot to rotate; this transform when empty.")]
        private Transform pivot;
        [SerializeField, Min(0f), Tooltip("Maximum turn speed in degrees per second.")]
        private float degreesPerSecond = 360f;
        [SerializeField, Tooltip("Rotate around world up, keeping the current elevation.")]
        private bool yawOnly = true;
        [SerializeField, Range(0f, 45f), Tooltip("Maximum remaining aim error before reporting alignment.")]
        private float tolerance = 3f;

        public Transform Pivot => pivot != null ? pivot : transform;

        public bool Face(Vector3 point, float deltaTime)
        {
            Vector3 direction = point - Pivot.position;
            if (yawOnly) direction = Vector3.ProjectOnPlane(direction, Vector3.up);
            if (direction.sqrMagnitude < 0.000001f) return false;
            Quaternion desired = Quaternion.LookRotation(direction, Vector3.up);
            Pivot.rotation = Quaternion.RotateTowards(Pivot.rotation, desired,
                Mathf.Max(0f, degreesPerSecond) * Mathf.Max(0f, deltaTime));
            return Quaternion.Angle(Pivot.rotation, desired) <= tolerance;
        }
    }
}
