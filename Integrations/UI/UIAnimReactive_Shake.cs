using UnityEngine;

namespace UnscriptedEngine.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("Unscripted Engine/UI/Reactive Animations/Shake")]
    public sealed class UIAnimReactive_Shake : UIAnimReactive
    {
        [SerializeField]
        [Tooltip("Maximum horizontal and vertical displacement in local UI units. Put shake on a visual child below layout-controlled objects.")]
        private Vector2 amplitude = new Vector2(6f, 6f);
        [SerializeField]
        [Min(0f)]
        [Tooltip("Noise sampling speed in cycles per second.")]
        private float frequency = 20f;
        [SerializeField]
        [Tooltip("Maps threshold progress to shake amplitude. Progress is zero at threshold and one at the selected limit.")]
        private AnimationCurve intensity = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        [Header("Debug")]
        [SerializeField]
        [Tooltip("Draw the maximum shake displacement around this UI element when selected.")]
        private bool showGizmos = true;

        private RectTransform target;
        private Vector3 appliedOffset;
        private float phase;
        protected override void OnEnable()
        {
            target = (RectTransform)transform;
            appliedOffset = Vector3.zero;
            phase = 0f;
            base.OnEnable();
        }
        private void LateUpdate()
        {
            RemoveOffset();
            if (Strength <= 0f) return;
            phase = Mathf.Repeat(phase + DeltaTime * Mathf.Max(0f, frequency), 4096f);
            float weight = Mathf.Clamp01(intensity.Evaluate(Strength));
            appliedOffset = new Vector3((Mathf.PerlinNoise(phase, 17.1f) * 2f - 1f) * amplitude.x,
                (Mathf.PerlinNoise(43.7f, phase) * 2f - 1f) * amplitude.y, 0f) * weight;
            target.anchoredPosition3D += appliedOffset;
        }
        private void RemoveOffset()
        {
            if (target != null) target.anchoredPosition3D -= appliedOffset;
            appliedOffset = Vector3.zero;
        }
        protected override void OnDisable()
        {
            RemoveOffset();
            base.OnDisable();
        }
        private void OnDrawGizmosSelected()
        {
            if (!showGizmos) return;
            Matrix4x4 previous = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(Mathf.Abs(amplitude.x) * 2f, Mathf.Abs(amplitude.y) * 2f, 0f));
            Gizmos.matrix = previous;
#if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.position, "Maximum shake displacement (UI units)");
#endif
        }
    }
}
