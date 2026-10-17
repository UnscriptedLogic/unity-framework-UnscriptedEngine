using Unity.Cinemachine;
using UnityEngine;

namespace UnscriptedEngine
{
    // Lives on CameraAnchor. Pipeline extensions keep cosmetic motion separate
    // from the anchor rotation used by pawn aiming and camera mode handoffs.
    [DisallowMultipleComponent]
    public class MotionCameraEffect : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour movement;
        private IMotionSource source;
        [SerializeField, Range(0f, 40f)] private float fovIncreaseAtFullSpeed = 12f;
        [SerializeField] private AnimationCurve fovBySpeed = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField, Min(0f)] private float swayDistance = 0.035f;
        [SerializeField, Range(0f, 10f)] private float tiltDegrees = 1.5f;
        [SerializeField, Min(0f)] private float cyclesPerSecond = 2f;
        [SerializeField, Min(0f)] private float smoothing = 6f;
        private UPawn _pawn;
        private MotionCameraExtension[] _extensions;
        private float _speed, _groundedSpeed, _phase;
        private float _targetSpeed, _targetGroundedSpeed;
        private int _lastFrame = -1;

        protected virtual void Awake()
        {
            _pawn = GetComponentInParent<UPawn>();
            if (movement == null) movement = GetComponentInParent<IMotionSource>() as MonoBehaviour;
            if (_pawn == null) return;
            var cameras = _pawn.GetComponentsInChildren<CinemachineCamera>(true);
            _extensions = new MotionCameraExtension[cameras.Length];
            for (int i = 0; i < cameras.Length; i++)
            {
                _extensions[i] = cameras[i].gameObject.AddComponent<MotionCameraExtension>();
                _extensions[i].Source = this;
            }
        }

        protected virtual void OnEnable()
        {
            source = movement as IMotionSource;
            if (source != null) source.MotionUpdated += HandleMotion;
        }

        private void HandleMotion(float speed, float turnRate, bool grounded)
        {
            _targetSpeed = speed;
            _targetGroundedSpeed = grounded ? speed : 0f;
        }

        public void Apply(ref CameraState state)
        {
            if (!isActiveAndEnabled || _pawn == null || !_pawn.IsSpawned || !_pawn.IsOwner) return;
            // Each camera can update multiple times; integrate only once per frame.
            if (_lastFrame != Time.frameCount)
            {
                _lastFrame = Time.frameCount;
                float blend = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
                _speed = Mathf.Lerp(_speed, _targetSpeed, blend);
                _groundedSpeed = Mathf.Lerp(_groundedSpeed, _targetGroundedSpeed, blend);
                _phase = Mathf.Repeat(_phase + Time.deltaTime * cyclesPerSecond * Mathf.PI * 2f
                    * Mathf.Lerp(0.5f, 1f, _speed), Mathf.PI * 2f);
            }
            var lens = state.Lens;
            lens.FieldOfView = Mathf.Clamp(lens.FieldOfView
                + fovBySpeed.Evaluate(_speed) * fovIncreaseAtFullSpeed, 1f, 179f);
            state.Lens = lens;
            float wave = Mathf.Sin(_phase) * _groundedSpeed;
            state.PositionCorrection += state.RawOrientation * new Vector3(wave * swayDistance, 0f, 0f);
            state.OrientationCorrection *= Quaternion.Euler(0f, 0f, wave * tiltDegrees);
        }

        protected virtual void OnDisable()
        {
            if (source != null) source.MotionUpdated -= HandleMotion;
            source = null;
            _speed = _groundedSpeed = _targetSpeed = _targetGroundedSpeed = _phase = 0f;
            _lastFrame = -1;
        }

        protected virtual void OnDestroy()
        {
            if (_extensions == null) return;
            foreach (var extension in _extensions)
                if (extension != null) Destroy(extension);
        }
    }

}
