using UnityEngine;

namespace UnscriptedEngine.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    [AddComponentMenu("Unscripted Engine/UI/Reactive Animations/Hide")]
    public sealed class UIAnimReactive_Hide : UIAnimReactive
    {
        [SerializeField]
        [Tooltip("Hide when the threshold is reached. Disable to show only while the threshold is reached.")]
        private bool hideWhenReached = true;
        [SerializeField]
        [Min(0f)]
        [Tooltip("Seconds for a full fade. Zero changes visibility immediately. The GameObject stays active so it can react again.")]
        private float fadeDuration = 0.2f;
        private CanvasGroup group;
        private float originalAlpha;
        private bool originalInteractable;
        private bool originalBlocksRaycasts;

        protected override void OnEnable()
        {
            group = GetComponent<CanvasGroup>();
            originalAlpha = group.alpha;
            originalInteractable = group.interactable;
            originalBlocksRaycasts = group.blocksRaycasts;
            base.OnEnable();
        }
        private void Update()
        {
            float target = ThresholdReached == hideWhenReached ? 0f : originalAlpha;
            group.alpha = fadeDuration <= 0f ? target : Mathf.MoveTowards(group.alpha, target, DeltaTime / fadeDuration);
            group.interactable = target > 0f && originalInteractable;
            group.blocksRaycasts = group.alpha > 0f && originalBlocksRaycasts;
        }
        protected override void OnDisable()
        {
            base.OnDisable();
            if (group == null) return;
            group.alpha = originalAlpha;
            group.interactable = originalInteractable;
            group.blocksRaycasts = originalBlocksRaycasts;
        }
    }
}
