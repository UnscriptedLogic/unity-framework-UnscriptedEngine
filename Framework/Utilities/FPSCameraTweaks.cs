using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

namespace UnscriptedEngine
{
    /// <summary>
    /// Adds cosmetic motion to a camera pivot. Prefer a dedicated child pivot beneath
    /// the look controller, with the Camera on that pivot or one of its children.
    /// Velocity is measured from the player's transform, including physics movement.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-10000)]
    public class FPSCameraTweaks : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Pivot receiving tilt and shake. Defaults to this transform.")]
        public Transform CameraRoot;
        [Tooltip("Output camera used to find the Cinemachine Brain, or receive FOV directly. Defaults to Camera.main.")]
        [SerializeField] private Camera targetCamera;
        [Tooltip("Optional Cinemachine camera override. Otherwise uses live cameras on the output camera's Brain, or a Cinemachine camera on CameraRoot.")]
        [SerializeField] private CinemachineCamera cinemachineCamera;
        [Tooltip("Player transform used to measure velocity. If empty, uses the active CameraHandler target, a parent movement component, or the camera pivot.")]
        [SerializeField] private Transform velocityTarget;

        [Header("Ground Detection")]
        [Tooltip("Optional player movement component for grounded state. Otherwise found above the velocity target.")]
        [SerializeField] private CharacterMovementComponent groundMovement;
        [Tooltip("Optional CharacterController fallback for grounded state. Otherwise found above the velocity target.")]
        [SerializeField] private CharacterController groundController;
        [Tooltip("Grounded state when neither movement component nor CharacterController is available.")]
        [SerializeField] private bool assumeGroundedWithoutController = true;

        [Header("Head Bob")]
        [SerializeField] private bool enableHeadBob = true;
        [Tooltip("Maximum vertical bob offset in local units. Only active while grounded and moving horizontally.")]
        [Min(0f)] [SerializeField] private float headBobHeight = 0.025f;
        [Tooltip("Horizontal speed below which head bob fades out.")]
        [Min(0f)] [SerializeField] private float headBobStartSpeed = 0.1f;
        [Tooltip("Horizontal speed at which head bob reaches full height and frequency.")]
        [Min(0.01f)] [SerializeField] private float headBobFullSpeed = 6f;
        [Tooltip("Vertical bob cycles per second at low and full movement speed.")]
        [Min(0f)] [SerializeField] private float minimumHeadBobFrequency = 1.5f;
        [Min(0f)] [SerializeField] private float maximumHeadBobFrequency = 2.5f;
        [Min(0f)] [SerializeField] private float headBobSmoothing = 10f;

        [Header("Velocity Jolts")]
        [SerializeField] private bool enableVelocityJolts = true;
        [Tooltip("Minimum vertical velocity change in units/second required to trigger a jolt.")]
        [Min(0f)] [SerializeField] private float joltVelocityThreshold = 2f;
        [Tooltip("Minimum vertical acceleration in units/second squared. Filters ordinary gravity and gradual speed changes.")]
        [Min(0f)] [SerializeField] private float joltAccelerationThreshold = 35f;
        [Tooltip("Spring kick per unit of vertical velocity change.")]
        [Min(0f)] [SerializeField] private float joltStrength = 0.3f;
        [Min(0f)] [SerializeField] private float landingJoltMultiplier = 1f;
        [Min(0f)] [SerializeField] private float airborneJoltMultiplier = 1f;
        [Tooltip("Maximum vertical jolt displacement in local units.")]
        [Min(0f)] [SerializeField] private float maximumJoltOffset = 0.15f;
        [Tooltip("Spring recovery rate. Higher values produce shorter, sharper jolts.")]
        [Min(0.01f)] [SerializeField] private float joltRecovery = 12f;

