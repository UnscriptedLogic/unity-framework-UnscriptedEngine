using UnityEngine;

namespace UnscriptedEngine
{
    /// <summary>
    /// Cosmetic hand motion for an item equip anchor. Attach to a dedicated pivot
    /// beneath the player's look rig and use that pivot as ItemEquipComponent's anchor.
    /// No input bindings are required. Distances use the anchor parent's local units.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9990)]
    public class FPSItemTweaks : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Player root used to measure movement. Defaults to a parent movement component, controller, rigidbody, or hierarchy root.")]
        [SerializeField] private Transform velocityTarget;
        [Tooltip("Optional movement component supplying velocity and grounded state. Automatically found above the velocity target.")]
        [SerializeField] private CharacterMovementComponent movement;
        [Tooltip("Optional CharacterController fallback. Automatically found above the velocity target.")]
        [SerializeField] private CharacterController characterController;
        [SerializeField] private bool assumeGroundedWithoutController = true;

        [Header("Movement Sway")]
        [SerializeField] private bool enableMovementSway = true;
        [Tooltip("Position lag per unit/second of sideways and forward movement.")]
        [SerializeField] private Vector2 movementPosition = new Vector2(0.006f, 0.003f);
        [Tooltip("Pitch and roll degrees per unit/second of forward and sideways movement.")]
        [SerializeField] private Vector2 movementRotation = new Vector2(0.5f, 1f);

        [Header("Look Sway")]
        [SerializeField] private bool enableLookSway = true;
        [Tooltip("Horizontal and vertical position lag per degree/second of look rotation.")]
        [SerializeField] private Vector2 lookPosition = new Vector2(0.00015f, 0.00015f);
        [Tooltip("Pitch, yaw and roll sway per degree/second of look rotation.")]
        [SerializeField] private Vector3 lookRotation = new Vector3(0.012f, 0.012f, 0.008f);
        [Min(0f)] [SerializeField] private float maximumSwayPosition = 0.06f;
        [Min(0f)] [SerializeField] private float maximumSwayAngle = 8f;
        [Min(0f)] [SerializeField] private float swaySmoothing = 12f;

        [Header("Walking Bob")]
        [SerializeField] private bool enableBob = true;
        [Tooltip("Sideways, vertical and forward bob distance at full speed.")]
        [SerializeField] private Vector3 bobPosition = new Vector3(0.015f, 0.02f, 0.005f);
        [Tooltip("Pitch, yaw and roll bob in degrees at full speed.")]
        [SerializeField] private Vector3 bobRotation = new Vector3(0.7f, 0.5f, 1.5f);
        [Min(0f)] [SerializeField] private float bobStartSpeed = 0.1f;
        [Min(0.01f)] [SerializeField] private float bobFullSpeed = 6f;
        [Tooltip("Side-to-side cycles per second. Vertical bob runs at twice this frequency.")]
        [Min(0f)] [SerializeField] private float bobFrequency = 1.5f;
        [Min(0f)] [SerializeField] private float bobSmoothing = 10f;

        [Header("Jump And Landing")]
        [SerializeField] private bool enableJumpMotion = true;
        [Tooltip("Vertical position lag per unit/second of airborne vertical speed.")]
        [Min(0f)] [SerializeField] private float airborneLag = 0.004f;
        [Tooltip("Downward spring kick per unit/second of takeoff or landing speed.")]
        [Min(0f)] [SerializeField] private float jumpStrength = 0.12f;
        [Min(0f)] [SerializeField] private float landingStrength = 0.2f;
        [Min(0f)] [SerializeField] private float maximumJumpOffset = 0.08f;
        [Min(0.01f)] [SerializeField] private float springRecovery = 12f;
        [Tooltip("Pitch degrees per unit of vertical hand displacement.")]
        [SerializeField] private float jumpPitch = -60f;

        [Header("Sampling")]
        [Tooltip("Ignore frame displacements above this world distance. Zero disables teleport filtering.")]
        [Min(0f)] [SerializeField] private float teleportDistance = 5f;

