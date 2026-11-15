using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace UnscriptedEngine.Gameplay
{
    /// <summary>Schedules effects per actor visit, independently of physics Stay callbacks.</summary>
    [RequireComponent(typeof(ColliderZone))]
    public abstract class ContactZoneEffect : MonoBehaviour
    {
        private sealed class Visit
        {
            internal bool Applied;
            internal float NextApplication;
        }

        [SerializeField]
        [Tooltip("Repeat while contact continues. Disabled applies once per actor visit, resetting after all its colliders leave.")]
        private bool ongoing;
        [SerializeField]
        [Min(0.01f)]
        [Tooltip("Seconds between ongoing applications. First application is immediate. Uses scaled gameplay time.")]
        private float interval = 1f;
        [SerializeField]
        [Tooltip("Invoked after an effect is accepted. Receives the resolved actor root.")]
        private UnityEvent<GameObject> applied = new UnityEvent<GameObject>();

        private ColliderZone zone;
        private readonly List<Collider> contacts = new List<Collider>();
        private readonly Dictionary<Collider, GameObject> contactActors = new Dictionary<Collider, GameObject>();
        private readonly Dictionary<GameObject, Visit> visits = new Dictionary<GameObject, Visit>();
        private readonly HashSet<GameObject> currentActors = new HashSet<GameObject>();
        private readonly List<GameObject> actorSnapshot = new List<GameObject>();
        private readonly List<GameObject> expiredActors = new List<GameObject>();
        private readonly List<Collider> expiredContacts = new List<Collider>();

        public bool Ongoing { get => ongoing; set => ongoing = value; }
        public float Interval { get => interval; set => interval = Mathf.Max(0.01f, value); }
        public UnityEvent<GameObject> Applied => applied;

        protected virtual void OnEnable()
        {
            zone = GetComponent<ColliderZone>(); ClearVisits();
            zone.Entered.RemoveListener(HandleEntered); zone.Entered.AddListener(HandleEntered);
            zone.Exited.RemoveListener(HandleExited); zone.Exited.AddListener(HandleExited);
        }
        protected virtual void OnDisable()
        {
            if (zone != null)
            {
                zone.Entered.RemoveListener(HandleEntered); zone.Exited.RemoveListener(HandleExited);
            }
            ClearVisits();
        }

        private void HandleEntered(Collider contact)
        {
            if (!isActiveAndEnabled || !ZoneEffectTarget.HasAuthority || contact == null) return;
            GameObject actor = ZoneEffectTarget.Resolve(contact);
            contactActors[contact] = actor;
            ApplyIfDue(actor);
        }

        private void HandleExited(Collider contact)
        {
            if (!contactActors.TryGetValue(contact, out GameObject actor)) return;
            contactActors.Remove(contact);
            foreach (KeyValuePair<Collider, GameObject> pair in contactActors)
                if (pair.Value == actor && zone.ContainsContact(pair.Key)) return;
            visits.Remove(actor);
        }

        private void ClearVisits()
        {
            contacts.Clear(); contactActors.Clear(); visits.Clear(); currentActors.Clear();
            actorSnapshot.Clear(); expiredActors.Clear(); expiredContacts.Clear();
        }

        private void Update()
        {
            if (!ZoneEffectTarget.HasAuthority || zone == null || !zone.isActiveAndEnabled || zone.Shape == null || !zone.Shape.enabled)
            {
                ClearVisits(); return;
            }
            zone.CopyContacts(contacts);
            currentActors.Clear(); actorSnapshot.Clear();
            foreach (Collider contact in contacts)
            {
                if (contact == null || !contact.enabled || !contact.gameObject.activeInHierarchy || !zone.Filter.Allows(contact)) continue;
                if (!contactActors.TryGetValue(contact, out GameObject actor) || actor == null)
                {
                    actor = ZoneEffectTarget.Resolve(contact);
                    contactActors[contact] = actor;
                }
                if (actor != null && currentActors.Add(actor)) actorSnapshot.Add(actor);
            }
            expiredContacts.Clear();
            foreach (KeyValuePair<Collider, GameObject> pair in contactActors)
                if (pair.Key == null || !zone.ContainsContact(pair.Key)) expiredContacts.Add(pair.Key);
            foreach (Collider contact in expiredContacts) contactActors.Remove(contact);
            expiredActors.Clear();
            foreach (GameObject actor in visits.Keys)
                if (actor == null || !currentActors.Contains(actor)) expiredActors.Add(actor);
            foreach (GameObject actor in expiredActors) visits.Remove(actor);

            for (int index = 0; index < actorSnapshot.Count && isActiveAndEnabled && zone.isActiveAndEnabled; index++)
            {
                GameObject actor = actorSnapshot[index];
                if (actor == null || !actor.activeInHierarchy) continue;
                ApplyIfDue(actor);
            }
        }

        private void ApplyIfDue(GameObject actor)
        {
            if (actor == null || !actor.activeInHierarchy) return;
            if (!visits.TryGetValue(actor, out Visit visit))
            {
                visit = new Visit(); visits.Add(actor, visit);
            }
            if ((!ongoing && visit.Applied) || Time.time < visit.NextApplication) return;
            visit.NextApplication = Time.time + Mathf.Max(0.01f, interval);
            if (!TryApply(actor)) return;
            visit.Applied = true;
            if (isActiveAndEnabled) applied.Invoke(actor);
        }

        protected abstract bool TryApply(GameObject actor);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ResetSessionVisits()
        {
            foreach (ContactZoneEffect effect in Object.FindObjectsByType<ContactZoneEffect>())
            {
                effect.zone = effect.GetComponent<ColliderZone>();
                effect.ClearVisits();
                effect.zone.Entered.RemoveListener(effect.HandleEntered);
                effect.zone.Exited.RemoveListener(effect.HandleExited);
                if (!effect.isActiveAndEnabled) continue;
                effect.zone.Entered.AddListener(effect.HandleEntered);
                effect.zone.Exited.AddListener(effect.HandleExited);
            }
        }
    }
}
