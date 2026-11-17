using System;
using UnityEngine;

namespace UnscriptedEngine.Settings
{
    public sealed class Vector3SettingDefinition : Setting<Vector3>
    {
        public override Vector3 DefaultValue => AuthoredDefault;

        public Vector3SettingDefinition(string id, Vector3 defaultValue, string label, string group,
            string description = "", SettingScope scope = SettingScope.LocalUser,
            string editorHint = "", bool advanced = false, int order = 0)
            : base(id, defaultValue, label, group, description, scope, editorHint, advanced, order)
        {
            if (!IsValid(defaultValue)) throw new ArgumentException("Vector components must be finite.", nameof(defaultValue));
        }

        protected override bool IsValid(Vector3 value) => SettingValueValidation.IsFinite(value.x) && SettingValueValidation.IsFinite(value.y) && SettingValueValidation.IsFinite(value.z);
    }
}