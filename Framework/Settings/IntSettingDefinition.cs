using UnityEngine;

namespace UnscriptedEngine.Settings
{
    [CreateAssetMenu(menuName = "Unscripted Engine/Settings/Integer")]
    public sealed class IntSettingDefinition : SettingDefinition<int>
    {
        [SerializeField, Tooltip("Lowest accepted integer and the default slider's lower bound. Defaults and writes below it are clamped.")]
        private int minimum;
        [SerializeField, Tooltip("Highest accepted integer and the default slider's upper bound. Values above it are clamped. Keep it at least as large as Minimum; use a custom editor for integers beyond float slider precision.")]
        private int maximum = 100;
        public int Minimum => minimum;
        public int Maximum => Mathf.Max(minimum, maximum);
        public void SetRange(int min, int max) { minimum = min; maximum = max; }
        protected override int Normalize(int value) => Mathf.Clamp(value, Minimum, Maximum);
    }
}