        [Header("Speed")]
        [SerializeField] private bool includeVerticalSpeed = true;
        [Min(0f)] [SerializeField] private float effectStartSpeed = 1f;
        [Min(0.01f)] [SerializeField] private float fullEffectSpeed = 15f;
        [Min(0f)] [SerializeField] private float speedSmoothing = 8f;
        [Tooltip("Ignore position jumps larger than this many world units per frame. Zero disables this filter.")]
        [Min(0f)] [SerializeField] private float teleportDistance = 10f;

        [Header("Tilt")]
        [SerializeField] private bool enableTilt = true;
        [Tooltip("Roll degrees per degree/second of horizontal camera rotation. Negative values reverse the tilt.")]
        [SerializeField] private float turnRoll = 0.025f;
        [Tooltip("Pitch degrees per degree/second of vertical camera rotation.")]
        [SerializeField] private float turnPitch = 0.015f;
        [Tooltip("Roll degrees per world unit/second of sideways camera movement.")]
        [SerializeField] private float panRoll = 0.3f;
        [Tooltip("Pitch degrees per world unit/second of vertical camera movement.")]
        [SerializeField] private float panPitch = 0.15f;
        [Min(0f)] [SerializeField] private float maximumRoll = 5f;
        [Min(0f)] [SerializeField] private float maximumPitch = 3f;
        [Min(0f)] [SerializeField] private float tiltSmoothing = 10f;

        [Header("Dynamic FOV")]
        [SerializeField] private bool enableDynamicFov = true;
        [SerializeField] private bool captureStartingFov = true;
        [Range(1f, 179f)] [SerializeField] private float baseFov = 60f;
        [Range(0f, 60f)] [SerializeField] private float extraFovAtFullSpeed = 15f;
        [Min(0f)] [SerializeField] private float fovSmoothing = 6f;

        [Header("Air Drag Shake")]
        [SerializeField] private bool enableShake = true;
        [Tooltip("Air-drag position and rotation shake strength while grounded. 0 disables it; 1 keeps full airborne strength.")]
        [Range(0f, 1f)] [SerializeField] private float groundedShakeMultiplier = 0.02f;
        [Tooltip("Maximum local position displacement per axis, in world units at unit scale.")]
        [SerializeField] private Vector3 shakePosition = new Vector3(0.015f, 0.015f, 0.005f);
        [Tooltip("Maximum local rotation displacement per axis, in degrees.")]
        [SerializeField] private Vector3 shakeRotation = new Vector3(0.3f, 0.3f, 0.4f);
        [Min(0f)] [SerializeField] private float minimumShakeFrequency = 6f;
        [Min(0f)] [SerializeField] private float maximumShakeFrequency = 18f;
        [Min(0f)] [SerializeField] private float shakeSmoothing = 8f;

        private Transform appliedRoot;
        private Transform previousVelocityTarget;
        private Vector3 previousPlayerPosition;
        private Vector3 previousCameraPosition;
        private Quaternion previousCameraRotation;
        private Vector3 cleanLocalPosition;
        private Quaternion cleanLocalRotation;
        private Vector3 appliedPosition;
        private Quaternion appliedRotation;
        private Vector2 tilt;
        private float speed;
        private float shakeAmount;
        private float headBobPhase;
        private float headBobAmplitude;
        private Transform groundSource;
        private CharacterMovementComponent detectedGroundMovement;
        private CharacterController detectedGroundController;
        private Rigidbody detectedJoltBody;
        private float previousJoltVelocity;
        private float joltOffset;
        private float joltSpringVelocity;
        private bool hasJoltSample;
        private bool previouslyGrounded;
        private float noiseTime;
        private float originalFov;
        private Camera ownedFovCamera;
        private readonly Dictionary<CinemachineCamera, float> originalLensFovs =
            new Dictionary<CinemachineCamera, float>();
        private readonly List<CinemachineCamera> liveFovCameras = new List<CinemachineCamera>();
        private readonly List<CinemachineCamera> retiredFovCameras = new List<CinemachineCamera>();
        private bool hasSample;
        private bool hasOffset;
        private bool ownsFov;
        private bool initialized;

