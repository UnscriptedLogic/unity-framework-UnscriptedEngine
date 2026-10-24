using System;
using UnityEngine;

namespace UnscriptedEngine.Settings
{
    public enum SettingScope { LocalUser, ServerSession }
    public enum SettingsRole { Standalone, Server, Client }

    /// <summary>Immutable authored metadata. Referencing a definition never activates it.</summary>
    public abstract class SettingDefinition
    {
        public string Id { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public string Group { get; }
        public SettingScope Scope { get; }
        public string EditorHint { get; }
        public bool Advanced { get; }
        public int Order { get; }
        public abstract Type ValueType { get; }
        public abstract object DefaultObject { get; }

        protected SettingDefinition(string id, string label, string group, string description,
            SettingScope scope, string editorHint, bool advanced, int order)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A setting needs a stable ID.", nameof(id));
            if (string.IsNullOrWhiteSpace(group)) throw new ArgumentException("A setting needs a group path.", nameof(group));
            string[] segments = group.Split('/');
            for (int index = 0; index < segments.Length; index++)
            {
                segments[index] = segments[index].Trim();
                if (segments[index].Length == 0) throw new ArgumentException("Group paths cannot contain empty sections.", nameof(group));
            }
            Id = id;
            DisplayName = string.IsNullOrWhiteSpace(label) ? id : label;
            Group = string.Join("/", segments);
            Description = description ?? "";
            Scope = scope;
            EditorHint = editorHint ?? "";
            Advanced = advanced;
            Order = order;
        }

        public abstract bool TryNormalize(object value, out object normalized);
        public abstract string SerializeValue(object value);
        public abstract bool TryDeserializeValue(string json, out object value);
    }

    public abstract class Setting<T> : SettingDefinition
    {
        [Serializable]
        private sealed class ValueEnvelope { public T value; }
        protected T AuthoredDefault { get; }
        public abstract T DefaultValue { get; }
        public override Type ValueType => typeof(T);
        public override object DefaultObject => DefaultValue;

        protected Setting(string id, T defaultValue, string label, string group, string description,
            SettingScope scope, string editorHint, bool advanced, int order)
            : base(id, label, group, description, scope, editorHint, advanced, order)
        {
            AuthoredDefault = defaultValue;
        }

        protected virtual bool IsValid(T value) => true;
        protected virtual T Normalize(T value) => value;

        public override bool TryNormalize(object value, out object normalized)
        {
            if (value is T typedValue && IsValid(typedValue))
            {
                normalized = Normalize(typedValue);
                return true;
            }
            normalized = null;
            return false;
        }

        public override string SerializeValue(object value)
        {
            return JsonUtility.ToJson(new ValueEnvelope { value = (T)value });
        }

        public override bool TryDeserializeValue(string json, out object value)
        {
            value = null;
            if (string.IsNullOrWhiteSpace(json)) return false;
            try
            {
                ValueEnvelope envelope = JsonUtility.FromJson<ValueEnvelope>(json);
                return envelope != null && TryNormalize(envelope.value, out value);
            }
            catch (ArgumentException) { return false; }
        }
    }
}
