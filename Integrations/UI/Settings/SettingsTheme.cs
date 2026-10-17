using System;
using System.Collections.Generic;
using UnityEngine;
using UnscriptedEngine.Settings;

namespace UnscriptedEngine.UI.Settings
{
    public enum SettingWidgetKind { Boolean, Float, Integer, Text, Choice, Custom }

    [CreateAssetMenu(menuName = "Unscripted Engine/Settings/UI Theme")]
    public sealed class SettingsTheme : ScriptableObject
    {
        [Serializable]
        public sealed class Mapping
        {
            [Tooltip("Optional exact definition to customize. When assigned, this mapping ignores Editor Hint and Kind and takes priority over hint/type mappings.")]
            public SettingDefinition setting;
            [Tooltip("Match a definition's Editor Hint exactly. Used only when Setting is empty. A nonempty hint takes priority over Kind mappings.")]
            public string editorHint;
            [Tooltip("Value/editor category to match when both Setting and Editor Hint are empty. Choice means a string definition with a nonempty Choices list.")]
            public SettingWidgetKind kind;
            [Tooltip("Prefab containing a SettingsWidget subclass and suitable UI layout components. The panel instantiates and binds it. Empty prefab references are skipped.")]
            public SettingsWidget prefab;
        }
        [SerializeField, Tooltip("Custom widget selection rules. Exact Setting matches win, then Editor Hint, then Kind. Within each tier the first matching entry wins. Unmatched settings use built-in template controls.")]
        private List<Mapping> mappings = new();
        public SettingsWidget Resolve(SettingDefinition definition)
        {
            foreach (var mapping in mappings)
                if (mapping != null && mapping.prefab != null && mapping.setting == definition) return mapping.prefab;
            foreach (var mapping in mappings)
                if (mapping != null && mapping.prefab != null && mapping.setting == null &&
                    !string.IsNullOrEmpty(mapping.editorHint) && mapping.editorHint == definition.EditorHint) return mapping.prefab;
            foreach (var mapping in mappings)
                if (mapping != null && mapping.prefab != null && mapping.setting == null &&
                    string.IsNullOrEmpty(mapping.editorHint) && mapping.kind == KindOf(definition)) return mapping.prefab;
            return null;
        }
        public static SettingWidgetKind KindOf(SettingDefinition definition)
        {
            if (definition is BoolSettingDefinition) return SettingWidgetKind.Boolean;
            if (definition is FloatSettingDefinition) return SettingWidgetKind.Float;
            if (definition is IntSettingDefinition) return SettingWidgetKind.Integer;
            if (definition is StringSettingDefinition text)
                return text.Choices.Count > 0 ? SettingWidgetKind.Choice : SettingWidgetKind.Text;
            return SettingWidgetKind.Custom;
        }
    }
}
