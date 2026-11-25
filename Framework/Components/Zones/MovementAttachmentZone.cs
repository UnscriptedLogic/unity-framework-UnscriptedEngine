using UnityEngine;

namespace UnscriptedEngine.Gameplay
{
    /// <summary>Lets a character walk or jump in a moving object's reference frame while inside a filtered trigger.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider), typeof(ColliderZone))]
    [AddComponentMenu("Unscripted Engine/Gameplay/Movement Attachment Zone")]
    public sealed class MovementAttachmentZone : MonoBehaviour
    {
        private ColliderZone zone;
        private BoxCollider bounds;

        private void Reset() => GetComponent<BoxCollider>().isTrigger = true;

        private void Awake()
        {
            zone = GetComponent<ColliderZone>();
            bounds = GetComponent<BoxCollider>();
        }

        private void OnEnable()
        {
            zone.Entered.AddListener(OfferAttachment);
            zone.Stayed.AddListener(OfferAttachment);
            zone.Exited.AddListener(ForgetAttachment);
        }

        private void OnDisable()
        {
            zone.Entered.RemoveListener(OfferAttachment);
            zone.Stayed.RemoveListener(OfferAttachment);
            zone.Exited.RemoveListener(ForgetAttachment);
        }

        private void OfferAttachment(Collider contact)
        {
            if (contact is CharacterController controller &&
                controller.TryGetComponent(out CharacterMovementComponent movement))
                movement.TryAttachMovementFrame(this);
        }

        private void ForgetAttachment(Collider contact)
        {
            if (contact is CharacterController controller && controller != null &&
                controller.TryGetComponent(out CharacterMovementComponent movement))
                movement.ForgetMovementFrame(this);
        }

        internal bool Allows(CharacterController controller) =>
            isActiveAndEnabled && zone.isActiveAndEnabled && bounds.enabled && bounds.isTrigger &&
            controller != null && controller.enabled &&
            zone.Filter.Allows(controller) && !transform.IsChildOf(controller.transform);

        internal Pose GetPhysicsPose()
        {
            Rigidbody body = bounds.attachedRigidbody;
            return body != null ? new Pose(body.position, body.rotation) : new Pose(transform.position, transform.rotation);
        }

        internal Vector3 ToRenderPosition(Vector3 worldPoint, Pose physicsPose)
        {
            Rigidbody body = bounds.attachedRigidbody;
            Transform frame = body != null ? body.transform : transform;
            Vector3 framePoint = Quaternion.Inverse(physicsPose.rotation) * (worldPoint - physicsPose.position);
            return frame.position + frame.rotation * framePoint;
        }

        internal bool Contains(Vector3 worldPoint, Pose physicsPose)
        {
            Vector3 point = bounds.transform.InverseTransformPoint(ToRenderPosition(worldPoint, physicsPose)) - bounds.center;
            Vector3 halfSize = bounds.size * 0.5f;
            return Mathf.Abs(point.x) <= halfSize.x && Mathf.Abs(point.y) <= halfSize.y && Mathf.Abs(point.z) <= halfSize.z;
        }
    }
}
