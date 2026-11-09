using System;
using System.Collections.Generic;
using Framework.GameplayTag;
using UnityEngine;
using UnityEngine.Events;

namespace UnscriptedEngine.UI
{
    /// <summary>Event-driven tagged value source. Subclass Publish for custom consumers.</summary>
    public class TaggedFloatBinding : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Exact gameplay tag to find, such as Stats.Health.")]
        private GameplayTag valueTag;
        [SerializeField]
        [Tooltip("Optional actor root. Otherwise use nearest TaggedValueContext, then owning canvas controller's pawn.")]
        private GameObject sourceRoot;
        [SerializeField]
        [Tooltip("Raw value on binding, changes, and unbinding. Missing values report zero.")]
        private UnityEvent<float> onValueChanged = new UnityEvent<float>();
        [SerializeField]
        [Tooltip("Clamped fraction of source minimum/maximum. Missing or zero-width values report zero.")]
        private UnityEvent<float> onNormalizedChanged = new UnityEvent<float>();
        [SerializeField]
        [Tooltip("Normalized value mapped to effect strength. Default strongest at low health; connect output to vignette intensity or alpha.")]
        private AnimationCurve response = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        [SerializeField]
        [Tooltip("Response curve output. Missing sources report zero to clear effects.")]
        private UnityEvent<float> onResponseChanged = new UnityEvent<float>();

        private readonly List<TaggedFloatValue> candidates = new List<TaggedFloatValue>();
        private TaggedValueContext context;
        private UCanvasComponent canvas;
        private GameObject resolvedRoot;
        private TaggedFloatValue source;
        public event Action Changed;
        public TaggedFloatValue Source => source;
        public bool IsBound => source != null;
        public float Value => IsBound ? source.Value : 0f;
        public float Minimum => IsBound ? source.Minimum : 0f;
        public float Maximum => IsBound ? source.Maximum : 0f;
        public float NormalizedValue => IsBound ? Mathf.InverseLerp(Minimum, Maximum, Value) : 0f;
        public float ResponseValue => IsBound ? response.Evaluate(NormalizedValue) : 0f;
        public GameplayTag ValueTag
        {
            get => valueTag;
            set { valueTag = value; Rebind(); }
        }
        public UnityEvent<float> OnValueChanged => onValueChanged;
        public UnityEvent<float> OnNormalizedChanged => onNormalizedChanged;
        public UnityEvent<float> OnResponseChanged => onResponseChanged;

        public void SetSource(GameObject root) { sourceRoot = root; Rebind(); }
        protected virtual void OnEnable() { CacheOwners(); Rebind(); }
        protected virtual void OnDisable() { Unsubscribe(); resolvedRoot = null; Publish(); }
        protected virtual void OnTransformParentChanged() { CacheOwners(); Rebind(); }
        private void CacheOwners()
        {
            context = GetComponentInParent<TaggedValueContext>(true);
            canvas = GetComponentInParent<UCanvasComponent>(true);
        }
        private GameObject ResolveRoot()
        {
            if (sourceRoot != null) return sourceRoot;
            if (context != null) return context.SourceRoot;
            UController controller = canvas != null ? canvas.OwningController : null;
            return controller != null && controller.Pawn != null ? controller.Pawn.gameObject : null;
        }
        protected virtual void LateUpdate()
        {
            // Reference checks only. Hierarchy scans happen only on binding.
            if (ResolveRoot() != resolvedRoot || (!ReferenceEquals(source, null) && source == null)) Rebind();
        }
        /// <summary>Call after modifying tags or adding/removing values within the same actor.</summary>
        public void Rebind()
        {
            Unsubscribe();
            resolvedRoot = ResolveRoot();
            if (isActiveAndEnabled && resolvedRoot != null && valueTag != null && valueTag.IsValid)
            {
                resolvedRoot.GetComponentsInChildren<TaggedFloatValue>(true, candidates);
                foreach (TaggedFloatValue candidate in candidates)
                {
                    if (!candidate.Tags.ContainsExact(valueTag)) continue;
                    if (source != null)
                    {
                        Debug.LogWarning("Multiple tagged values match this UI binding. Binding cleared; use a more specific source or tag.", this);
                        source = null;
                        break;
                    }
                    source = candidate;
                }
                candidates.Clear();
                if (source != null) source.OnValueChanged.AddListener(HandleValueChanged);
            }
            Publish();
        }
        private void Unsubscribe()
        {
            if (source != null) source.OnValueChanged.RemoveListener(HandleValueChanged);
            source = null;
        }
        private void HandleValueChanged(float value) => Publish();
        public void Refresh() => Publish();
        protected virtual void Publish()
        {
            onValueChanged.Invoke(Value);
            onNormalizedChanged.Invoke(NormalizedValue);
            onResponseChanged.Invoke(ResponseValue);
            Changed?.Invoke();
        }
    }
}
