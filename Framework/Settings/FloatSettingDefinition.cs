using System;
using UnityEngine;

namespace UnscriptedEngine.Settings
{
    public sealed class FloatSettingDefinition : Setting<float>
    {
        public float Minimum { get; }
        public float Maximum { get; }
        public override float DefaultValue => Normalize(AuthoredDefault);

        public FloatSettingDefinition(string id, float defaultValue, float min, float max, string label,
            string group, string description = "", SettingScope scope = SettingScope.LocalUser,
            string editorHint = "", bool advanced = false, int order = 0)
            : base(id, defaultValue, label, group, description, scope, editorHint, advanced, order)
        {
            if (!IsFinite(min) || !IsFinite(max) || min > max)
                throw new ArgumentException("Float settings need finite, ordered bounds.");
            if (!IsFinite(defaultValue)) throw new ArgumentException("The default must be finite.", nameof(defaultValue));
            Minimum = min;
            Maximum = max;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        protected override bool IsValid(float value) => IsFinite(value);
        protected override float Normalize(float value) => Mathf.Clamp(value, Minimum, Maximum);
    }
}
