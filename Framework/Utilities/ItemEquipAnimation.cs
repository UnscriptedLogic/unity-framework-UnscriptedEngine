using UnityEngine;

namespace UnscriptedEngine
{
    /// <summary>
    /// Raises the held item into its resting pose when ItemEquipComponent changes items.
    /// Attach to the equip anchor alongside FPSItemTweaks. Uses procedural offsets,
    /// so an Animator and animation clips are not required.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9980)]
    public class ItemEquipAnimation : MonoBehaviour
    {
        [Tooltip("Equipment source. Automatically found in parents when unassigned.")]
        [SerializeField] private ItemEquipComponent equipment;
        [SerializeField] private bool animateOnEnable = true;
        [Min(0f)] [SerializeField] private float duration = 0.3f;
        [Tooltip("Starting displacement in anchor-parent local units.")]
        [SerializeField] private Vector3 positionOffset = new Vector3(0f, -0.18f, -0.08f);
        [Tooltip("Starting rotation in anchor-parent space, in degrees.")]
        [SerializeField] private Vector3 rotationOffset = new Vector3(15f, 0f, -8f);
        [Tooltip("Remaining offset over normalized time: 1 starts displaced; 0 ends at rest. Overshoot is supported.")]
        [SerializeField] private AnimationCurve offsetCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

        private ItemEquipComponent subscribedEquipment;
        private ItemEquipAnimationOverride activeOverride;
        private float elapsed;
        private float activeDuration;
        private bool playing;
        private bool hasOffset;
        private Vector3 appliedPosition;
        private Quaternion appliedRotation = Quaternion.identity;

        public bool IsPlaying => playing;

        private void OnEnable()
        {
            if (equipment == null) equipment = GetComponentInParent<ItemEquipComponent>();
            subscribedEquipment = equipment;
            if (subscribedEquipment == null) return;
            subscribedEquipment.HeldItemChanged += OnHeldItemChanged;
            if (animateOnEnable) OnHeldItemChanged();
        }

        private void OnHeldItemChanged()
        {
            Stop();
            if (subscribedEquipment == null || subscribedEquipment.HeldVisual == null) return;
            Play(subscribedEquipment.HeldVisual);
        }

        /// <summary>Replay using this visual's enabled override, or the anchor defaults.</summary>
        public void Play(GameObject itemVisual)
        {
            Stop();
            if (!isActiveAndEnabled || itemVisual == null) return;
            foreach (ItemEquipAnimationOverride candidate in itemVisual.GetComponentsInChildren<ItemEquipAnimationOverride>(true))
            {
                if (!candidate.isActiveAndEnabled) continue;
                activeOverride = candidate;
                break;
            }
            activeDuration = Mathf.Max(0f, activeOverride != null ? activeOverride.Duration : duration);
            playing = activeDuration > 0f;
        }

        // FPSItemTweaks removes its previous pose first, then layers sway over this
        // animation in LateUpdate. This ordering avoids accumulation on a shared anchor.
        private void Update()
        {
            RemoveOffset();
            if (!playing) return;
            if (elapsed >= activeDuration)
            {
                Stop();
                return;
            }
            float time = Mathf.Clamp01(elapsed / activeDuration);
            if (activeOverride != null && activeOverride.isActiveAndEnabled)
                activeOverride.Evaluate(time, out appliedPosition, out appliedRotation);
            else
            {
                float amount = offsetCurve != null ? offsetCurve.Evaluate(time) : 1f - time;
                appliedPosition = positionOffset * amount;
                appliedRotation = Quaternion.Euler(rotationOffset * amount);
            }
            transform.localPosition += appliedPosition;
            transform.localRotation = appliedRotation * transform.localRotation;
            hasOffset = true;
            elapsed += Time.deltaTime;
        }

        public void Stop()
        {
            RemoveOffset();
            playing = false;
            elapsed = 0f;
            activeOverride = null;
        }

        private void RemoveOffset()
        {
            if (!hasOffset) return;
            // Item changes and disable callbacks can arrive after sway's LateUpdate.
            // Unwind that outer layer before removing our own animation pose.
            GetComponent<FPSItemTweaks>()?.RemoveOffset();
            transform.localPosition -= appliedPosition;
            transform.localRotation = Quaternion.Inverse(appliedRotation) * transform.localRotation;
            hasOffset = false;
        }

        private void OnDisable()
        {
            if (subscribedEquipment != null) subscribedEquipment.HeldItemChanged -= OnHeldItemChanged;
            subscribedEquipment = null;
            Stop();
        }
    }
}
