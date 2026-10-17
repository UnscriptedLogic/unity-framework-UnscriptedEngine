using UnityEngine;
namespace UnscriptedEngine
{
    public class LeverControl : NormalizedValueControl
    {
        [SerializeField] private Transform handle;
        [SerializeField] private Vector3 closedLocalEulerAngles;
        [SerializeField] private Vector3 openLocalEulerAngles;
        [SerializeField, Min(0)] private float animationDuration = 0.25f;
        protected override void ApplyVisual(bool animate)
        {
            if (handle == null) { NotifyExtreme(); return; }
            Quaternion start = handle.localRotation;
            Quaternion target = Quaternion.Euler(Vector3.Lerp(closedLocalEulerAngles, openLocalEulerAngles, Value));
            Animate(animate ? animationDuration : 0, t => handle.localRotation = Quaternion.Slerp(start, target, t));
        }
    }
}
