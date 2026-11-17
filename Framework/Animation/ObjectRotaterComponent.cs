using UnityEngine;

namespace UnscriptedEngine
{
    public enum ObjectRotationRoute { Shortest, FullAngles }

    [DisallowMultipleComponent]
    [AddComponentMenu("Unscripted Engine/Object Motion/Object Rotater Component")]
    public sealed class ObjectRotaterComponent : ObjectMotionComponent
    {
        public ObjectRotationRoute RotationRoute;
        private Quaternion origin;
        protected override void CapturePose() => origin = transform.rotation;
        protected override float SegmentDistance(Vector3 from, Vector3 to) => RotationRoute == ObjectRotationRoute.Shortest
            ? Quaternion.Angle(Quaternion.Euler(from), Quaternion.Euler(to)) : Vector3.Distance(from, to);
        protected override void ApplyOffset(Vector3 from, Vector3 to, float progress, bool immediate)
        {
            Quaternion offset = RotationRoute == ObjectRotationRoute.Shortest
                ? Quaternion.SlerpUnclamped(Quaternion.Euler(from), Quaternion.Euler(to), progress)
                : Quaternion.Euler(Vector3.LerpUnclamped(from, to, progress));
            Quaternion rotation = origin * offset;
            if (UsesPhysics && Body != null)
            {
                if (immediate) Body.rotation = rotation;
                else Body.MoveRotation(rotation);
            }
            else transform.rotation = rotation;
        }
    }
}
