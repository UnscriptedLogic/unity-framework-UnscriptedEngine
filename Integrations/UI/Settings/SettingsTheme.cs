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
            [Tooltip("Optional exact stable ID, e.g. general.camera.dynamic-fov. Takes priority over Editor Hint and Kind; leave empty for a broader match.")]
            public string settingId;
            [Tooltip("Match the code definition's EditorHint exactly when Setting Id is empty. Takes priority over Kind.")]
            public string editorHint;
            [Tooltip("Used when Setting Id and Editor Hint are empty. Choice means a string setting with a fixed list of values.")]
            public SettingWidgetKind kind;
            [Tooltip("Prefab containing a SettingsWidget subclass and suitable layout components. Empty prefab references are skipped.")]
            public SettingsWidget prefab;
        }

        [SerializeField, Tooltip("Exact setting IDs win, then editor hints, then value kinds. The first matching entry in each tier wins. Unmatched settings use template controls.")]
        private List<Mapping> mappings = new List<Mapping>();

        public SettingsWidget Resolve(SettingDefinition definition)
        {
            SettingsWidget exactMatch = FindExactMapping(definition.Id);
            if (exactMatch != null) return exactMatch;
            SettingsWidget hintMatch = FindHintMapping(definition.EditorHint);
            return hintMatch != null ? hintMatch : FindKindMapping(KindOf(definition));
        }

        private SettingsWidget FindExactMapping(string id)
        {
            foreach (Mapping mapping in mappings)
                if (IsUsable(mapping) && mapping.settingId == id) return mapping.prefab;
            return null;
        }

        private SettingsWidget FindHintMapping(string hint)
        {
            if (string.IsNullOrEmpty(hint)) return null;
            foreach (Mapping mapping in mappings)
                if (IsUsable(mapping) && string.IsNullOrEmpty(mapping.settingId) &&
                    mapping.editorHint == hint) return mapping.prefab;
            return null;
        }

        private SettingsWidget FindKindMapping(SettingWidgetKind kind)
        {
            foreach (Mapping mapping in mappings)
                if (IsUsable(mapping) && string.IsNullOrEmpty(mapping.settingId) &&
                    string.IsNullOrEmpty(mapping.editorHint) && mapping.kind == kind) return mapping.prefab;
            return null;
        }

        private static bool IsUsable(Mapping mapping) => mapping != null && mapping.prefab != null;

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
