using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace UnscriptedEngine.Settings
{
    /// <summary>Discovers only explicitly selected catalog types. Does not activate settings.</summary>
    public static class SettingsCatalog
    {
        private static readonly Dictionary<Type, IReadOnlyList<SettingDefinition>> Cache =
            new Dictionary<Type, IReadOnlyList<SettingDefinition>>();

        public static IReadOnlyList<SettingDefinition> GetDefinitions(Type catalogType)
        {
            if (catalogType == null) throw new ArgumentNullException(nameof(catalogType));
            if (Cache.TryGetValue(catalogType, out IReadOnlyList<SettingDefinition> cached)) return cached;
            List<SettingDefinition> definitions = new List<SettingDefinition>();
            CollectDefinitions(catalogType, definitions);
            ValidateIdentities(definitions);
            IReadOnlyList<SettingDefinition> result = definitions.AsReadOnly();
            Cache.Add(catalogType, result);
            return result;
        }

        private static void CollectDefinitions(Type catalogType, List<SettingDefinition> definitions)
        {
            FieldInfo[] fields = catalogType.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
            foreach (FieldInfo field in fields)
            {
                if (!typeof(SettingDefinition).IsAssignableFrom(field.FieldType)) continue;
                if (!field.IsInitOnly) throw new ArgumentException($"Catalog field '{field.Name}' must be static readonly.");
                SettingDefinition definition = field.GetValue(null) as SettingDefinition;
                if (definition == null) throw new ArgumentException($"Catalog field '{field.Name}' is null.");
                definitions.Add(definition);
            }
            foreach (Type nestedType in catalogType.GetNestedTypes(BindingFlags.Public))
                CollectDefinitions(nestedType, definitions);
        }

        private static void ValidateIdentities(IEnumerable<SettingDefinition> definitions)
        {
            Dictionary<string, SettingDefinition> identities = new Dictionary<string, SettingDefinition>(StringComparer.Ordinal);
            foreach (SettingDefinition definition in definitions)
            {
                if (identities.TryGetValue(definition.Id, out SettingDefinition existing) && !ReferenceEquals(existing, definition))
                    throw new ArgumentException($"Different definitions share setting ID '{definition.Id}'.");
                identities[definition.Id] = definition;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() => Cache.Clear();
    }
}
