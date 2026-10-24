using System;
using System.Collections.Generic;
using System.Linq;

namespace UnscriptedEngine.Settings
{
    /// <summary>Runtime UI projection of active code definitions. Not an authored asset.</summary>
    public sealed class SettingsGroup
    {
        private readonly List<SettingDefinition> settings = new List<SettingDefinition>();
        private readonly List<SettingsGroup> children = new List<SettingsGroup>();
        public string DisplayName { get; }
        public string Path { get; }
        public IReadOnlyList<SettingDefinition> Settings => settings.AsReadOnly();
        public IReadOnlyList<SettingsGroup> Children => children.AsReadOnly();

        private SettingsGroup(string displayName, string path)
        {
            DisplayName = displayName;
            Path = path;
        }

        public static IReadOnlyList<SettingsGroup> BuildTree(IEnumerable<SettingDefinition> definitions)
        {
            Dictionary<string, SettingsGroup> groups = new Dictionary<string, SettingsGroup>(StringComparer.Ordinal);
            List<SettingsGroup> roots = new List<SettingsGroup>();
            foreach (SettingDefinition definition in definitions)
            {
                SettingsGroup parent = null;
                string path = "";
                foreach (string segment in definition.Group.Split('/'))
                {
                    path = path.Length == 0 ? segment : path + "/" + segment;
                    if (!groups.TryGetValue(path, out SettingsGroup group))
                    {
                        group = new SettingsGroup(segment, path);
                        groups.Add(path, group);
                        if (parent == null) roots.Add(group);
                        else parent.children.Add(group);
                    }
                    parent = group;
                }
                parent.settings.Add(definition);
            }
            return roots.OrderBy(group => group.DisplayName, StringComparer.Ordinal).ToList().AsReadOnly();
        }
    }
}
