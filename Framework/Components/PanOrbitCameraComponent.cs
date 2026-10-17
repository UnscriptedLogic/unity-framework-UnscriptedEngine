using UnityEngine;

namespace UnscriptedEngine
{
    /// <summary>
    /// Input-independent camera orbit and pan. Feed displacement deltas from a controller.
    /// Drives a regular camera or a Cinemachine camera with procedural position/rotation disabled.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public class PanOrbitCameraComponent : MonoBehaviour
    {
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private Transform orbitTarget;
        [SerializeField, Tooltip("World-space pivot used when Orbit Target is not assigned.")]
        private Vector3 orbitPosition;
        [SerializeField, Min(0f)] private float orbitSensitivity = 0.2f;
        [SerializeField, Min(0f)] private float panSensitivity = 0.01f;
        [SerializeField, Range(1f, 89f)] private float pitchLimit = 85f;

        private Vector3 panOffset;
        private Vector3 initialOffset;
        private float yaw;
        private float pitch;
        private float distance;
        private bool initialized;

        public Vector3 OrbitPosition
        {
            get => orbitPosition;
            set
            {
                orbitPosition = value;
                ApplyView();
            }
        }

        private Vector3 PivotPosition => orbitTarget != null ? orbitTarget.position : orbitPosition;

        /// <summary>Sets the pivot and camera, and captures the initial world-space offset for reset.</summary>
        public void Initialize(Transform view, Transform target, Vector3 offset)
        {
            cameraTransform = view;
            orbitTarget = target;
            initialized = cameraTransform != null;
            if (!initialized) return;
            initialOffset = offset.sqrMagnitude > 0.0001f ? offset : new Vector3(0f, 0f, -5f);
            ResetView();
        }

        private void Start()
        {
            if (!initialized && cameraTransform != null)
                Initialize(cameraTransform, orbitTarget, cameraTransform.position - PivotPosition);
        }

        /// <summary>Orbits from displacement, e.g. mouse pixels. Do not multiply by delta time.</summary>
        public void Orbit(Vector2 delta)
        {
            if (!initialized || !isActiveAndEnabled) return;
            yaw = Mathf.Repeat(yaw + delta.x * orbitSensitivity, 360f);
            pitch = Mathf.Clamp(pitch - delta.y * orbitSensitivity, -pitchLimit, pitchLimit);
            ApplyView();
        }

        /// <summary>Moves the orbit pivot in the camera's screen plane.</summary>
        public void Pan(Vector2 delta)
        {
            if (!initialized || !isActiveAndEnabled || cameraTransform == null) return;
            panOffset -= (cameraTransform.right * delta.x + cameraTransform.up * delta.y) * panSensitivity;
            ApplyView();
        }

        public void ResetView()
        {
            if (!initialized) return;
            panOffset = Vector3.zero;
            distance = initialOffset.magnitude;
            yaw = Mathf.Atan2(initialOffset.x, initialOffset.z) * Mathf.Rad2Deg;
            pitch = Mathf.Asin(initialOffset.y / distance) * Mathf.Rad2Deg;
            ApplyView();
        }

        private void LateUpdate() => ApplyView();

        private void ApplyView()
        {
            if (!initialized || cameraTransform == null) return;
            float elevation = pitch * Mathf.Deg2Rad;
            float azimuth = yaw * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(elevation) * Mathf.Sin(azimuth),
                Mathf.Sin(elevation), Mathf.Cos(elevation) * Mathf.Cos(azimuth)) * distance;
            Vector3 pivot = PivotPosition + panOffset;
            cameraTransform.SetPositionAndRotation(pivot + offset, Quaternion.LookRotation(-offset, Vector3.up));
        }
    }
}
