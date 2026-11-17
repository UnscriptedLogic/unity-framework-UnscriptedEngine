using UnityEngine;

namespace UnscriptedEngine
{
    /// <summary>Shared force routing for physics bodies and controller-driven characters.</summary>
    public static class PhysicsForceUtility
    {
        public static bool IsFinite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
            !float.IsNaN(value.z) && !float.IsInfinity(value.z);

        public static Component ResolveTarget(Collider contact)
        {
            if (contact == null) return null;
            Rigidbody body = contact.attachedRigidbody;
            if (body != null && !body.isKinematic) return body;
            CharacterMovementComponent movement = contact.GetComponentInParent<CharacterMovementComponent>();
            return movement != null && movement.isActiveAndEnabled ? movement : null;
        }

        public static Vector3 GetCenter(Component target)
        {
            if (target is Rigidbody body) return body.worldCenterOfMass;
            CharacterController controller = target.GetComponent<CharacterController>();
            return controller != null ? controller.bounds.center : target.transform.position;
        }

        public static bool BelongsTo(Collider collider, Component target) => ResolveTarget(collider) == target;

        /// <summary>Acceleration is integrated once; character impulses are velocity changes (no Rigidbody mass).</summary>
        public static bool TryApply(Component target, Vector3 force, bool impulse, float deltaTime)
        {
            if (target == null || !IsFinite(force)) return false;
            if (target is Rigidbody body)
            {
                if (body.isKinematic || !body.gameObject.activeInHierarchy) return false;
                body.AddForce(force, impulse ? ForceMode.Impulse : ForceMode.Acceleration);
                return true;
            }
            return target is CharacterMovementComponent movement &&
                movement.QueueAuthoritativeVelocityChange(impulse ? force : force * deltaTime);
        }
    }
}