        private void OnEnable()
        {
            if (CameraRoot == null) CameraRoot = transform;
            if (targetCamera == null) targetCamera = Camera.main;

            hasSample = false;
            speed = 0f;
            tilt = Vector2.zero;
            shakeAmount = 0f;
            headBobPhase = 0f;
            headBobAmplitude = 0f;
            groundSource = null;
            hasJoltSample = false;
            joltOffset = 0f;
            joltSpringVelocity = 0f;
            noiseTime = 0f;
            initialized = true;
        }

        // Remove last frame's cosmetic pose before ordinary movement/look Updates.
        private void Update()
        {
            RemoveOffset();
        }

        private void LateUpdate()
        {
            if (!initialized || CameraRoot == null || Time.deltaTime <= 0f) return;

            float deltaTime = Time.deltaTime;
            Transform source = ResolveVelocityTarget();
            Vector3 playerPosition = source.position;
            Vector3 cameraPosition = CameraRoot.position;
            Quaternion cameraRotation = CameraRoot.rotation;
            Vector3 velocity = Vector3.zero;
            Vector3 panVelocity = Vector3.zero;
            Vector3 angularVelocity = Vector3.zero;
            bool validVelocitySample = false;

            if (hasSample && source == previousVelocityTarget && CameraRoot == appliedRoot)
            {
                Vector3 displacement = playerPosition - previousPlayerPosition;
                Vector3 pan = cameraPosition - previousCameraPosition;
                bool teleported = teleportDistance > 0f &&
                    (displacement.magnitude > teleportDistance || pan.magnitude > teleportDistance);
                if (!teleported)
                {
                    validVelocitySample = true;
                    velocity = displacement / deltaTime;
                    panVelocity = Quaternion.Inverse(cameraRotation) * (pan / deltaTime);
                    Quaternion delta = Quaternion.Inverse(previousCameraRotation) * cameraRotation;
                    delta.ToAngleAxis(out float angle, out Vector3 axis);
                    if (angle > 180f) angle -= 360f;
                    if (Mathf.Abs(angle) > 0.001f) angularVelocity = axis * (angle / deltaTime);
                }
            }

            previousVelocityTarget = source;
            previousPlayerPosition = playerPosition;
            previousCameraPosition = cameraPosition;
            previousCameraRotation = cameraRotation;
            hasSample = true;
            bool grounded = IsGrounded(source);
            UpdateVelocityJolt(velocity.y, grounded, validVelocitySample, deltaTime);
            float horizontalSpeed = new Vector2(velocity.x, velocity.z).magnitude;
            float bobSpeedAmount = Mathf.InverseLerp(headBobStartSpeed,
                Mathf.Max(headBobStartSpeed + 0.01f, headBobFullSpeed), horizontalSpeed);
            float desiredBobAmplitude = enableHeadBob && grounded ? bobSpeedAmount * headBobHeight : 0f;
            headBobAmplitude = Mathf.Lerp(headBobAmplitude, desiredBobAmplitude,
                Blend(headBobSmoothing, deltaTime));
            if (headBobAmplitude > 0.00001f)
            {
                headBobPhase = Mathf.Repeat(headBobPhase + deltaTime * 2f * Mathf.PI *
                    Mathf.Lerp(minimumHeadBobFrequency, maximumHeadBobFrequency, bobSpeedAmount),
                    2f * Mathf.PI);
            }
            else
            {
                headBobPhase = 0f;
            }

            if (!includeVerticalSpeed) velocity.y = 0f;
            speed = Mathf.Lerp(speed, velocity.magnitude, Blend(speedSmoothing, deltaTime));
            float amount = Mathf.InverseLerp(effectStartSpeed,
                Mathf.Max(effectStartSpeed + 0.01f, fullEffectSpeed), speed);

            Vector2 desiredTilt = enableTilt
                ? new Vector2(Mathf.Clamp(angularVelocity.x * turnPitch + panVelocity.y * panPitch,
                        -maximumPitch, maximumPitch),
                    Mathf.Clamp(-angularVelocity.y * turnRoll - panVelocity.x * panRoll,
                        -maximumRoll, maximumRoll))
                : Vector2.zero;
            tilt = Vector2.Lerp(tilt, desiredTilt, Blend(tiltSmoothing, deltaTime));
            float desiredShake = enableShake ? amount * (grounded ? groundedShakeMultiplier : 1f) : 0f;
            shakeAmount = Mathf.Lerp(shakeAmount, desiredShake,
                Blend(shakeSmoothing, deltaTime));
            noiseTime += deltaTime * Mathf.Lerp(minimumShakeFrequency, maximumShakeFrequency, amount);

            appliedRoot = CameraRoot;
            cleanLocalPosition = CameraRoot.localPosition;
            cleanLocalRotation = CameraRoot.localRotation;
            appliedPosition = cleanLocalPosition + Vector3.Scale(Noise(0f), shakePosition) * shakeAmount;
            appliedPosition.y += Mathf.Sin(headBobPhase) * headBobAmplitude;
            appliedPosition.y += joltOffset;
            Vector3 rotation = new Vector3(tilt.x, 0f, tilt.y) +
                Vector3.Scale(Noise(37f), shakeRotation) * shakeAmount;
            appliedRotation = cleanLocalRotation * Quaternion.Euler(rotation);
            CameraRoot.localPosition = appliedPosition;
            CameraRoot.localRotation = appliedRotation;
            hasOffset = true;

            UpdateDynamicFov(amount, deltaTime);
        }

