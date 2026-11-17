using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnscriptedEngine.Settings;

namespace UnscriptedEngine.Configuration
{
    /// <summary>One inspector's baseline and undo history. Never registers with global preferences.</summary>
    public sealed class ObjectConfigurationSession : ISettingsValueSource, IDisposable
    {
        private readonly Transform observer;
        private readonly float range;
        private readonly Func<bool> authorized;
        private readonly Dictionary<string, ConfigurableField> fields;
        private readonly Dictionary<string, object> baseline;
        private readonly Dictionary<string, object> observed = new Dictionary<string, object>();
        private readonly Dictionary<string, Action<object>> listeners = new Dictionary<string, Action<object>>();
        private readonly List<KeyValuePair<SettingDefinition, object>> undo = new List<KeyValuePair<SettingDefinition, object>>();
        private bool disposed;
        public ConfigurableObject Target { get; }
        public IReadOnlyList<SettingsGroup> ActiveGroups { get; }
        public event Action StructureChanged { add { } remove { } }
        public bool CanUndo => undo.Count > 0;
        public string LastError { get; private set; }

        public ObjectConfigurationSession(ConfigurableObject target, Transform observer, float range, Func<bool> authorized = null)
        {
            Target = target != null ? target : throw new ArgumentNullException(nameof(target));
            this.observer = observer;
            this.range = range;
            this.authorized = authorized;
            fields = target.Fields.ToDictionary(field => field.Definition.Id, StringComparer.Ordinal);
            baseline = fields.ToDictionary(pair => pair.Key, pair => pair.Value.Read(), StringComparer.Ordinal);
            List<SettingDefinition> definitions = fields.Values.Select(field => field.Definition).ToList();
            for (int index = 0; index < target.UnsupportedFields.Count; index++)
                definitions.Add(new StringSettingDefinition("unsupported." + index, "Unsupported", target.UnsupportedFields[index],
                    "Unsupported fields", description: "Requires a runtime control or adapter. Shared assets and object references are not edited."));
            ActiveGroups = SettingsGroup.BuildTree(definitions);
            target.ValuesChanged += Refresh;
        }

        public object Get(SettingDefinition definition) => fields.TryGetValue(definition.Id, out ConfigurableField field)
            ? field.Read() : definition.DefaultObject;

        public bool CanWrite(SettingDefinition definition) => !disposed && Target != null && Target.CanEdit
            && Target.InRange(observer, range) && (authorized == null || authorized())
            && fields.ContainsKey(definition.Id) && fields[definition.Id].Component != null;

        public bool TrySet(SettingDefinition definition, object value)
        {
            object previous = Get(definition);
            if (!Write(definition, value)) return false;
            if (!Equals(previous, Get(definition)))
            {
                if (undo.Count == 128) undo.RemoveAt(0);
                undo.Add(new KeyValuePair<SettingDefinition, object>(definition, previous));
            }
            return true;
        }

        private bool Write(SettingDefinition definition, object value)
        {
            LastError = "The target is unavailable, out of range, or controlled by the host.";
            if (!CanWrite(definition)) return false;
            bool accepted = Target.TrySet(definition.Id, value, observer, range);
            LastError = Target.LastError;
            Refresh();
            return accepted;
        }

        public bool Reset(SettingDefinition definition) => baseline.TryGetValue(definition.Id, out object value)
            && TrySet(definition, value);

        public bool Undo()
        {
            if (undo.Count == 0) return false;
            KeyValuePair<SettingDefinition, object> edit = undo[undo.Count - 1];
            if (!Write(edit.Key, edit.Value)) return false;
            undo.RemoveAt(undo.Count - 1);
            return true;
        }

        public IDisposable Observe(SettingDefinition definition, Action<object> callback, bool emitCurrent = true)
        {
            if (disposed) throw new ObjectDisposedException(nameof(ObjectConfigurationSession));
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            listeners.TryGetValue(definition.Id, out Action<object> existing);
            listeners[definition.Id] = existing + callback;
            observed[definition.Id] = Get(definition);
            if (emitCurrent) callback(observed[definition.Id]);
            return new Subscription(() =>
            {
                if (listeners.TryGetValue(definition.Id, out Action<object> current)) listeners[definition.Id] = current - callback;
            });
        }

        /// <summary>Call at a modest rate to reflect changes made by gameplay outside this inspector.</summary>
        public void Refresh()
        {
            if (disposed) return;
            foreach (ConfigurableField field in fields.Values)
            {
                string id = field.Definition.Id;
                object value = field.Read();
                if (observed.TryGetValue(id, out object previous) && Equals(previous, value)) continue;
                observed[id] = value;
                if (listeners.TryGetValue(id, out Action<object> callback)) callback?.Invoke(value);
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (Target != null) Target.ValuesChanged -= Refresh;
            listeners.Clear();
            undo.Clear();
        }

        private sealed class Subscription : IDisposable
        {
            private Action release;
            public Subscription(Action release) => this.release = release;
            public void Dispose() { Action action = release; release = null; action?.Invoke(); }
        }
    }
}
