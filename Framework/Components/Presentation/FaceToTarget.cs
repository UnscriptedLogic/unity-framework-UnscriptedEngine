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
            return Face(point, deltaTime, null);
        }

        /// <summary>In full rotation mode, align the forward ray of an origin that moves with the pivot.</summary>
        public bool Face(Vector3 point, float deltaTime, Transform aimOrigin)
        {
            Vector3 direction = point - Pivot.position;
            if (yawOnly) direction = Vector3.ProjectOnPlane(direction, Vector3.up);
            if (direction.sqrMagnitude < 0.000001f) return false;
            Quaternion desired = Quaternion.LookRotation(direction, Vector3.up);
            if (!yawOnly && aimOrigin != null)
            {
                Vector3 offset = aimOrigin.position - Pivot.position;
                Vector3 forward = aimOrigin.forward;
                float along = Vector3.Dot(offset, forward);
                // Find where the muzzle ray intersects a sphere centered at the pivot and passing through the target.
                float remaining = direction.sqrMagnitude - (offset - forward * along).sqrMagnitude;
                if (remaining < 0f) return false;
                float distance = Mathf.Sqrt(remaining) - along;
                if (distance <= 0f) return false;
                Vector3 rayPoint = Quaternion.Inverse(Pivot.rotation) * (offset + forward * distance);
                desired *= Quaternion.FromToRotation(rayPoint, Vector3.forward);
            }
            Pivot.rotation = Quaternion.RotateTowards(Pivot.rotation, desired,
                Mathf.Max(0f, degreesPerSecond) * Mathf.Max(0f, deltaTime));
            return Quaternion.Angle(Pivot.rotation, desired) <= tolerance;
        }
    }
}
