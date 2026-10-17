using UnityEngine;

namespace UnscriptedEngine.Settings
{
    [CreateAssetMenu(menuName = "Unscripted Engine/Settings/Float")]
    public sealed class FloatSettingDefinition : SettingDefinition<float>
    {
        [SerializeField, Tooltip("Lowest accepted value and the default slider's lower bound. Defaults and writes below it are clamped. Use a finite number.")]
        private float minimum;
        [SerializeField, Tooltip("Highest accepted value and the default slider's upper bound. Defaults and writes above it are clamped. Use a finite number at least as large as Minimum.")]
        private float maximum = 1f;
        public float Minimum => minimum;
        public float Maximum => Mathf.Max(minimum, maximum);
        public void SetRange(float min, float max) { minimum = min; maximum = max; }
        protected override bool IsValid(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        protected override float Normalize(float value) => Mathf.Clamp(IsValid(value) ? value : minimum, Minimum, Maximum);
    }
}
