using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using Unity.Netcode;
using UnscriptedEngine.Settings;

namespace UnscriptedEngine.Configuration
{
    /// <summary>Implement when a serialized field needs validation, a setter, or rebuilding at runtime.
    /// The adapter owns assignment and must leave state unchanged when rejecting a write.</summary>
    public interface IConfigurableFieldAdapter
    {
        bool TrySetConfiguration(string fieldName, object value, out string reason);
    }

    public sealed class ConfigurableField
    {
        public MonoBehaviour Component { get; }
        public FieldInfo Field { get; }
        public SettingDefinition Definition { get; }

        private ConfigurableField(MonoBehaviour component, FieldInfo field, SettingDefinition definition)
        { Component = component; Field = field; Definition = definition; }

        public object Read()
        {
            if (Component == null) return Definition.DefaultObject;
            object value = Field.GetValue(Component);
            return Field.FieldType.IsEnum ? value.ToString() : value ?? "";
        }

        internal bool TryWrite(object value, out string reason)
        {
            reason = "The component no longer exists or the value is invalid.";
            if (Component == null || !Definition.TryNormalize(value, out object normalized)) return false;
            object fieldValue = Field.FieldType.IsEnum ? Enum.Parse(Field.FieldType, (string)normalized) : normalized;
            try
            {
                if (Component is IConfigurableFieldAdapter adapter)
                    return adapter.TrySetConfiguration(Field.Name, fieldValue, out reason);
                Field.SetValue(Component, fieldValue);
                reason = null;
                return true;
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException
                || exception is TargetInvocationException || exception is FieldAccessException)
            {
                reason = exception.Message;
                return false;
            }
        }

        internal static List<ConfigurableField> Discover(ConfigurableObject owner, List<string> unsupported)
        {
            List<ConfigurableField> result = new List<ConfigurableField>();
            foreach (MonoBehaviour component in owner.ConfigurationComponents())
            {
                string componentId = ComponentAddress(owner.transform, component);
                string group = Nicify(component.GetType().Name) + " [" + componentId.Split(':')[0] + "]";
                for (Type type = component.GetType(); type != null && type != typeof(MonoBehaviour)
                    && type != typeof(NetworkBehaviour); type = type.BaseType)
                {
                    FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public
                        | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                    Array.Sort(fields, (left, right) => left.MetadataToken.CompareTo(right.MetadataToken));
                    string section = group;
                    foreach (FieldInfo field in fields)
                    {
                        if (field.IsStatic || field.IsLiteral || field.IsInitOnly || field.IsNotSerialized
                            || field.IsDefined(typeof(HideInInspector), true)
                            || (!field.IsPublic && !field.IsDefined(typeof(SerializeField), true)
                                && !field.IsDefined(typeof(SerializeReference), true))) continue;
                        HeaderAttribute header = field.GetCustomAttribute<HeaderAttribute>();
                        string headerName = header?.header.Replace('/', ' ').Trim();
                        if (!string.IsNullOrWhiteSpace(headerName)) section = group + "/" + headerName;
                        string id = Hash128.Compute(componentId + ":" + type.FullName + ":" + field.Name).ToString();
                        SettingDefinition definition = CreateDefinition(component, field, id, section);
                        if (definition != null) result.Add(new ConfigurableField(component, field, definition));
                        else unsupported.Add(group + " / " + Nicify(field.Name) + " (" + field.FieldType.Name + ")");
                    }
                }
            }
            return result;
        }

        private static string ComponentAddress(Transform root, MonoBehaviour component)
        {
            List<string> path = new List<string>();
            for (Transform current = component.transform; current != root; current = current.parent)
                path.Add(current.GetSiblingIndex().ToString(System.Globalization.CultureInfo.InvariantCulture));
            path.Reverse();
            int index = Array.IndexOf(component.GetComponents<MonoBehaviour>(), component);
            return (path.Count == 0 ? "root" : string.Join(".", path)) + "." + index + ":" + component.GetType().FullName;
        }

        private static SettingDefinition CreateDefinition(MonoBehaviour component, FieldInfo field, string id, string group)
        {
            object value = field.GetValue(component);
            string label = field.GetCustomAttribute<InspectorNameAttribute>()?.displayName ?? Nicify(field.Name);
            string description = field.GetCustomAttribute<TooltipAttribute>()?.tooltip ?? "";
            RangeAttribute range = field.GetCustomAttribute<RangeAttribute>();
            MinAttribute minimum = field.GetCustomAttribute<MinAttribute>();
            string hint = range == null ? "number-input" : "";
            if (field.FieldType == typeof(bool))
                return new BoolSettingDefinition(id, (bool)value, label, group, description);
            if (field.FieldType == typeof(float))
            {
                float number = (float)value;
                if (float.IsNaN(number) || float.IsInfinity(number)) return null;
                float min = range?.min ?? minimum?.min ?? -float.MaxValue;
                float max = range?.max ?? float.MaxValue;
                return min <= max ? new FloatSettingDefinition(id, number, min, max, label, group, description,
                    editorHint: hint) : null;
            }
            if (field.FieldType == typeof(int))
            {
                int min = range != null ? Mathf.CeilToInt(range.min) : minimum != null ? Mathf.CeilToInt(minimum.min) : int.MinValue;
                int max = range != null ? Mathf.FloorToInt(range.max) : int.MaxValue;
                return min <= max ? new IntSettingDefinition(id, (int)value, min, max, label, group, description,
                    editorHint: hint) : null;
            }
            if (field.FieldType == typeof(string))
            {
                string text = (string)value ?? "";
                if (text.Length > 1024) return null;
                return new StringSettingDefinition(id, text, label, group, maxLength: 1024, description: description);
            }
            if (field.FieldType.IsEnum && !field.FieldType.IsDefined(typeof(FlagsAttribute), false)
                && Enum.IsDefined(field.FieldType, value))
                return new StringSettingDefinition(id, value.ToString(), label, group,
                    choices: Enum.GetNames(field.FieldType), description: description);
            return null;
        }

        private static string Nicify(string name) => Regex.Replace(name.TrimStart('_'), "([a-z0-9])([A-Z])", "$1 $2");
    }
}
