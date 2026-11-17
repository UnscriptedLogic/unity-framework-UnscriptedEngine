using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnscriptedEngine.Gameplay
{
    /// <summary>Applies forces once per contacted Rigidbody or controller-driven character, respecting ColliderZone filters and authority.</summary>
    [RequireComponent(typeof(ColliderZone))]
    public abstract class PhysicsForceZone : MonoBehaviour
    {
        [SerializeField, Min(0f), Tooltip("Wind/suction acceleration in m/s². Knockback impulse in N·s (affected by mass). Character knockback is a velocity change in m/s. Compound colliders count once.")] private float strength = 10f;
        [SerializeField, Tooltip("Optional plane transform. Defaults to this object; the plane passes through its origin.")]
        private Transform sourcePlane;
        [SerializeField, Tooltip("Plane normal / push direction in the source plane's local space. Unity planes use +Y.")]
        private Vector3 localDirection = Vector3.up;
        [SerializeField] private bool requireLineOfSight = true;
        [SerializeField] private LayerMask obstructionLayers = ~0;

        private ColliderZone zone;
        private readonly List<Collider> contacts = new List<Collider>();
        private readonly HashSet<Component> bodies = new HashSet<Component>();
        private readonly HashSet<Component> appliedBodies = new HashSet<Component>();
        private RaycastHit[] hits = new RaycastHit[16];

        public float Strength { get => strength; set => strength = Mathf.Max(0f, value); }
        public Transform SourcePlane { get => sourcePlane; set => sourcePlane = value; }
        public Vector3 LocalDirection { get => localDirection; set => localDirection = value; }
        public bool RequireLineOfSight { get => requireLineOfSight; set => requireLineOfSight = value; }
        public LayerMask ObstructionLayers { get => obstructionLayers; set => obstructionLayers = value; }
        protected virtual bool Instant => false;
        protected virtual bool PullToPlane => false;
        private Transform Plane => sourcePlane != null ? sourcePlane : transform;
        public Vector3 WorldDirection => Plane.TransformDirection(localDirection).normalized;

        protected virtual void OnEnable()
        {
            zone = GetComponent<ColliderZone>();
            appliedBodies.Clear();
            zone.Entered.AddListener(HandleEntered);
            zone.Exited.AddListener(HandleExited);
        }

        protected virtual void OnDisable()
        {
            if (zone != null)
            {
                zone.Entered.RemoveListener(HandleEntered);
                zone.Exited.RemoveListener(HandleExited);
            }
            contacts.Clear(); bodies.Clear(); appliedBodies.Clear();
        }

        private void HandleEntered(Collider contact)
        {
            if (Instant && CanApply() && contact != null) ApplyToBody(PhysicsForceUtility.ResolveTarget(contact));
        }

        private void HandleExited(Collider contact)
        {
            CollectBodies();
            appliedBodies.RemoveWhere(body => body == null || !bodies.Contains(body));
        }

        private bool CanApply() => isActiveAndEnabled && ZoneEffectTarget.HasAuthority &&
            zone != null && zone.isActiveAndEnabled && zone.Shape != null && zone.Shape.enabled;

        private void CollectBodies()
        {
            bodies.Clear();
            zone.CopyContacts(contacts);
            foreach (Collider contact in contacts)
            {
                if (contact == null || !contact.enabled || !contact.gameObject.activeInHierarchy || !zone.Filter.Allows(contact)) continue;
                Component body = PhysicsForceUtility.ResolveTarget(contact);
                if (body != null) bodies.Add(body);
            }
        }

        protected virtual void FixedUpdate()
        {
            if (!CanApply()) { appliedBodies.Clear(); return; }
            CollectBodies();
            appliedBodies.RemoveWhere(body => body == null || !bodies.Contains(body));
            foreach (Component body in bodies) ApplyToBody(body);
        }

        private void ApplyToBody(Component body)
        {
            if (body == null || (Instant && appliedBodies.Contains(body))) return;
            Vector3 normal = WorldDirection;
            if (normal.sqrMagnitude < 0.0001f || strength <= 0f) return;
            Vector3 target = PhysicsForceUtility.GetCenter(body);
            float distance = Vector3.Dot(target - Plane.position, normal);
            Vector3 planePoint = target - normal * distance;
            if (requireLineOfSight && !HasLineOfSight(planePoint, target, body)) return;
            Vector3 direction = PullToPlane ? -normal * Mathf.Sign(distance) : normal;
            if (PullToPlane && Mathf.Abs(distance) < 0.001f) return;
            if (!PhysicsForceUtility.TryApply(body, direction * strength, Instant, Time.fixedDeltaTime)) return;
            if (Instant) appliedBodies.Add(body);
        }

        private bool HasLineOfSight(Vector3 start, Vector3 target, Component body)
        {
            Vector3 offset = target - start;
            float distance = offset.magnitude;
            if (distance < 0.001f) return true;
            // Cast from the body toward the plane so solid source surfaces do not hide intervening walls.
            int count;
            while (true)
            {
                count = gameObject.scene.GetPhysicsScene().Raycast(target, -offset / distance, hits, distance, obstructionLayers, QueryTriggerInteraction.Ignore);
                if (count < hits.Length) break;
                System.Array.Resize(ref hits, hits.Length * 2);
            }
            for (int index = 0; index < count; index++)
            {
                Collider hit = hits[index].collider;
                if (hit == zone.Shape || PhysicsForceUtility.BelongsTo(hit, body) || hit.transform == Plane) continue;
                return false;
            }
            return true;
        }

        protected virtual void OnDrawGizmosSelected()
        {
            Gizmos.color = PullToPlane ? Color.cyan : Color.yellow;
            Vector3 origin = Plane.position;
            Vector3 direction = WorldDirection;
            Gizmos.DrawRay(origin, direction * 2f);
            Vector3 side = Vector3.Cross(direction, Vector3.right);
            if (side.sqrMagnitude < 0.01f) side = Vector3.Cross(direction, Vector3.forward);
            side.Normalize();
            Vector3 other = Vector3.Cross(direction, side);
            Gizmos.DrawLine(origin - side - other, origin + side - other);
            Gizmos.DrawLine(origin + side - other, origin + side + other);
            Gizmos.DrawLine(origin + side + other, origin - side + other);
            Gizmos.DrawLine(origin - side + other, origin - side - other);
        }
    }
}
