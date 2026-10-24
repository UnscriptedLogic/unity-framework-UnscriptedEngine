namespace UnscriptedEngine.Settings
{
    public sealed class BoolSettingDefinition : Setting<bool>
    {
        public override bool DefaultValue => AuthoredDefault;
        public BoolSettingDefinition(string id, bool defaultValue, string label, string group,
            string description = "", SettingScope scope = SettingScope.LocalUser,
            string editorHint = "", bool advanced = false, int order = 0)
            : base(id, defaultValue, label, group, description, scope, editorHint, advanced, order) { }
    }
}
