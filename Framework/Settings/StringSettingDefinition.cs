using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnscriptedEngine.Settings
{
    [CreateAssetMenu(menuName = "Unscripted Engine/Settings/Text or Choice")]
    public sealed class StringSettingDefinition : SettingDefinition<string>
    {
        [SerializeField, Min(1), Tooltip("Maximum length of free-text values. Longer input is truncated. Ignored when Choices contains entries.")]
        private int maxLength = 256;
        [SerializeField, Tooltip("Leave empty for a text field. Add entries for a dropdown restricted to these exact strings. Strings are saved as values, not indices: reordering is safe, but renaming a choice can invalidate saved values. Set Default Value to one of these entries.")]
        private string[] choices = Array.Empty<string>();
        public IReadOnlyList<string> Choices => choices;
        public int MaxLength => Mathf.Max(1, maxLength);
        public void SetChoices(params string[] values) => choices = values ?? Array.Empty<string>();
        protected override bool IsValid(string value) => choices.Length == 0 || Array.IndexOf(choices, value) >= 0;
        protected override string Normalize(string value)
        {
            if (choices.Length > 0) return IsValid(value) ? value : choices[0];
            value ??= "";
            return value.Length <= MaxLength ? value : value.Substring(0, MaxLength);
        }
    }
}
