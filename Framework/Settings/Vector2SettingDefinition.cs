using System;
using UnityEngine;

namespace UnscriptedEngine.Settings
{
    public sealed class Vector2SettingDefinition : Setting<Vector2>
    {
        public override Vector2 DefaultValue => AuthoredDefault;

        public Vector2SettingDefinition(string id, Vector2 defaultValue, string label, string group,
            string description = "", SettingScope scope = SettingScope.LocalUser,
            string editorHint = "", bool advanced = false, int order = 0)
            : base(id, defaultValue, label, group, description, scope, editorHint, advanced, order)
        {
            if (!IsValid(defaultValue)) throw new ArgumentException("Vector components must be finite.", nameof(defaultValue));
        }

        protected override bool IsValid(Vector2 value) => SettingValueValidation.IsFinite(value.x) && SettingValueValidation.IsFinite(value.y);
    }
}