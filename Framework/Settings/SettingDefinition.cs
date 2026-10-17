using System;
using UnityEngine;

namespace UnscriptedEngine.Settings
{
    public enum SettingScope { LocalUser, ServerSession }
    public enum SettingsRole { Standalone, Server, Client }

    /// <summary>Authored metadata only. Runtime values belong to SettingsService.</summary>
    public abstract class SettingDefinition : ScriptableObject
    {
        [SerializeField, Tooltip("Permanent, application-wide unique key used for saved preferences and network synchronization, e.g. grapple.experimental.spring-strength. Keep it unchanged when renaming or regrouping this setting.")]
        private string id;
        [SerializeField, Tooltip("Label shown in the settings UI. Uses the asset name when empty; changing this label does not change saved preferences.")]
        private string displayName;
        [SerializeField, TextArea, Tooltip("Help text displayed beneath the setting in the default UI and included in settings searches.")]
        private string description;
        [SerializeField, Tooltip("LocalUser saves a preference on this device. ServerSession is writable by the server or in standalone mode, read-only on clients, and not saved as a local preference. Multiplayer requires a configured network bridge.")]
        private SettingScope scope;
        [SerializeField, Tooltip("Optional key matched by a SettingsTheme mapping, e.g. audio-volume. Selects a custom editor without changing the value type. Leave empty to use the type's default editor.")]
        private string editorHint;
        [SerializeField, Tooltip("Hide this setting until Show advanced settings is enabled in the generated panel. This affects visibility only, not activation or its effective value.")]
        private bool advanced;
        [SerializeField, Tooltip("Display priority within its group. Lower numbers appear first; equal priorities are sorted by display name.")]
        private int order;

        public string Id => id;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public SettingScope Scope => scope;
        public string EditorHint => editorHint;
        public bool Advanced => advanced;
        public int Order => order;
        public abstract Type ValueType { get; }
        public abstract object DefaultObject { get; }
        public abstract bool TryNormalize(object value, out object normalized);
        public abstract string SerializeValue(object value);
        public abstract bool TryDeserializeValue(string json, out object value);

        // For runtime definitions, tools, and tests. Do not change identity after registration.
        public void Configure(string stableId, string label, SettingScope settingScope = SettingScope.LocalUser)
        {
            if (string.IsNullOrWhiteSpace(stableId)) throw new ArgumentException("A setting needs a stable ID.");
            id = stableId;
            displayName = label;
            scope = settingScope;
        }
    }

    public abstract class SettingDefinition<T> : SettingDefinition
    {
        [SerializeField, Tooltip("Fallback when no override is active, and the value used after Reset. Validation normalizes this value. Removing the last local module restores it; clients still use replicated server values for ServerSession settings.")]
        private T defaultValue;
        [Serializable] private sealed class Box { public T value; }
        public T DefaultValue => Normalize(defaultValue);
        public override Type ValueType => typeof(T);
        public override object DefaultObject => DefaultValue;
        protected virtual T Normalize(T value) => value;
        protected virtual bool IsValid(T value) => true;
        public void SetDefault(T value) => defaultValue = value;

        public override bool TryNormalize(object value, out object normalized)
        {
            if (value is T typed && IsValid(typed))
            {
                normalized = Normalize(typed);
                return true;
            }
            normalized = null;
            return false;
        }

        public override string SerializeValue(object value) => JsonUtility.ToJson(new Box { value = (T)value });
        public override bool TryDeserializeValue(string json, out object value)
        {
            value = null;
            if (string.IsNullOrWhiteSpace(json)) return false;
            try
            {
                var box = JsonUtility.FromJson<Box>(json);
                return box != null && TryNormalize(box.value, out value);
            }
            catch (ArgumentException) { return false; }
        }
    }
}
