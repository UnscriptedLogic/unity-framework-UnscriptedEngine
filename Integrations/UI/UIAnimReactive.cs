using UnityEngine;

namespace UnscriptedEngine.UI
{
    public enum UIReactiveLimit { Minimum, Maximum }

    /// <summary>Shared normalized threshold and lifecycle for reactive UI effects.</summary>
    public abstract class UIAnimReactive : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Value binding to observe. Defaults to the nearest binding on this object or its parents.")]
        private TaggedFloatBinding binding;
        [SerializeField]
        [Tooltip("Minimum reacts at or below the percentage; Maximum reacts at or above it.")]
        private UIReactiveLimit limit = UIReactiveLimit.Minimum;
        [SerializeField]
        [Range(0f, 100f)]
        [Tooltip("Percentage of the source's minimum-to-maximum range where the effect starts. 25 means 25%, regardless of raw bounds.")]
        private float percentage = 25f;
        [SerializeField]
        [Tooltip("Animate while the game is paused by using unscaled time.")]
        private bool useUnscaledTime = true;

        private TaggedFloatBinding subscribedBinding;
        public bool ThresholdReached { get; private set; }
        public float Strength { get; private set; }
        protected float DeltaTime => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        public TaggedFloatBinding Binding
        {
            get => binding;
            set { binding = value; if (isActiveAndEnabled) Subscribe(); }
        }

        protected virtual void OnEnable() => Subscribe();
        protected virtual void OnDisable()
        {
            Unsubscribe();
            ThresholdReached = false;
            Strength = 0f;
        }
        protected virtual void OnTransformParentChanged()
        {
            if (isActiveAndEnabled) Subscribe();
        }
        private void Subscribe()
        {
            Unsubscribe();
            subscribedBinding = binding != null ? binding : GetComponentInParent<TaggedFloatBinding>(true);
            if (subscribedBinding != null) subscribedBinding.Changed += Refresh;
            Refresh();
        }
        private void Unsubscribe()
        {
            if (subscribedBinding != null) subscribedBinding.Changed -= Refresh;
            subscribedBinding = null;
        }
        public void Refresh()
        {
            bool available = subscribedBinding != null && subscribedBinding.isActiveAndEnabled && subscribedBinding.IsBound;
            float value = available ? subscribedBinding.NormalizedValue : 0f;
            float threshold = Mathf.Clamp01(percentage / 100f);
            ThresholdReached = available && (limit == UIReactiveLimit.Minimum ? value <= threshold : value >= threshold);
            Strength = !ThresholdReached ? 0f : limit == UIReactiveLimit.Minimum
                ? (threshold <= 0f ? 1f : Mathf.Clamp01((threshold - value) / threshold))
                : (threshold >= 1f ? 1f : Mathf.Clamp01((value - threshold) / (1f - threshold)));
        }
    }
}