        private void UpdateDynamicFov(float speedAmount, float deltaTime)
        {
            if (!enableDynamicFov)
            {
                RestoreAllFovValues();
                return;
            }

            CollectLiveFovCameras();
            RestoreInactiveLensValues();
            if (liveFovCameras.Count > 0)
            {
                RestoreOutputCameraFov();
                foreach (CinemachineCamera camera in liveFovCameras)
                    UpdateCinemachineFov(camera, speedAmount, deltaTime);
                return;
            }

            // Do not fight the Brain when a different type of Cinemachine rig owns the lens.
            CinemachineBrain brain = targetCamera != null ? targetCamera.GetComponent<CinemachineBrain>() : null;
            if (brain != null && brain.isActiveAndEnabled && brain.ActiveVirtualCamera != null)
            {
                RestoreOutputCameraFov();
                return;
            }
            UpdateOutputCameraFov(speedAmount, deltaTime);
        }

        private void CollectLiveFovCameras()
        {
            liveFovCameras.Clear();
            if (cinemachineCamera != null)
            {
                liveFovCameras.Add(cinemachineCamera);
                return;
            }

            if (targetCamera == null) targetCamera = Camera.main;
            CinemachineBrain brain = targetCamera != null ? targetCamera.GetComponent<CinemachineBrain>() : null;
            if (brain != null && brain.isActiveAndEnabled)
            {
                // Includes outgoing blend cameras and live children of camera managers.
                for (int index = 0; index < CinemachineCore.VirtualCameraCount; index++)
                {
                    CinemachineCamera camera = CinemachineCore.GetVirtualCamera(index) as CinemachineCamera;
                    if (camera != null && brain.IsLiveChild(camera)) liveFovCameras.Add(camera);
                }
                return;
            }

            CinemachineCamera localCamera = CameraRoot.GetComponent<CinemachineCamera>();
            if (localCamera == null) localCamera = CameraRoot.GetComponentInParent<CinemachineCamera>();
            if (localCamera != null) liveFovCameras.Add(localCamera);
        }

