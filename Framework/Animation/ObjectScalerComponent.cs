using UnityEngine;

namespace UnscriptedEngine
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Unscripted Engine/Object Motion/Object Scaler Component")]
    public sealed class ObjectScalerComponent : ObjectMotionComponent
    {
        [Tooltip("Zero locks an axis; fractions reduce its scaling rate and displacement.")]
        public Vector3 AxisScaling = Vector3.one;
        [Tooltip("Maximum absolute local scale per axis. The captured starting pose is preserved.")]
        [Min(0f)] public float MaximumThreshold = 10f;
        private Vector3 origin;
        protected override void CapturePose() => origin = transform.localScale;
        protected override float SegmentDistance(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            return Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y), Mathf.Abs(delta.z));
        }
        protected override void ApplyOffset(Vector3 from, Vector3 to, float progress, bool immediate)
        {
            Vector3 offset = Vector3.Scale(Vector3.LerpUnclamped(from, to, progress), AxisScaling);
            Vector3 result = origin + offset;
            for (int axis = 0; axis < 3; axis++)
            {
                float limit = Mathf.Max(Mathf.Abs(origin[axis]), Mathf.Max(0f, MaximumThreshold));
                result[axis] = Mathf.Clamp(result[axis], -limit, limit);
            }
            transform.localScale = result;
        }
    }
}
