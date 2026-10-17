using UnityEngine;

namespace UnscriptedEngine
{
    [DisallowMultipleComponent]
    public class BodyLeanEffect : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour movement;
        private IMotionSource source;
        [SerializeField, Range(0f, 45f)] private float maxLean = 18f;
        [SerializeField, Min(1f)] private float turnRateAtFullLean = 180f;
        [SerializeField, Min(0f)] private float smoothing = 8f;
        private Quaternion _restRotation;
        private float _targetLean;
        private float _lean;

        protected virtual void Awake() => _restRotation = transform.localRotation;

        protected virtual void OnEnable()
        {
            if (movement == null) movement = GetComponentInParent<IMotionSource>() as MonoBehaviour;
            source = movement as IMotionSource;
            if (source != null) source.MotionUpdated += HandleMotion;
        }

        private void HandleMotion(float speed, float turnRate, bool grounded)
        {
            _targetLean = grounded
                ? -Mathf.Clamp(turnRate / turnRateAtFullLean, -1f, 1f) * maxLean * speed : 0f;
        }

        protected virtual void LateUpdate()
        {
            _lean = Mathf.Lerp(_lean, _targetLean, 1f - Mathf.Exp(-smoothing * Time.deltaTime));
            transform.localRotation = _restRotation * Quaternion.Euler(0f, 0f, _lean);
        }

        protected virtual void OnDisable()
        {
            if (source != null) source.MotionUpdated -= HandleMotion;
            source = null;
            _lean = _targetLean = 0f;
            transform.localRotation = _restRotation;
        }
    }
}
