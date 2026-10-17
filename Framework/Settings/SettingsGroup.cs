using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnscriptedEngine.Settings
{
    [CreateAssetMenu(menuName = "Unscripted Engine/Settings/Group")]
    public sealed class SettingsGroup : ScriptableObject
    {
        [SerializeField, Tooltip("Collapsible section label in the generated UI. Uses the asset name when empty. Renaming the group does not change setting IDs or preferences.")]
        private string displayName;
        [SerializeField, Tooltip("Display priority among sibling groups or active root groups. Lower numbers appear first; ties are sorted by display name.")]
        private int order;
        [SerializeField, Tooltip("Definitions displayed directly in this section. A module activates them together with all child groups. Assign existing definition assets; missing references and conflicting IDs are rejected.")]
        private List<SettingDefinition> settings = new();
        [SerializeField, Tooltip("Nested sections, e.g. Controls inside General. A module activates the entire tree. Do not reference this group or any ancestor; cycles and missing references are rejected.")]
        private List<SettingsGroup> children = new();
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public int Order => order;
        public IReadOnlyList<SettingDefinition> Settings => settings;
        public IReadOnlyList<SettingsGroup> Children => children;
        public void Configure(string label, IEnumerable<SettingDefinition> entries, params SettingsGroup[] groups)
        {
            displayName = label;
            settings = entries == null ? new() : new(entries);
            children = groups == null ? new() : new(groups);
        }

        public List<SettingDefinition> Flatten()
        {
            var result = new List<SettingDefinition>();
            Visit(this, new HashSet<SettingsGroup>(), new HashSet<SettingDefinition>(), result);
            return result;
        }

        private static void Visit(SettingsGroup group, HashSet<SettingsGroup> path,
            HashSet<SettingDefinition> seen, List<SettingDefinition> result)
        {
            if (group == null) throw new ArgumentException("Settings groups cannot contain missing child references.");
            if (!path.Add(group)) throw new ArgumentException($"Settings group cycle at '{group.name}'.");
            foreach (var setting in group.settings)
            {
                if (setting == null) throw new ArgumentException($"Missing setting in '{group.name}'.");
                if (seen.Add(setting)) result.Add(setting);
            }
            foreach (var child in group.children) Visit(child, path, seen, result);
            path.Remove(group);
        }
    }
}
