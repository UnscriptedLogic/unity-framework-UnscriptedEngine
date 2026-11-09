using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UnscriptedEngine.UI
{
    /// <summary>One binding can drive any combination of Slider, filled Image, and text.</summary>
    [RequireComponent(typeof(TaggedFloatBinding))]
    public sealed class TaggedFloatDisplay : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Optional read-only Slider; receives normalized value without firing Slider events.")]
        private Slider slider;
        [SerializeField]
        [Tooltip("Optional Image configured as Filled; receives normalized value.")]
        private Image fillImage;
        [SerializeField]
        [Tooltip("Optional TextMeshPro label.")]
        private TMP_Text label;
        [SerializeField]
        [Tooltip("Format: {0} value, {1} minimum, {2} maximum, {3} normalized. Example: {0:0} / {2:0}.")]
        private string format = "{0:0} / {2:0}";
        private TaggedFloatBinding binding;
        private bool warned;
        private void OnEnable()
        {
            binding = GetComponent<TaggedFloatBinding>();
            binding.Changed += Refresh;
            Refresh();
        }
        private void OnDisable()
        {
            if (binding != null) binding.Changed -= Refresh;
        }
        public void Refresh()
        {
            if (binding == null) return;
            if (slider != null)
            {
                slider.wholeNumbers = false;
                slider.minValue = 0f;
                slider.maxValue = 1f;
                slider.SetValueWithoutNotify(binding.NormalizedValue);
            }
            if (fillImage != null) fillImage.fillAmount = binding.NormalizedValue;
            if (label == null) return;
            try
            {
                label.text = string.Format(CultureInfo.CurrentCulture, format ?? "{0}", binding.Value, binding.Minimum, binding.Maximum, binding.NormalizedValue);
            }
            catch (FormatException)
            {
                label.text = binding.Value.ToString(CultureInfo.CurrentCulture);
                if (warned) return;
                warned = true;
                Debug.LogWarning("Invalid tagged float display format. Use {0}, {1}, {2}, or {3}.", this);
            }
        }
    }
}
