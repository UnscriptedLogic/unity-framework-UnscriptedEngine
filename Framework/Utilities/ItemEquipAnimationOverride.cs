using UnityEngine;

namespace UnscriptedEngine
{
    /// <summary>
    /// Place on an item visual prefab (or a child) to replace the anchor's equip animation.
    /// Subclass and override Evaluate for custom procedural motion. Offsets are in
    /// anchor-parent space; rotation is in degrees. The anchor returns to rest on completion.
    /// </summary>
    [DisallowMultipleComponent]
    public class ItemEquipAnimationOverride : MonoBehaviour
    {
        [Min(0f)] [SerializeField] private float duration = 0.35f;
        [SerializeField] private Vector3 positionOffset = new Vector3(0f, -0.2f, -0.1f);
        [SerializeField] private Vector3 rotationOffset = new Vector3(20f, 0f, -10f);
        [Tooltip("Remaining offset over normalized time: 1 starts displaced; 0 ends at rest. Overshoot is supported.")]
        [SerializeField] private AnimationCurve offsetCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

        public virtual float Duration => duration;

        public virtual void Evaluate(float normalizedTime, out Vector3 position, out Quaternion rotation)
        {
            float amount = offsetCurve != null ? offsetCurve.Evaluate(normalizedTime) : 1f - normalizedTime;
            position = positionOffset * amount;
            rotation = Quaternion.Euler(rotationOffset * amount);
        }
    }
}
