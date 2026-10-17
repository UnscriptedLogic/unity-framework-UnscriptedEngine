using UnityEngine;
namespace UnscriptedEngine
{
    public class ValveControl : NormalizedValueControl
    {
        [SerializeField] private Transform handle;
        [SerializeField] private Vector3 closedLocalEulerAngles;
        [SerializeField] private Vector3 openLocalEulerAngles = new(0, 360, 0);
        [SerializeField, Min(0)] private float animationDuration = 0.5f;
        private float visualValue;
        protected override void ApplyVisual(bool animate)
        {
            if (handle == null) { NotifyExtreme(); return; }
            float start = visualValue, target = Value;
            Animate(animate ? animationDuration : 0, t =>
            {
                visualValue = Mathf.Lerp(start, target, t);
                handle.localEulerAngles = Vector3.Lerp(closedLocalEulerAngles, openLocalEulerAngles, visualValue);
            });
        }
    }
}
