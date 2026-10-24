using System;
using UnityEngine;

namespace UnscriptedEngine.Settings
{
    public sealed class IntSettingDefinition : Setting<int>
    {
        public int Minimum { get; }
        public int Maximum { get; }
        public override int DefaultValue => Normalize(AuthoredDefault);

        public IntSettingDefinition(string id, int defaultValue, int min, int max, string label,
            string group, string description = "", SettingScope scope = SettingScope.LocalUser,
            string editorHint = "", bool advanced = false, int order = 0)
            : base(id, defaultValue, label, group, description, scope, editorHint, advanced, order)
        {
            if (min > max) throw new ArgumentException("Integer settings need ordered bounds.");
            Minimum = min;
            Maximum = max;
        }

        protected override int Normalize(int value) => Mathf.Clamp(value, Minimum, Maximum);
    }
}
