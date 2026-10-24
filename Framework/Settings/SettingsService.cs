using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnscriptedEngine.Settings
{
    [Serializable]
    public sealed class SettingsPreferences
    {
        public List<SettingValueRecord> values = new List<SettingValueRecord>();
    }

    [Serializable]
    public sealed class SettingValueRecord
    {
        public string id;
        public string value;
    }

    /// <summary>Main-thread state store. Scene registrations determine activation, not code references.</summary>
    public sealed class SettingsService
    {
        private sealed class Entry
        {
            public readonly SettingDefinition Definition;
            public int Owners;
            public object Override;
            public object ServerValue;
            public event Action<object> Changed;

            public Entry(SettingDefinition definition) => Definition = definition;
            public void Subscribe(Action<object> listener) => Changed += listener;
            public void Unsubscribe(Action<object> listener) => Changed -= listener;

            public void Notify(object value)
            {
                if (Changed == null) return;
                foreach (Action<object> listener in Changed.GetInvocationList())
                {
                    try { listener(value); }
                    catch (Exception exception) { Debug.LogException(exception); }
                }
            }
        }

        private sealed class RegistrationLease : IDisposable
        {
            private Action release;

            public RegistrationLease(Action release) => this.release = release;

            public void Dispose()
            {
                Action action = release;
                release = null;
                action?.Invoke();
            }
        }

        private readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> saved = new Dictionary<string, string>(StringComparer.Ordinal);

        public SettingsRole Role { get; private set; }
        public event Action StructureChanged;
        public event Action PreferencesChanged;
        public event Action<SettingDefinition, object> ValueChanged;
        public IReadOnlyList<SettingsGroup> ActiveGroups => SettingsGroup.BuildTree(GetActiveDefinitions());

        public SettingsService(SettingsRole role = SettingsRole.Standalone) => Role = role;

        public IReadOnlyList<SettingDefinition> GetActiveDefinitions()
        {
            return entries.Values.Where(entry => entry.Owners > 0)
                .Select(entry => entry.Definition).OrderBy(definition => definition.Id, StringComparer.Ordinal)
                .ToList().AsReadOnly();
        }

        public T Get<T>(Setting<T> definition) => (T)Get((SettingDefinition)definition);
        public object Get(SettingDefinition definition) => GetEffectiveValue(GetOrCreateEntry(definition));
        public bool IsActive(SettingDefinition definition) => GetOrCreateEntry(definition).Owners > 0;

        public bool CanWrite(SettingDefinition definition)
        {
            return IsActive(definition) &&
                (definition.Scope == SettingScope.LocalUser || Role != SettingsRole.Client);
        }

        public IDisposable Observe<T>(Setting<T> definition, Action<T> callback, bool emitCurrent = true)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            return Observe((SettingDefinition)definition, value => callback((T)value), emitCurrent);
        }

        public IDisposable Observe(SettingDefinition definition, Action<object> callback, bool emitCurrent = true)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            Entry entry = GetOrCreateEntry(definition);
            entry.Subscribe(callback);
            try
            {
                if (emitCurrent) callback(GetEffectiveValue(entry));
            }
            catch
            {
                entry.Unsubscribe(callback);
                throw;
            }
            return new RegistrationLease(() => entry.Unsubscribe(callback));
        }

        public bool TrySet<T>(Setting<T> definition, T value) => TrySet((SettingDefinition)definition, value);

        public bool TrySet(SettingDefinition definition, object value)
        {
            if (!CanWrite(definition) || !definition.TryNormalize(value, out object normalized)) return false;
            Entry entry = GetOrCreateEntry(definition);
            object previous = GetEffectiveValue(entry);
            entry.Override = normalized;
            SaveLocalOverride(definition, normalized);
            NotifyEffectiveValueChanged(entry, previous);
            return true;
        }

        public bool Reset(SettingDefinition definition)
        {
            if (!CanWrite(definition)) return false;
            Entry entry = GetOrCreateEntry(definition);
            object previous = GetEffectiveValue(entry);
            entry.Override = null;
            if (definition.Scope == SettingScope.LocalUser && saved.Remove(definition.Id))
                PublishNotification(PreferencesChanged);
            NotifyEffectiveValueChanged(entry, previous);
            return true;
        }

        public IDisposable Register(IEnumerable<SettingDefinition> definitions)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            SettingDefinition[] uniqueDefinitions = definitions.Distinct().ToArray();
            ValidateRegistration(uniqueDefinitions);
            Entry[] registeredEntries = uniqueDefinitions.Select(GetOrCreateEntry).ToArray();
            Dictionary<Entry, object> previous = CaptureEffectiveValues(registeredEntries);
            foreach (Entry entry in registeredEntries) entry.Owners++;
            NotifyEffectiveValueChanges(previous);
            PublishNotification(StructureChanged);
            return new RegistrationLease(() => UnregisterEntries(registeredEntries));
        }

        public SettingsPreferences ExportPreferences()
        {
            SettingsPreferences preferences = new SettingsPreferences();
            foreach (KeyValuePair<string, string> pair in saved.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                if (entries.TryGetValue(pair.Key, out Entry entry) && entry.Definition.Scope == SettingScope.ServerSession)
                    continue;
                preferences.values.Add(new SettingValueRecord { id = pair.Key, value = pair.Value });
            }
            return preferences;
        }

        public void LoadPreferences(SettingsPreferences preferences)
        {
            Dictionary<Entry, object> previous = CaptureEffectiveValues(entries.Values);
            saved.Clear();
            if (preferences?.values != null)
            {
                foreach (SettingValueRecord record in preferences.values)
                    if (record != null && !string.IsNullOrWhiteSpace(record.id) && record.value != null)
                        saved[record.id] = record.value;
            }
            foreach (Entry entry in previous.Keys)
                if (entry.Definition.Scope == SettingScope.LocalUser) RestoreSavedOverride(entry);
            NotifyEffectiveValueChanges(previous);
        }

        /// <summary>Transport/bootstrap operation. Changing role clears session overrides.</summary>
        public void SetRole(SettingsRole role)
        {
            if (role == Role) return;
            Dictionary<Entry, object> previous = CaptureEffectiveValues(entries.Values);
            Role = role;
            foreach (Entry entry in previous.Keys)
            {
                if (entry.Definition.Scope != SettingScope.ServerSession) continue;
                entry.Override = null;
                entry.ServerValue = null;
            }
            NotifyEffectiveValueChanges(previous);
            PublishNotification(StructureChanged);
        }

        /// <summary>Transport adapter hook. Authenticate the server sender before calling.</summary>
        public bool ApplyServerValue(SettingDefinition definition, object value)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (Role != SettingsRole.Client || definition.Scope != SettingScope.ServerSession ||
                !definition.TryNormalize(value, out object normalized)) return false;
            Entry entry = GetOrCreateEntry(definition);
            object previous = GetEffectiveValue(entry);
            entry.ServerValue = normalized;
            NotifyEffectiveValueChanged(entry, previous);
            return true;
        }

        private Entry GetOrCreateEntry(SettingDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (entries.TryGetValue(definition.Id, out Entry existing))
            {
                ValidateMatchingDefinition(existing.Definition, definition);
                return existing;
            }
            Entry entry = new Entry(definition);
            RestoreSavedOverride(entry);
            entries.Add(definition.Id, entry);
            return entry;
        }

        private object GetEffectiveValue(Entry entry)
        {
            if (entry.Definition.Scope == SettingScope.ServerSession && Role == SettingsRole.Client)
                return entry.ServerValue ?? entry.Definition.DefaultObject;
            return entry.Owners > 0 ? entry.Override ?? entry.Definition.DefaultObject : entry.Definition.DefaultObject;
        }

        private void RestoreSavedOverride(Entry entry)
        {
            entry.Override = null;
            if (entry.Definition.Scope == SettingScope.LocalUser &&
                saved.TryGetValue(entry.Definition.Id, out string json) &&
                entry.Definition.TryDeserializeValue(json, out object value))
                entry.Override = value;
        }

        private void SaveLocalOverride(SettingDefinition definition, object value)
        {
            if (definition.Scope != SettingScope.LocalUser) return;
            string json = definition.SerializeValue(value);
            bool changed = !saved.TryGetValue(definition.Id, out string previous) || previous != json;
            saved[definition.Id] = json;
            if (changed) PublishNotification(PreferencesChanged);
        }

        private void ValidateRegistration(IEnumerable<SettingDefinition> definitions)
        {
            Dictionary<string, SettingDefinition> identities = new Dictionary<string, SettingDefinition>(StringComparer.Ordinal);
            foreach (SettingDefinition definition in definitions)
            {
                if (definition == null) throw new ArgumentException("Cannot register a null setting.");
                if (identities.TryGetValue(definition.Id, out SettingDefinition existing))
                    ValidateMatchingDefinition(existing, definition);
                identities[definition.Id] = definition;
                if (entries.TryGetValue(definition.Id, out Entry entry))
                    ValidateMatchingDefinition(entry.Definition, definition);
            }
        }

        private static void ValidateMatchingDefinition(SettingDefinition existing, SettingDefinition incoming)
        {
            if (!ReferenceEquals(existing, incoming))
                throw new ArgumentException($"Different definitions share setting ID '{incoming.Id}'. Reuse a static readonly declaration.");
        }

        private void UnregisterEntries(Entry[] registeredEntries)
        {
            Dictionary<Entry, object> previous = CaptureEffectiveValues(registeredEntries);
            foreach (Entry entry in registeredEntries) entry.Owners--;
            NotifyEffectiveValueChanges(previous);
            PublishNotification(StructureChanged);
        }

        private Dictionary<Entry, object> CaptureEffectiveValues(IEnumerable<Entry> source)
        {
            return source.ToDictionary(entry => entry, GetEffectiveValue);
        }

        private void NotifyEffectiveValueChanges(Dictionary<Entry, object> previous)
        {
            foreach (KeyValuePair<Entry, object> pair in previous)
                NotifyEffectiveValueChanged(pair.Key, pair.Value);
        }

        private void NotifyEffectiveValueChanged(Entry entry, object previous)
        {
            object current = GetEffectiveValue(entry);
            if (Equals(previous, current)) return;
            entry.Notify(current);
            if (ValueChanged == null) return;
            foreach (Action<SettingDefinition, object> listener in ValueChanged.GetInvocationList())
            {
                try { listener(entry.Definition, current); }
                catch (Exception exception) { Debug.LogException(exception); }
            }
        }

        private static void PublishNotification(Action listeners)
        {
            if (listeners == null) return;
            foreach (Action listener in listeners.GetInvocationList())
            {
                try { listener(); }
                catch (Exception exception) { Debug.LogException(exception); }
            }
        }
    }
}
