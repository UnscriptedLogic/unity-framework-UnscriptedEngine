using System.Collections.Generic;
using UnityEngine;

namespace UnscriptedEngine
{
    /// <summary>
    /// Attach beside a CharacterController or the Rigidbody receiving collision callbacks.
    /// Targets need non-kinematic Rigidbodies and non-trigger colliders.
    /// Networked physics is applied on the server; targets must replicate their own transforms.
    /// </summary>
    [DisallowMultipleComponent]
    public class PhysicsMovementInteractor : UObjectComponent
    {
        [Header("Push Filtering")]
        [SerializeField] private LayerMask pushableLayers = ~0;
        [SerializeField, Min(0f)] private float minimumMovementSpeed = 0.1f;
        [Tooltip("Contacts with more vertical normals are treated as floors or ceilings.")]
        [SerializeField, Range(0f, 1f)] private float maximumVerticalNormal = 0.5f;

        [Header("Push Strength")]
        [Tooltip("Fraction of closing speed transferred per physics step.")]
        [SerializeField, Range(0f, 1f)] private float velocityTransfer = 0.75f;
        [Tooltip("Maximum impulse per target per physics step, in Newton seconds. Heavy objects accelerate less.")]
        [SerializeField, Min(0f)] private float maximumPushImpulse = 3f;
        [Tooltip("Maximum target speed along the push direction, in metres per second.")]
        [SerializeField, Min(0f)] private float maximumPushSpeed = 5f;
        [Tooltip("Apply at the contact point to allow objects to tip and spin.")]
        [SerializeField] private bool applyAtContactPoint = true;

        private readonly HashSet<Rigidbody> pushedBodies = new HashSet<Rigidbody>();
        private Rigidbody sourceBody;
        private CharacterMovementComponent characterMovement;
        private Vector3 previousPosition;
        private Vector3 sampledVelocity;
        private double lastPushFixedTime = double.NegativeInfinity;

        protected override void Awake()
        {
            base.Awake();

            sourceBody = GetComponent<Rigidbody>();
            characterMovement = GetComponent<CharacterMovementComponent>();
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            previousPosition = transform.position;
            sampledVelocity = Vector3.zero;
            pushedBodies.Clear();
            lastPushFixedTime = double.NegativeInfinity;
        }

        // Sample ordinary transform-driven movers too, without requiring a spawned UObject.
        private void LateUpdate()
        {
            Vector3 currentPosition = transform.position;
            sampledVelocity = Time.deltaTime > 0f
                ? (currentPosition - previousPosition) / Time.deltaTime
                : Vector3.zero;

            previousPosition = currentPosition;
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            Vector3 movementVelocity = characterMovement != null
                ? characterMovement.Velocity
                : hit.moveDirection * (hit.moveLength / Mathf.Max(Time.deltaTime, 0.0001f));

            TryPushBody(hit.collider, hit.point, hit.normal, movementVelocity);
        }

        private void OnCollisionEnter(Collision collision)
        {
            PushCollisionContacts(collision);
        }

        private void OnCollisionStay(Collision collision)
        {
            PushCollisionContacts(collision);
        }

        private void PushCollisionContacts(Collision collision)
        {
            for (int contactIndex = 0; contactIndex < collision.contactCount; contactIndex++)
            {
                ContactPoint contact = collision.GetContact(contactIndex);
                Vector3 movementVelocity = GetMovementVelocity(contact.point);

                TryPushBody(collision.collider, contact.point, contact.normal, movementVelocity);
            }
        }

        private Vector3 GetMovementVelocity(Vector3 contactPoint)
        {
            if (characterMovement != null)
                return characterMovement.Velocity;

            if (sourceBody != null)
                return sourceBody.GetPointVelocity(contactPoint);

            return sampledVelocity;
        }

        private bool CanApplyPhysics()
        {
            if (!isActiveAndEnabled)
                return false;

            Unity.Netcode.NetworkManager manager = Unity.Netcode.NetworkManager.Singleton;
            return manager == null || !manager.IsListening || manager.IsServer;
        }

        private bool IsPushable(Collider targetCollider, Rigidbody targetBody)
        {
            if (targetBody == null || targetBody.isKinematic || targetBody == sourceBody)
                return false;

            if (targetBody.transform == transform || targetBody.transform.IsChildOf(transform))
                return false;

            return !targetCollider.isTrigger &&
                (pushableLayers.value & (1 << targetCollider.gameObject.layer)) != 0;
        }

        private void ResetPushTrackingForNewStep()
        {
            if (lastPushFixedTime == Time.fixedTimeAsDouble)
                return;

            lastPushFixedTime = Time.fixedTimeAsDouble;
            pushedBodies.Clear();
        }

        private void TryPushBody(Collider targetCollider, Vector3 point, Vector3 normal, Vector3 movementVelocity)
        {
            if (!CanApplyPhysics())
                return;

            Rigidbody targetBody = targetCollider.attachedRigidbody;
            if (!IsPushable(targetCollider, targetBody) || Mathf.Abs(normal.y) > maximumVerticalNormal)
                return;

            ResetPushTrackingForNewStep();
            if (pushedBodies.Contains(targetBody))
                return;

            Vector3 pushDirection = Vector3.ProjectOnPlane(-normal, Vector3.up).normalized;
            float movementSpeed = Vector3.Dot(movementVelocity, pushDirection);
            if (movementSpeed < minimumMovementSpeed)
                return;

            float impulseMagnitude = CalculatePushImpulse(targetBody, pushDirection, movementSpeed);
            if (impulseMagnitude <= 0f)
                return;

            pushedBodies.Add(targetBody);
            ApplyPushImpulse(targetBody, point, pushDirection * impulseMagnitude);
        }

        private float CalculatePushImpulse(Rigidbody targetBody, Vector3 direction, float movementSpeed)
        {
            float targetSpeed = Vector3.Dot(targetBody.linearVelocity, direction);
            float desiredSpeed = Mathf.Min(movementSpeed, maximumPushSpeed);
            float speedIncrease = Mathf.Max(0f, desiredSpeed - targetSpeed) * velocityTransfer;

            return Mathf.Min(speedIncrease * targetBody.mass, maximumPushImpulse);
        }

        private void ApplyPushImpulse(Rigidbody targetBody, Vector3 contactPoint, Vector3 impulse)
        {
            if (applyAtContactPoint)
                targetBody.AddForceAtPosition(impulse, contactPoint, ForceMode.Impulse);
            else
                targetBody.AddForce(impulse, ForceMode.Impulse);
        }
    }
}
