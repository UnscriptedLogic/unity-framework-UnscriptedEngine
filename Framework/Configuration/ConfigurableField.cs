using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Framework.GameplayTag;
using UnityEngine;
using Unity.Netcode;
using UnscriptedEngine.Settings;

namespace UnscriptedEngine.Configuration
{
    /// <summary>Implement when a serialized field needs validation, a setter, or rebuilding at runtime.
    /// The adapter owns assignment and must leave state unchanged when rejecting a write.</summary>
    public interface IConfigurableFieldAdapter
    {
        // Nested members use paths such as "filter.layers" or "valueChanges[0].amount".
        bool TrySetConfiguration(string fieldName, object value, out string reason);
    }

    public sealed class ConfigurableField
    {
        public MonoBehaviour Component { get; }
        public FieldInfo Field { get; }
        public string Path { get; }
        public SettingDefinition Definition { get; }
        private readonly Type valueType;
        private readonly Func<object> readValue;
        private readonly Action<object> writeValue;

        private ConfigurableField(MonoBehaviour component, FieldInfo field, string path, Type valueType,
            SettingDefinition definition, Func<object> readValue, Action<object> writeValue)
        {
            Component = component; Field = field; Path = path; this.valueType = valueType;
            Definition = definition; this.readValue = readValue; this.writeValue = writeValue;
        }

        public object Read()
        {
            if (Component == null) return Definition.DefaultObject;
            try
            {
                object value = readValue();
                if (valueType == typeof(LayerMask)) return ((LayerMask)value).value;
                if (IsTagValue(valueType)) return ReadTags(value);
                return valueType.IsEnum ? value.ToString() : value ?? "";
            }
            catch (InvalidOperationException) { return Definition.DefaultObject; }
        }

        internal bool TryWrite(object value, out string reason) => TryWrite(value, true, out reason);
        internal bool TryWriteSerialized(object value, out string reason) => TryWrite(value, false, out reason);

        private bool TryWrite(object value, bool useAdapter, out string reason)
        {
            reason = "The component no longer exists or the value is invalid.";
            if (Component == null || !Definition.TryNormalize(value, out object normalized)) return false;
            object fieldValue = valueType.IsEnum ? Enum.Parse(valueType, (string)normalized)
                : valueType == typeof(LayerMask) ? (object)(LayerMask)(int)normalized : normalized;
            if (IsTagValue(valueType) && !TryResolveTags((string)normalized, valueType, out fieldValue, out reason)) return false;
            try
            {
                if (useAdapter && Component is IConfigurableFieldAdapter adapter)
                    return adapter.TrySetConfiguration(Path, fieldValue, out reason);
                writeValue(fieldValue);
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
                DiscoverMembers(component, component.GetType(), () => component, null, "", componentId, group, 0, result, unsupported);
            }
            return result;
        }

        private static void DiscoverMembers(MonoBehaviour component, Type containerType, Func<object> readParent,
            Action<object> writeParent, string prefix, string componentId, string group, int depth,
            List<ConfigurableField> result, List<string> unsupported)
        {
            for (Type type = containerType; type != null && type != typeof(object) && type != typeof(MonoBehaviour)
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
                    string path = prefix + field.Name;
                    object ReadParent() => readParent() ?? throw new InvalidOperationException("Configuration container no longer exists: " + path);
                    Func<object> read = () => field.GetValue(ReadParent());
                    Action<object> write = value =>
                    {
                        object parent = ReadParent();
                        field.SetValue(parent, value);
                        writeParent?.Invoke(parent);
                    };
                    DiscoverValue(component, field, field.FieldType, read, write, path, componentId,
                        section, depth, result, unsupported);
                }
            }
        }