        private Transform source;
        private CharacterMovementComponent detectedMovement;
        private CharacterController detectedController;
        private Rigidbody detectedBody;
        private Vector3 previousPosition;
        private Quaternion previousRotation;
        private float previousVerticalSpeed;
        private bool previousGrounded;
        private bool hasSample;
        private Vector3 swayPosition;
        private Vector3 swayAngles;
        private float bobPhase;
        private float bobAmount;
        private float jumpOffset;
        private float springVelocity;
        private float verticalLag;
        private Vector3 cleanPosition;
        private Quaternion cleanRotation;
        private Vector3 appliedPosition;
        private Quaternion appliedRotation;
        private bool hasOffset;

        private void OnEnable()
        {
            source = null;
            ResetMotion();
        }

        // Remove cosmetic motion before ordinary movement and animation updates.
        private void Update() => RemoveOffset();

        private void LateUpdate()
        {
            RemoveOffset();
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            Transform target = ResolveSource();
            if (source != target)
            {
                source = target;
                detectedMovement = source.GetComponentInParent<CharacterMovementComponent>();
                detectedController = source.GetComponentInParent<CharacterController>();
                detectedBody = source.GetComponentInParent<Rigidbody>();
                ResetMotion();
            }

            Vector3 position = source.position;
            // Parent rotation excludes this component's own cosmetic rotation.
            Quaternion rotation = transform.parent != null ? transform.parent.rotation : Quaternion.identity;
            Vector3 displacement = position - previousPosition;
            bool valid = hasSample && (teleportDistance <= 0f || displacement.magnitude <= teleportDistance);
            CharacterMovementComponent motor = movement != null ? movement : detectedMovement;
            CharacterController controller = characterController != null ? characterController : detectedController;
            Vector3 velocity = motor != null ? motor.Velocity : controller != null ? controller.velocity
                : detectedBody != null ? detectedBody.linearVelocity : valid ? displacement / dt : Vector3.zero;
            bool grounded = motor != null ? motor.IsGrounded : controller != null
                ? controller.isGrounded : assumeGroundedWithoutController;
            Vector3 angularVelocity = Vector3.zero;
            if (valid)
            {
                Quaternion delta = Quaternion.Inverse(previousRotation) * rotation;
                delta.ToAngleAxis(out float angle, out Vector3 axis);
                if (angle > 180f) angle -= 360f;
                if (Mathf.Abs(angle) > 0.001f) angularVelocity = axis * (angle / dt);
            }
            else ResetMotion();

            Vector3 localVelocity = Quaternion.Inverse(rotation) * (valid ? velocity : Vector3.zero);
            Vector3 desiredPosition = Vector3.zero;
            Vector3 desiredAngles = Vector3.zero;
            if (enableMovementSway)
            {
                desiredPosition = new Vector3(-localVelocity.x * movementPosition.x, 0f,
                    -localVelocity.z * movementPosition.y);
                desiredAngles = new Vector3(localVelocity.z * movementRotation.x, 0f,
                    -localVelocity.x * movementRotation.y);
            }
            if (enableLookSway)
            {
                desiredPosition += new Vector3(-angularVelocity.y * lookPosition.x,
                    angularVelocity.x * lookPosition.y, 0f);
                desiredAngles += new Vector3(-angularVelocity.x * lookRotation.x,
                    -angularVelocity.y * lookRotation.y, angularVelocity.y * lookRotation.z);
            }
            swayPosition = Vector3.Lerp(swayPosition, Vector3.ClampMagnitude(desiredPosition,
                maximumSwayPosition), Blend(swaySmoothing, dt));
            swayAngles = Vector3.Lerp(swayAngles, Vector3.ClampMagnitude(desiredAngles,
                maximumSwayAngle), Blend(swaySmoothing, dt));

            float speed = new Vector2(velocity.x, velocity.z).magnitude;
            float amount = valid && enableBob && grounded ? Mathf.InverseLerp(bobStartSpeed,
                Mathf.Max(bobStartSpeed + 0.01f, bobFullSpeed), speed) : 0f;
            bobAmount = Mathf.Lerp(bobAmount, amount, Blend(bobSmoothing, dt));
            if (bobAmount > 0.00001f)
                bobPhase = Mathf.Repeat(bobPhase + dt * Mathf.PI * 2f * bobFrequency
                    * Mathf.Lerp(0.5f, 1f, amount), Mathf.PI * 2f);
            else bobPhase = 0f;
            Vector3 wave = new Vector3(Mathf.Sin(bobPhase), Mathf.Cos(bobPhase * 2f), Mathf.Cos(bobPhase));

            if (valid && enableJumpMotion)
            {
                if (!grounded && previousGrounded && velocity.y > 0f)
                    springVelocity -= velocity.y * jumpStrength;
                if (grounded && !previousGrounded && previousVerticalSpeed < 0f)
                    springVelocity -= Mathf.Max(-previousVerticalSpeed, Mathf.Abs(velocity.y - previousVerticalSpeed))
                        * landingStrength;
            }
            if (!enableJumpMotion) { jumpOffset = 0f; springVelocity = 0f; }
            float recovery = Mathf.Max(0.01f, springRecovery);
            float decay = Mathf.Exp(-recovery * dt);
            float term = springVelocity + recovery * jumpOffset;
            float next = (jumpOffset + term * dt) * decay;
            springVelocity = (springVelocity - recovery * term * dt) * decay;
            jumpOffset = Mathf.Clamp(next, -maximumJumpOffset, maximumJumpOffset);
            if (next != jumpOffset && Mathf.Sign(springVelocity) == Mathf.Sign(next)) springVelocity = 0f;
            verticalLag = Mathf.Lerp(verticalLag, valid && enableJumpMotion && !grounded
                ? Mathf.Clamp(-velocity.y * airborneLag, -maximumJumpOffset, maximumJumpOffset) : 0f,
                Blend(swaySmoothing, dt));
            float verticalOffset = Mathf.Clamp(jumpOffset + verticalLag, -maximumJumpOffset, maximumJumpOffset);

            cleanPosition = transform.localPosition;
            cleanRotation = transform.localRotation;
            appliedPosition = cleanPosition + swayPosition + Vector3.Scale(wave, bobPosition) * bobAmount
                + Vector3.up * verticalOffset;
            appliedRotation = cleanRotation * Quaternion.Euler(swayAngles
                + Vector3.Scale(wave, bobRotation) * bobAmount + Vector3.right * (verticalOffset * jumpPitch));
            transform.SetLocalPositionAndRotation(appliedPosition, appliedRotation);
            hasOffset = true;
            previousPosition = position;
            previousRotation = rotation;
            previousVerticalSpeed = velocity.y;
            previousGrounded = grounded;
            hasSample = true;
        }

