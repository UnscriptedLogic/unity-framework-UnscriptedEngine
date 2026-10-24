namespace UnscriptedEngine.Settings
{
    /// <summary>Declare public static readonly settings in a code catalog using these factories.</summary>
    public static class Setting
    {
        public static Setting<bool> Bool(string id, bool defaultValue, string label, string group,
            string description = "", SettingScope scope = SettingScope.LocalUser,
            string editorHint = "", bool advanced = false, int order = 0)
        {
            return new BoolSettingDefinition(id, defaultValue, label, group, description, scope, editorHint, advanced, order);
        }

        public static Setting<float> Float(string id, float defaultValue, float min, float max, string label,
            string group, string description = "", SettingScope scope = SettingScope.LocalUser,
            string editorHint = "", bool advanced = false, int order = 0)
        {
            return new FloatSettingDefinition(id, defaultValue, min, max, label, group, description, scope, editorHint, advanced, order);
        }

        public static Setting<int> Int(string id, int defaultValue, int min, int max, string label,
            string group, string description = "", SettingScope scope = SettingScope.LocalUser,
            string editorHint = "", bool advanced = false, int order = 0)
        {
            return new IntSettingDefinition(id, defaultValue, min, max, label, group, description, scope, editorHint, advanced, order);
        }

        public static Setting<string> Text(string id, string defaultValue, string label, string group,
            int maxLength = 256, string description = "", SettingScope scope = SettingScope.LocalUser,
            string editorHint = "", bool advanced = false, int order = 0)
        {
            return new StringSettingDefinition(id, defaultValue, label, group, maxLength, null, description, scope, editorHint, advanced, order);
        }

        public static Setting<string> Choice(string id, string defaultValue, string[] choices, string label,
            string group, string description = "", SettingScope scope = SettingScope.LocalUser,
            string editorHint = "", bool advanced = false, int order = 0)
        {
            return new StringSettingDefinition(id, defaultValue, label, group, 256, choices, description, scope, editorHint, advanced, order);
        }
    }
}
