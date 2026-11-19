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
            if (CanReceiveForce(body)) return body;
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

        /// <summary>Whether a body is dynamic or has an active release trigger.</summary>
        public static bool CanReceiveForce(Rigidbody body) =>
            body != null && body.gameObject.activeInHierarchy &&
            (!body.isKinematic || (body.TryGetComponent(out RigidbodyImpactRelease trigger) && trigger.CanRelease));

        /// <summary>Releases a held target before applying the requested force to it.</summary>
        public static bool TryApply(Rigidbody body, Vector3 force, ForceMode mode, Vector3? point = null)
        {
            if (body == null || !body.gameObject.activeInHierarchy || !IsFinite(force) ||
                (point.HasValue && !IsFinite(point.Value)))
                return false;

            if (body.isKinematic && (force == Vector3.zero ||
                !body.TryGetComponent(out RigidbodyImpactRelease trigger) || !trigger.TryRelease() || body.isKinematic))
                return false;

            if (point.HasValue)
                body.AddForceAtPosition(force, point.Value, mode);
            else
                body.AddForce(force, mode);
            return true;
        }

        /// <summary>Acceleration is integrated once; character impulses are velocity changes (no Rigidbody mass).</summary>
        public static bool TryApply(Component target, Vector3 force, bool impulse, float deltaTime)
        {
            if (target == null || !IsFinite(force)) return false;
            if (target is Rigidbody body)
                return TryApply(body, force, impulse ? ForceMode.Impulse : ForceMode.Acceleration);
            return target is CharacterMovementComponent movement &&
                movement.QueueAuthoritativeVelocityChange(impulse ? force : force * deltaTime);
        }
    }
}
