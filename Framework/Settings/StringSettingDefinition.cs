using System;
using System.Collections.Generic;

namespace UnscriptedEngine.Settings
{
    public sealed class StringSettingDefinition : Setting<string>
    {
        private readonly string[] choiceValues;
        public IReadOnlyList<string> Choices { get; }
        public int MaxLength { get; }
        public override string DefaultValue => Normalize(AuthoredDefault);

        public StringSettingDefinition(string id, string defaultValue, string label, string group,
            int maxLength = 256, string[] choices = null, string description = "",
            SettingScope scope = SettingScope.LocalUser, string editorHint = "", bool advanced = false, int order = 0)
            : base(id, defaultValue, label, group, description, scope, editorHint, advanced, order)
        {
            if (maxLength < 1) throw new ArgumentOutOfRangeException(nameof(maxLength));
            MaxLength = maxLength;
            choiceValues = choices == null ? Array.Empty<string>() : (string[])choices.Clone();
            HashSet<string> uniqueChoices = new HashSet<string>(StringComparer.Ordinal);
            foreach (string choice in choiceValues)
                if (choice == null || !uniqueChoices.Add(choice))
                    throw new ArgumentException("Choices must be non-null and unique.", nameof(choices));
            if (defaultValue == null || !IsValid(defaultValue))
                throw new ArgumentException("The default must be non-null and match an available choice.", nameof(defaultValue));
            Choices = Array.AsReadOnly(choiceValues);
        }

        protected override bool IsValid(string value) => choiceValues.Length == 0 || Array.IndexOf(choiceValues, value) >= 0;
        protected override string Normalize(string value)
        {
            if (choiceValues.Length > 0) return value;
            return value.Length <= MaxLength ? value : value.Substring(0, MaxLength);
        }
    }
}
