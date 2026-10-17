using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnscriptedEngine.Settings
{
    [Serializable]
    public sealed class SettingsPreferences
    {
        public List<SettingValueRecord> values = new();
    }

    [Serializable]
    public sealed class SettingValueRecord
    {
        public string id;
        public string value;
    }

    /// <summary>Main-thread service. Definitions remain immutable while registered.</summary>
    public sealed class SettingsService
    {
        private sealed class Entry
        {
            public SettingDefinition Definition;
            public int Owners;
            public object Override;
            public object ServerValue;
            public event Action<object> Changed;
            public void Add(Action<object> listener) => Changed += listener;
            public void Remove(Action<object> listener) => Changed -= listener;
            public void Notify(object value)
            {
                if (Changed == null) return;
                foreach (Action<object> listener in Changed.GetInvocationList())
                    try { listener(value); } catch (Exception e) { Debug.LogException(e); }
            }
        }

        private sealed class Lease : IDisposable
        {
            private Action release;
            public Lease(Action release) => this.release = release;
            public void Dispose() { var action = release; release = null; action?.Invoke(); }
        }

        private readonly Dictionary<string, Entry> entries = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> saved = new(StringComparer.Ordinal);
        private readonly Dictionary<SettingsGroup, int> roots = new();
        public SettingsRole Role { get; private set; }
        public event Action StructureChanged;
        public event Action PreferencesChanged;
        public event Action<SettingDefinition, object> ValueChanged;
        public IEnumerable<SettingsGroup> ActiveGroups => roots.Keys.OrderBy(g => g.Order).ThenBy(g => g.DisplayName);

        public SettingsService(SettingsRole role = SettingsRole.Standalone) => Role = role;

        private Entry Ensure(SettingDefinition definition)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
                throw new ArgumentException("Settings require a definition with a stable ID.");
            if (entries.TryGetValue(definition.Id, out var entry))
            {
                if (entry.Definition != definition)
                    throw new ArgumentException($"Different definitions share setting ID '{definition.Id}'.");
                return entry;
            }
            entry = new Entry { Definition = definition };
            if (definition.Scope == SettingScope.LocalUser && saved.TryGetValue(definition.Id, out var json) &&
                definition.TryDeserializeValue(json, out var value)) entry.Override = value;
            entries.Add(definition.Id, entry);
            return entry;
        }

        private object Effective(Entry entry)
        {
            if (entry.Definition.Scope == SettingScope.ServerSession && Role == SettingsRole.Client)
                return entry.ServerValue ?? entry.Definition.DefaultObject;
            return entry.Owners > 0 ? entry.Override ?? entry.Definition.DefaultObject : entry.Definition.DefaultObject;
        }

        public T Get<T>(SettingDefinition<T> definition) => (T)Get((SettingDefinition)definition);
        public object Get(SettingDefinition definition) => Effective(Ensure(definition));
        public bool IsActive(SettingDefinition definition) => Ensure(definition).Owners > 0;
        public bool CanWrite(SettingDefinition definition) => IsActive(definition) &&
            (definition.Scope == SettingScope.LocalUser || Role != SettingsRole.Client);

        public IDisposable Observe<T>(SettingDefinition<T> definition, Action<T> callback, bool emitCurrent = true)
            => Observe((SettingDefinition)definition, value => callback((T)value), emitCurrent);

        public IDisposable Observe(SettingDefinition definition, Action<object> callback, bool emitCurrent = true)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            var entry = Ensure(definition);
            entry.Add(callback);
            try { if (emitCurrent) callback(Effective(entry)); }
            catch { entry.Remove(callback); throw; }
            return new Lease(() => entry.Remove(callback));
        }

        public bool TrySet<T>(SettingDefinition<T> definition, T value) => TrySet((SettingDefinition)definition, value);
        public bool TrySet(SettingDefinition definition, object value)
        {
            if (!CanWrite(definition) || !definition.TryNormalize(value, out var normalized)) return false;
            var entry = Ensure(definition);
            var before = Effective(entry);
            entry.Override = normalized;
            if (definition.Scope == SettingScope.LocalUser)
            {
                string json = definition.SerializeValue(normalized);
                bool changed = !saved.TryGetValue(definition.Id, out var old) || old != json;
                saved[definition.Id] = json;
                if (changed) Publish(PreferencesChanged);
            }
            Notify(entry, before);
            return true;
        }

        public bool Reset(SettingDefinition definition)
        {
            if (!CanWrite(definition)) return false;
            var entry = Ensure(definition);
            var before = Effective(entry);
            entry.Override = null;
            if (definition.Scope == SettingScope.LocalUser && saved.Remove(definition.Id)) Publish(PreferencesChanged);
            Notify(entry, before);
            return true;
        }

        public IDisposable Register(SettingsGroup root)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            var definitions = root.Flatten();
            // Validate the complete registration before activating any values.
            var ids = new Dictionary<string, SettingDefinition>(StringComparer.Ordinal);
            foreach (var definition in definitions)
            {
                if (string.IsNullOrWhiteSpace(definition.Id)) throw new ArgumentException("A setting is missing its ID.");
                if (ids.TryGetValue(definition.Id, out var other) && other != definition)
                    throw new ArgumentException($"Duplicate setting ID '{definition.Id}'.");
                ids[definition.Id] = definition;
                if (entries.TryGetValue(definition.Id, out var existing) && existing.Definition != definition)
                    throw new ArgumentException($"Duplicate setting ID '{definition.Id}'.");
            }
            var registered = definitions.Select(Ensure).ToArray();
            var before = registered.Select(Effective).ToArray();
            foreach (var entry in registered) entry.Owners++;
            roots[root] = roots.TryGetValue(root, out var count) ? count + 1 : 1;
            for (int i = 0; i < registered.Length; i++) Notify(registered[i], before[i]);
            Publish(StructureChanged);
            return new Lease(() =>
            {
                var previous = registered.Select(Effective).ToArray();
                foreach (var entry in registered) entry.Owners--;
                if (--roots[root] == 0) roots.Remove(root);
                for (int i = 0; i < registered.Length; i++) Notify(registered[i], previous[i]);
                Publish(StructureChanged);
            });
        }

        private void Notify(Entry entry, object before)
        {
            var after = Effective(entry);
            if (Equals(before, after)) return;
            entry.Notify(after);
            if (ValueChanged != null)
                foreach (Action<SettingDefinition, object> listener in ValueChanged.GetInvocationList())
                    try { listener(entry.Definition, after); } catch (Exception e) { Debug.LogException(e); }
        }

        private static void Publish(Action listeners)
        {
            if (listeners == null) return;
            foreach (Action listener in listeners.GetInvocationList())
                try { listener(); } catch (Exception e) { Debug.LogException(e); }
        }

        public SettingsPreferences ExportPreferences() => new()
        {
            values = saved.Where(pair => !entries.TryGetValue(pair.Key, out var entry) || entry.Definition.Scope == SettingScope.LocalUser)
                .OrderBy(pair => pair.Key).Select(pair => new SettingValueRecord { id = pair.Key, value = pair.Value }).ToList()
        };

        public void LoadPreferences(SettingsPreferences preferences)
        {
            var before = entries.Values.ToDictionary(e => e, Effective);
            saved.Clear();
            foreach (var record in preferences?.values ?? new List<SettingValueRecord>())
                if (record != null && !string.IsNullOrWhiteSpace(record.id) && record.value != null)
                    saved[record.id] = record.value;
            foreach (var entry in before.Keys)
            {
                if (entry.Definition.Scope != SettingScope.LocalUser) continue;
                entry.Override = null;
                if (saved.TryGetValue(entry.Definition.Id, out var json) && entry.Definition.TryDeserializeValue(json, out var value))
                    entry.Override = value;
                Notify(entry, before[entry]);
            }
        }

        /// <summary>Transport/bootstrap operation, never a UI permission toggle. Clears session overrides.</summary>
        public void SetRole(SettingsRole role)
        {
            if (role == Role) return;
            var before = entries.Values.ToDictionary(e => e, Effective);
            Role = role;
            foreach (var entry in entries.Values.ToArray())
            {
                if (entry.Definition.Scope == SettingScope.ServerSession) { entry.Override = null; entry.ServerValue = null; }
                Notify(entry, before[entry]);
            }
            Publish(StructureChanged);
        }

        /// <summary>Call only from a transport handler after authenticating the server sender.</summary>
        public bool ApplyServerValue(SettingDefinition definition, object value)
        {
            if (Role != SettingsRole.Client || definition.Scope != SettingScope.ServerSession ||
                !definition.TryNormalize(value, out var normalized)) return false;
            var entry = Ensure(definition);
            var before = Effective(entry);
            entry.ServerValue = normalized;
            Notify(entry, before);
            return true;
        }
    }
}