        private Transform ResolveSource()
        {
            if (velocityTarget != null) return velocityTarget;
            if (movement != null) return movement.transform;
            if (characterController != null) return characterController.transform;
            CharacterMovementComponent motor = GetComponentInParent<CharacterMovementComponent>();
            if (motor != null) return motor.transform;
            CharacterController controller = GetComponentInParent<CharacterController>();
            if (controller != null) return controller.transform;
            Rigidbody body = GetComponentInParent<Rigidbody>();
            return body != null ? body.transform : transform.root;
        }

        private void ResetMotion()
        {
            hasSample = false;
            swayPosition = swayAngles = Vector3.zero;
            bobPhase = bobAmount = jumpOffset = springVelocity = verticalLag = 0f;
        }

        private static float Blend(float sharpness, float dt) =>
            sharpness <= 0f ? 1f : 1f - Mathf.Exp(-sharpness * dt);

        internal void RemoveOffset()
        {
            if (!hasOffset) return;
            // Keep any pose authored by another system since our last application.
            if (transform.localPosition == appliedPosition) transform.localPosition = cleanPosition;
            if (transform.localRotation == appliedRotation) transform.localRotation = cleanRotation;
            hasOffset = false;
        }

        private void OnDisable()
        {
            RemoveOffset();
            ResetMotion();
        }
    }
}
