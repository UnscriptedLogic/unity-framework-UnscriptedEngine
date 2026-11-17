using System;
using UnityEngine;

namespace UnscriptedEngine.Settings
{
    public sealed class Vector4SettingDefinition : Setting<Vector4>
    {
        public override Vector4 DefaultValue => AuthoredDefault;

        public Vector4SettingDefinition(string id, Vector4 defaultValue, string label, string group,
            string description = "", SettingScope scope = SettingScope.LocalUser,
            string editorHint = "", bool advanced = false, int order = 0)
            : base(id, defaultValue, label, group, description, scope, editorHint, advanced, order)
        {
            if (!IsValid(defaultValue)) throw new ArgumentException("Vector4 components must be finite.", nameof(defaultValue));
        }

        protected override bool IsValid(Vector4 value) => SettingValueValidation.IsFinite(value.x) && SettingValueValidation.IsFinite(value.y) && SettingValueValidation.IsFinite(value.z) && SettingValueValidation.IsFinite(value.w);
    }
}