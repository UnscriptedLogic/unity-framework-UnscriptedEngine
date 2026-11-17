using UnityEngine;

namespace UnscriptedEngine
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Unscripted Engine/Object Motion/Object Translater Component")]
    public sealed class ObjectTranslaterComponent : ObjectMotionComponent
    {
        private Vector3 origin;
        protected override void CapturePose() => origin = transform.position;
        protected override float SegmentDistance(Vector3 from, Vector3 to) => Vector3.Distance(from, to);
        protected override void ApplyOffset(Vector3 from, Vector3 to, float progress, bool immediate)
        {
            Vector3 position = origin + Vector3.LerpUnclamped(from, to, progress);
            if (UsesPhysics && Body != null)
            {
                if (immediate) Body.position = position;
                else Body.MovePosition(position);
            }
            else transform.position = position;
        }
    }
}