        private void UpdateCinemachineFov(CinemachineCamera camera, float speedAmount, float deltaTime)
        {
            if (camera.Lens.Orthographic) return;
            if (!originalLensFovs.TryGetValue(camera, out float startingFov))
            {
                startingFov = camera.Lens.FieldOfView;
                originalLensFovs.Add(camera, startingFov);
            }
            LensSettings lens = camera.Lens;
            lens.FieldOfView = CalculateDynamicFov(lens.FieldOfView, startingFov, speedAmount, deltaTime);
            camera.Lens = lens;
        }

        private void UpdateOutputCameraFov(float speedAmount, float deltaTime)
        {
            if (ownedFovCamera != targetCamera) RestoreOutputCameraFov();
            if (targetCamera == null || targetCamera.orthographic)
            {
                RestoreOutputCameraFov();
                return;
            }
            if (!ownsFov)
            {
                ownedFovCamera = targetCamera;
                originalFov = targetCamera.fieldOfView;
                ownsFov = true;
            }
            targetCamera.fieldOfView =
                CalculateDynamicFov(targetCamera.fieldOfView, originalFov, speedAmount, deltaTime);
        }

        private float CalculateDynamicFov(float currentFov, float startingFov, float speedAmount, float deltaTime)
        {
            float restingFov = captureStartingFov ? startingFov : baseFov;
            float desiredFov = Mathf.Clamp(restingFov + extraFovAtFullSpeed * speedAmount, 1f, 179f);
            return Mathf.Lerp(currentFov, desiredFov, Blend(fovSmoothing, deltaTime));
        }

        private void RestoreInactiveLensValues()
        {
            retiredFovCameras.Clear();
            foreach (KeyValuePair<CinemachineCamera, float> entry in originalLensFovs)
            {
                if (entry.Key != null && liveFovCameras.Contains(entry.Key) && !entry.Key.Lens.Orthographic)
                    continue;
                RestoreLensFov(entry.Key, entry.Value);
                retiredFovCameras.Add(entry.Key);
            }
            foreach (CinemachineCamera camera in retiredFovCameras) originalLensFovs.Remove(camera);
        }

        private static void RestoreLensFov(CinemachineCamera camera, float fieldOfView)
        {
            if (camera == null) return;
            LensSettings lens = camera.Lens;
            lens.FieldOfView = fieldOfView;
            camera.Lens = lens;
        }

        private void RestoreOutputCameraFov()
        {
            if (ownsFov && ownedFovCamera != null) ownedFovCamera.fieldOfView = originalFov;
            ownedFovCamera = null;
            ownsFov = false;
        }

        private void RestoreAllFovValues()
        {
            foreach (KeyValuePair<CinemachineCamera, float> entry in originalLensFovs)
                RestoreLensFov(entry.Key, entry.Value);
            originalLensFovs.Clear();
            RestoreOutputCameraFov();
        }

        private Transform ResolveVelocityTarget()
        {
            if (velocityTarget != null) return velocityTarget;
            if (CameraHandler.Instance != null && CameraHandler.Instance.ActiveTarget != null)
                return CameraHandler.Instance.ActiveTarget;
            CharacterMovementComponent movement = CameraRoot.GetComponentInParent<CharacterMovementComponent>();
            return movement != null ? movement.transform : CameraRoot;
        }

        private bool IsGrounded(Transform source)
        {
            if (groundSource != source)
            {
                groundSource = source;
                detectedGroundMovement = source.GetComponentInParent<CharacterMovementComponent>();
                detectedGroundController = source.GetComponentInParent<CharacterController>();
                detectedJoltBody = source.GetComponentInParent<Rigidbody>();
            }

            // Explicit references take precedence over automatically detected components.
            if (groundMovement != null) return groundMovement.IsGrounded;
            if (groundController != null) return groundController.isGrounded;
            if (detectedGroundMovement != null) return detectedGroundMovement.IsGrounded;
            if (detectedGroundController != null) return detectedGroundController.isGrounded;
            return assumeGroundedWithoutController;
        }

