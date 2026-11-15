using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace UnscriptedEngine.Gameplay
{
    /// <summary>One local 3D collider, filtered per contacting collider. Compose behavior through events.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Unscripted Engine/Gameplay/Collider Zone")]
    public sealed class ColliderZone : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Layer and gameplay-tag conditions. All enabled conditions must pass.")]
        private ColliderZoneFilter filter = new ColliderZoneFilter();
        [SerializeField]
        [Tooltip("Invoked once when a collider is accepted. Receives the contacting collider.")]
        private UnityEvent<Collider> entered = new UnityEvent<Collider>();
        [SerializeField]
        [Tooltip("Invoked on accepted physics Stay callbacks. Sleeping bodies may not send Stay.")]
        private UnityEvent<Collider> stayed = new UnityEvent<Collider>();
        [SerializeField]
        [Tooltip("Invoked when an accepted collider leaves, becomes invalid, or stops passing filters. Destroyed colliders compare null.")]
        private UnityEvent<Collider> exited = new UnityEvent<Collider>();
        [Header("Debug")]
        [SerializeField]
        [Tooltip("Allow runtime debug visualization. Editor wireframes are always drawn when Scene view Gizmos are enabled.")]
        private bool showGizmos = true;
        [SerializeField]
        [Min(0f)]
        [Tooltip("Seconds to flash green after an accepted Enter callback.")]
        private float flashDuration = 0.35f;

        private readonly HashSet<Collider> contacts = new HashSet<Collider>();
        private readonly List<Collider> expired = new List<Collider>();
        private readonly List<MonoBehaviour> debugColorSources = new List<MonoBehaviour>();
        private readonly Dictionary<CharacterController, Vector3> controllerContacts = new Dictionary<CharacterController, Vector3>();
        private static readonly HashSet<ColliderZone> activeZones = new HashSet<ColliderZone>();
        private Collider shape;
        private float flashUntil = -1f;
        private int acceptedCount;
        private string lastContact = "None";

        public static IEnumerable<ColliderZone> ActiveZones => activeZones;
        public Collider Shape => shape != null ? shape : shape = GetComponent<Collider>();
        public ColliderZoneFilter Filter => filter;
        public UnityEvent<Collider> Entered => entered;
        public UnityEvent<Collider> Stayed => stayed;
        public UnityEvent<Collider> Exited => exited;
        public bool ShowGizmos => showGizmos;
        public int ContactCount => contacts.Count;
        public int AcceptedCount => acceptedCount;
        public string LastContact => lastContact;
        public bool ContainsContact(Collider other) => contacts.Contains(other);
        /// <summary>Replaces the caller-owned buffer with accepted contacts.</summary>
        public void CopyContacts(List<Collider> destination)
        {
            destination.Clear();
            foreach (Collider contact in contacts) destination.Add(contact);
        }
        public Color DebugColor
        {
            get
            {
                if (Application.isPlaying && Time.unscaledTime < flashUntil) return Color.green;
                GetComponents(debugColorSources);
                foreach (MonoBehaviour source in debugColorSources)
                {
                    if (source == null || !source.enabled || !(source is IZoneDebugColorProvider provider) ||
                        !provider.TryGetZoneDebugColor(out Color color)) continue;
                    color.a = 1f;
                    return color;
                }
                return Shape != null && Shape.isTrigger ? new Color(0.15f, 0.55f, 1f) : new Color(1f, 0.45f, 0.1f);
            }
        }

        private void OnEnable() { ResetContacts(); activeZones.Add(this); }
        private void Reset()
        {
            if (GetComponent<Collider>() == null) gameObject.AddComponent<BoxCollider>().isTrigger = true;
        }
        private void OnDisable() { activeZones.Remove(this); ResetContacts(); }
        private void ResetContacts()
        {
            contacts.Clear(); expired.Clear(); controllerContacts.Clear();
            acceptedCount = 0; lastContact = "None"; flashUntil = -1f;
        }

        /// <summary>Forward from the moving controller's OnControllerColliderHit callback.</summary>
        public static void ForwardControllerHit(ControllerColliderHit hit)
        {
            if (hit == null || hit.collider == null) return;
            ColliderZone zone = hit.collider.GetComponent<ColliderZone>();
            if (zone != null) zone.ReportControllerContact(hit.controller, hit.point);
        }

        /// <summary>Reports a real controller hit on this solid zone, using a world-space surface point.</summary>
        public void ReportControllerContact(CharacterController controller, Vector3 worldContactPoint)
        {
            if (!isActiveAndEnabled || Shape == null || !Shape.enabled || Shape.isTrigger || controller == null || !controller.enabled) return;
            controllerContacts[controller] = Shape.transform.InverseTransformPoint(worldContactPoint);
            AcceptContact(controller, contacts.Contains(controller));
            if (!contacts.Contains(controller)) controllerContacts.Remove(controller);
        }

        private void OnTriggerEnter(Collider other) => AcceptContact(other, false);
        private void OnTriggerStay(Collider other) => AcceptContact(other, true);
        private void OnTriggerExit(Collider other) => ReleaseContact(other);
        private void OnCollisionEnter(Collision collision) => AcceptContact(collision.collider, false);
        private void OnCollisionStay(Collision collision) => AcceptContact(collision.collider, true);
        private void OnCollisionExit(Collision collision) => ReleaseContact(collision.collider);

        private void AcceptContact(Collider other, bool stay)
        {
            if (!isActiveAndEnabled || Shape == null || !Shape.enabled) return;
            if (!filter.Allows(other)) { ReleaseContact(other); return; }
            if (contacts.Add(other))
            {
                acceptedCount++;
                lastContact = other.name;
                flashUntil = Time.unscaledTime + flashDuration;
                entered.Invoke(other);
            }
            if (stay && isActiveAndEnabled && contacts.Contains(other)) stayed.Invoke(other);
        }

        private void ReleaseContact(Collider other)
        {
            if (other is CharacterController controller) controllerContacts.Remove(controller);
            if (contacts.Remove(other)) exited.Invoke(other);
        }

        private void FixedUpdate()
        {
            expired.Clear();
            foreach (Collider other in contacts)
                if (Shape == null || !Shape.enabled || other == null || !other.enabled || !other.gameObject.activeInHierarchy || !filter.Allows(other)
                    || other is CharacterController controller && !IsControllerContactCurrent(controller))
                    expired.Add(other);
            // Remove before invoking listeners: callbacks may disable this component.
            for (int index = 0; index < expired.Count && isActiveAndEnabled; index++) ReleaseContact(expired[index]);
        }

        private bool IsControllerContactCurrent(CharacterController controller)
        {
            // Trigger contacts still use Unity's normal Enter/Stay/Exit lifecycle.
            if (!controllerContacts.TryGetValue(controller, out Vector3 localPoint)) return true;
            if (Shape.isTrigger || !controller.detectCollisions) return false;
            Vector3 point = Shape.transform.TransformPoint(localPoint);
            Vector3 scale = controller.transform.lossyScale;
            float radius = controller.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            float halfSegment = Mathf.Max(0f, controller.height * Mathf.Abs(scale.y) * 0.5f - radius);
            Vector3 center = controller.transform.TransformPoint(controller.center);
            Vector3 axis = controller.transform.up;
            Vector3 closestAxisPoint = center + axis * Mathf.Clamp(Vector3.Dot(point - center, axis), -halfSegment, halfSegment);
            float tolerance = controller.skinWidth * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)) + Shape.contactOffset + 0.001f;
            return (point - closestAxisPoint).sqrMagnitude <= (radius + tolerance) * (radius + tolerance);
        }

        private void OnDrawGizmos()
        {
            if (Shape == null) return;
            Gizmos.color = DebugColor;
            ColliderZoneWireframe.DrawGizmos(Shape);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => activeZones.Clear();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RecoverZones()
        {
            foreach (ColliderZone zone in Object.FindObjectsByType<ColliderZone>())
            {
                zone.ResetContacts();
                if (zone.isActiveAndEnabled) activeZones.Add(zone);
            }
        }
    }
}
