using System;
using UnityEngine;

namespace UnscriptedEngine.Settings
{
    public sealed class ColorSettingDefinition : Setting<Color>
    {
        public override Color DefaultValue => AuthoredDefault;

        public ColorSettingDefinition(string id, Color defaultValue, string label, string group,
            string description = "", SettingScope scope = SettingScope.LocalUser,
            string editorHint = "", bool advanced = false, int order = 0)
            : base(id, defaultValue, label, group, description, scope, editorHint, advanced, order)
        {
            if (!IsValid(defaultValue)) throw new ArgumentException("Color components must be finite.", nameof(defaultValue));
        }

        protected override bool IsValid(Color value) => SettingValueValidation.IsFinite(value.r) && SettingValueValidation.IsFinite(value.g) && SettingValueValidation.IsFinite(value.b) && SettingValueValidation.IsFinite(value.a);
    }
}