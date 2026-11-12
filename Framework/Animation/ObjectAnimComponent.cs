using UnityEngine;

namespace UnscriptedEngine
{
    public enum ObjectAnimShape { Tween, Punch, Wiggle }

    /// <summary>One local transform channel, sampled by an ObjectAnimBrain.</summary>
    public abstract class ObjectAnimComponent : MonoBehaviour
    {
        [Tooltip("Visual transform to animate. Empty uses this object. Use a separate pivot for each writer of the same property.")]
        [SerializeField] private Transform target;
        [Tooltip("Tween retains its final curve value. Punch and Wiggle return to rest at completion.")]
        [SerializeField] private ObjectAnimShape shape = ObjectAnimShape.Punch;
        [Tooltip("Tween: strength over time. Punch: decay envelope sampled from 1 to 0. Wiggle ignores this curve.")]
        [SerializeField] private AnimationCurve curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [Tooltip("Wiggle cycles per brain cycle. Whole numbers make repeating seams smooth.")]
        [Min(1f)] [SerializeField] private float oscillations = 3f;
        private Transform capturedTarget;
        private ObjectAnimBrain owner;

        public Transform Target
        {
            get => target != null ? target : transform;
            set { Restore(); target = value; }
        }
        public ObjectAnimShape Shape { get => shape; set => shape = value; }
        public AnimationCurve Curve { get => curve; set => curve = value; }
        public float Oscillations { get => oscillations; set => oscillations = Mathf.Max(1f, value); }

        internal bool Capture(ObjectAnimBrain brain)
        {
            if (!isActiveAndEnabled || (owner != null && owner != brain)) return false;
            Restore();
            capturedTarget = Target;
            owner = brain;
            CapturePose(capturedTarget);
            return true;
        }

        internal void Sample(ObjectAnimBrain brain, float progress)
        {
            if (owner != brain || capturedTarget == null || !isActiveAndEnabled) return;
            float t = Mathf.Clamp01(progress);
            float amount;
            switch (shape)
            {
                case ObjectAnimShape.Punch:
                    amount = t >= 1f ? 0f : Mathf.Sin(t * Mathf.PI) * (curve?.Evaluate(1f - t) ?? 1f);
                    break;
                case ObjectAnimShape.Wiggle:
                    amount = t >= 1f ? 0f : Mathf.Sin(t * Mathf.PI * 2f * oscillations);
                    break;
                default:
                    amount = curve?.Evaluate(t) ?? t;
                    break;
            }
            ApplyPose(capturedTarget, amount);
        }

        internal void Restore()
        {
            if (capturedTarget != null) ApplyPose(capturedTarget, 0f);
            capturedTarget = null;
            owner = null;
        }

        internal void Restore(ObjectAnimBrain brain) { if (owner == brain) Restore(); }

        protected virtual void OnDisable() => Restore();
        protected abstract void CapturePose(Transform value);
        protected abstract void ApplyPose(Transform value, float amount);
    }
}