        private static void DiscoverValue(MonoBehaviour component, FieldInfo field, Type valueType,
            Func<object> read, Action<object> write, string path, string componentId, string group, int depth,
            List<ConfigurableField> result, List<string> unsupported)
        {
            object value = read();
            string id = Hash128.Compute(componentId + ":" + field.DeclaringType.FullName + ":" + path).ToString();
            SettingDefinition definition = CreateDefinition(value, valueType, field, id, group);
            if (definition != null)
            {
                result.Add(new ConfigurableField(component, field, path, valueType, definition, read, write));
                return;
            }
            if (depth < 8 && value is IList list && (valueType.IsArray
                || valueType.IsGenericType && valueType.GetGenericTypeDefinition() == typeof(List<>)) && list.Count > 0 && list.Count <= 128)
            {
                Type elementType = valueType.IsArray ? valueType.GetElementType() : valueType.GetGenericArguments()[0];
                for (int index = 0; index < list.Count; index++)
                {
                    int elementIndex = index;
                    IList ReadList()
                    {
                        if (!(read() is IList current) || elementIndex >= current.Count)
                            throw new InvalidOperationException("Configuration list entry no longer exists: " + path);
                        return current;
                    }
                    DiscoverValue(component, field, elementType, () => ReadList()[elementIndex],
                        item => ReadList()[elementIndex] = item, path + "[" + index + "]", componentId,
                        group + "/" + Nicify(field.Name) + " " + index, depth + 1, result, unsupported);
                }
                return;
            }
            if (depth < 8 && value != null && valueType.IsSerializable && !valueType.IsPrimitive && !valueType.IsEnum
                && !valueType.IsValueType && valueType != typeof(string) && !(value is UnityEngine.Object)
                && !(value is UnityEngine.Events.UnityEventBase) && !(value is IList)
                && valueType.Assembly != typeof(string).Assembly && valueType.Assembly != typeof(Vector3).Assembly)
            {
                DiscoverMembers(component, valueType, read, write, path + ".", componentId,
                    group + "/" + Nicify(field.Name), depth + 1, result, unsupported);
                return;
            }
            unsupported.Add(group + " / " + path + " (" + valueType.Name + ")");
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

        private static SettingDefinition CreateDefinition(object value, Type valueType, FieldInfo field, string id, string group)
        {
            string label = field.GetCustomAttribute<InspectorNameAttribute>()?.displayName ?? Nicify(field.Name);
            string description = field.GetCustomAttribute<TooltipAttribute>()?.tooltip ?? "";
            RangeAttribute range = field.GetCustomAttribute<RangeAttribute>();
            MinAttribute minimum = field.GetCustomAttribute<MinAttribute>();
            string hint = range == null ? "number-input" : "";
            if (IsTagValue(valueType))
                return ReadTags(value).Length > 1024 ? null : new StringSettingDefinition(id, ReadTags(value), label, group, maxLength: 1024,
                    description: description + (valueType == typeof(GameplayTag)
                        ? " Gameplay tag path; empty clears the reference."
                        : " Comma-separated gameplay tag paths; empty clears the condition."));
            if (valueType == typeof(LayerMask))
                return new IntSettingDefinition(id, ((LayerMask)value).value, int.MinValue, int.MaxValue,
                    label, group, description + " Layer bitmask (-1 includes every layer).", editorHint: "number-input");
            if (valueType == typeof(bool))
                return new BoolSettingDefinition(id, (bool)value, label, group, description);
            if (valueType == typeof(float))
            {
                float number = (float)value;
                if (float.IsNaN(number) || float.IsInfinity(number)) return null;
                float min = range?.min ?? minimum?.min ?? -float.MaxValue;
                float max = range?.max ?? float.MaxValue;
                return min <= max ? new FloatSettingDefinition(id, number, min, max, label, group, description,
                    editorHint: hint) : null;
            }
            if (valueType == typeof(int))
            {
                int min = range != null ? Mathf.CeilToInt(range.min) : minimum != null ? Mathf.CeilToInt(minimum.min) : int.MinValue;
                int max = range != null ? Mathf.FloorToInt(range.max) : int.MaxValue;
                return min <= max ? new IntSettingDefinition(id, (int)value, min, max, label, group, description,
                    editorHint: hint) : null;
            }
            if (valueType == typeof(string))
            {
                string text = (string)value ?? "";
                if (text.Length > 1024) return null;
                return new StringSettingDefinition(id, text, label, group, maxLength: 1024, description: description);
            }
            if (valueType.IsEnum && !valueType.IsDefined(typeof(FlagsAttribute), false)
                && Enum.IsDefined(valueType, value))
                return new StringSettingDefinition(id, value.ToString(), label, group,
                    choices: Enum.GetNames(valueType), description: description);
            // These definitions validate every component, including authored defaults.
            // Invalid defaults are reported as unsupported rather than breaking discovery.
            try
            {
                if (valueType == typeof(Vector2))
                    return new Vector2SettingDefinition(id, (Vector2)value, label, group, description);
                if (valueType == typeof(Vector3))
                    return new Vector3SettingDefinition(id, (Vector3)value, label, group, description);
                if (valueType == typeof(Vector4))
                    return new Vector4SettingDefinition(id, (Vector4)value, label, group, description);
                if (valueType == typeof(Quaternion))
                    return new QuaternionSettingDefinition(id, (Quaternion)value, label, group, description);
                if (valueType == typeof(Color))
                    return new ColorSettingDefinition(id, (Color)value, label, group, description);
            }
            catch (ArgumentException) { return null; }
            return null;
        }

        private static string Nicify(string name) => Regex.Replace(name.TrimStart('_'), "([a-z0-9])([A-Z])", "$1 $2");

        private static bool IsTagValue(Type type) => type == typeof(GameplayTag) || type == typeof(GameplayTag[]);

        private static string ReadTags(object value)
        {
            if (value is GameplayTag tag) return tag.Path ?? "";
            if (!(value is GameplayTag[] tags)) return "";
            List<string> paths = new List<string>();
            foreach (GameplayTag item in tags) if (item != null) paths.Add(item.Path);
            return string.Join(", ", paths);
        }

        private static bool TryResolveTags(string text, Type type, out object value, out string reason)
        {
            value = null;
            reason = null;
            string[] paths = text.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (type == typeof(GameplayTag) && paths.Length > 1)
            {
                reason = "Enter one gameplay tag path.";
                return false;
            }
            List<GameplayTag> resolved = new List<GameplayTag>();
            GameplayTag[] available = Resources.FindObjectsOfTypeAll<GameplayTag>();
            foreach (string entry in paths)
            {
                string path = GameplayTagPath.Normalize(entry.Trim());
                if (string.IsNullOrEmpty(path)) continue;
                GameplayTag match = null;
                foreach (GameplayTag candidate in available)
                {
                    if (!candidate.IsValid || !string.Equals(candidate.Path, path, StringComparison.Ordinal)) continue;
                    if (match != null && match != candidate)
                    {
                        reason = "Ambiguous gameplay tag path: " + path;
                        return false;
                    }
                    match = candidate;
                }
#if UNITY_EDITOR
                if (match == null)
                    foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:GameplayTag"))
                    {
                        GameplayTag candidate = UnityEditor.AssetDatabase.LoadAssetAtPath<GameplayTag>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                        if (candidate == null || !candidate.IsValid || !string.Equals(candidate.Path, path, StringComparison.Ordinal)) continue;
                        if (match != null && match != candidate)
                        {
                            reason = "Ambiguous gameplay tag path: " + path;
                            return false;
                        }
                        match = candidate;
                    }
#endif
                if (match == null)
                {
                    reason = "Unknown or unloaded gameplay tag: " + path;
                    return false;
                }
                resolved.Add(match);
            }
            value = type == typeof(GameplayTag) ? (object)(resolved.Count == 0 ? null : resolved[0]) : resolved.ToArray();
            return true;
        }
    }
}
