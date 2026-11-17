using System;
using UnityEngine;

namespace UnscriptedEngine.Settings
{
    public sealed class QuaternionSettingDefinition : Setting<Quaternion>
    {
        public override Quaternion DefaultValue => AuthoredDefault;

        public QuaternionSettingDefinition(string id, Quaternion defaultValue, string label, string group,
            string description = "", SettingScope scope = SettingScope.LocalUser,
            string editorHint = "", bool advanced = false, int order = 0)
            : base(id, defaultValue, label, group, description, scope, editorHint, advanced, order)
        {
            if (!IsValid(defaultValue)) throw new ArgumentException("Quaternion components must be finite.", nameof(defaultValue));
        }

        protected override bool IsValid(Quaternion value) => SettingValueValidation.IsFinite(value.x) && SettingValueValidation.IsFinite(value.y) && SettingValueValidation.IsFinite(value.z) && SettingValueValidation.IsFinite(value.w);
    }
}