        private void UpdateVelocityJolt(float measuredVelocity, bool grounded, bool validSample, float deltaTime)
        {
            // Physical velocity avoids false impulses between physics ticks and from camera follow lag.
            float verticalVelocity = measuredVelocity;
            if (groundMovement != null) verticalVelocity = groundMovement.Velocity.y;
            else if (groundController != null) verticalVelocity = groundController.velocity.y;
            else if (detectedGroundMovement != null) verticalVelocity = detectedGroundMovement.Velocity.y;
            else if (detectedGroundController != null) verticalVelocity = detectedGroundController.velocity.y;
            else if (detectedJoltBody != null) verticalVelocity = detectedJoltBody.linearVelocity.y;

            if (!validSample)
            {
                // Rebase after enable, target changes, and teleports instead of generating an impact.
                hasJoltSample = false;
                joltOffset = 0f;
                joltSpringVelocity = 0f;
            }
            else if (enableVelocityJolts && hasJoltSample)
            {
                float change = verticalVelocity - previousJoltVelocity;
                bool landed = grounded && !previouslyGrounded && previousJoltVelocity < 0f;
                float impact = landed ? Mathf.Max(Mathf.Abs(change), -previousJoltVelocity) : Mathf.Abs(change);
                if (impact > joltVelocityThreshold &&
                    (landed || impact / deltaTime > joltAccelerationThreshold))
                {
                    // Landing compresses downward; airborne kicks follow the velocity change.
                    bool downwardLanding = landed || (grounded && previousJoltVelocity < 0f && change > 0f);
                    float direction = downwardLanding ? -1f : Mathf.Sign(change);
                    float multiplier = downwardLanding ? landingJoltMultiplier : airborneJoltMultiplier;
                    joltSpringVelocity += direction * impact * joltStrength * multiplier;
                }
            }

            previousJoltVelocity = verticalVelocity;
            previouslyGrounded = grounded;
            hasJoltSample = validSample;

            // Exact critically damped spring integration remains stable across variable frame times.
            float recovery = Mathf.Max(0.01f, joltRecovery);
            float decay = Mathf.Exp(-recovery * deltaTime);
            float springTerm = joltSpringVelocity + recovery * joltOffset;
            float nextOffset = (joltOffset + springTerm * deltaTime) * decay;
            joltSpringVelocity = (joltSpringVelocity - recovery * springTerm * deltaTime) * decay;
            float limit = Mathf.Max(0f, maximumJoltOffset);
            joltOffset = Mathf.Clamp(nextOffset, -limit, limit);
            if (Mathf.Abs(nextOffset) > limit && Mathf.Sign(joltSpringVelocity) == Mathf.Sign(nextOffset))
                joltSpringVelocity = 0f;
        }

        private Vector3 Noise(float seed)
        {
            return new Vector3(Mathf.PerlinNoise(seed + 11f, noiseTime) * 2f - 1f,
                Mathf.PerlinNoise(seed + 23f, noiseTime) * 2f - 1f,
                Mathf.PerlinNoise(seed + 47f, noiseTime) * 2f - 1f);
        }

        private static float Blend(float sharpness, float deltaTime)
        {
            return sharpness <= 0f ? 1f : 1f - Mathf.Exp(-sharpness * deltaTime);
        }

        private void RemoveOffset()
        {
            if (!hasOffset || appliedRoot == null) return;
            // Preserve poses overwritten by another controller since we applied the effect.
            if (appliedRoot.localPosition == appliedPosition) appliedRoot.localPosition = cleanLocalPosition;
            if (appliedRoot.localRotation == appliedRotation) appliedRoot.localRotation = cleanLocalRotation;
            hasOffset = false;
        }

        private void OnDisable()
        {
            RemoveOffset();
            RestoreAllFovValues();
            ownsFov = false;
            initialized = false;
        }
    }
}
