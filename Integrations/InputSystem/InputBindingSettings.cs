using System;
using System.Collections.Generic;
using System.Linq;
using UnscriptedEngine.Settings;
using UnityEngine.InputSystem;

namespace UnscriptedEngine.Input
{
    /// <summary>One local override. An empty value follows the input owner's authored path.</summary>
    public sealed class InputBindingSetting : Setting<string>
    {
        internal const string Hint = "input-binding";
        public string DefaultPath { get; }
        public string DeviceLayout { get; }
        public string ControlLayout { get; }

        internal InputBindingSetting(string id, string label, string group, string path, string controlLayout, int order)
            : base(id, "", label, group, "", SettingScope.LocalUser, Hint, false, order)
        {
            DefaultPath = path;
            DeviceLayout = InputControlPath.TryGetDeviceLayout(path);
            ControlLayout = controlLayout;
        }

        public override string DefaultValue => "";

        protected override bool IsValid(string value)
        {
            if (value.Length == 0) return true;
            if (value.Length > 256 || value.Contains("*")) return false;
            string device = InputControlPath.TryGetDeviceLayout(value);
            string control = InputControlPath.TryGetControlLayout(value);
            return MatchesLayout(device, DeviceLayout) && MatchesLayout(control, ControlLayout);
        }

        private static bool MatchesLayout(string candidate, string expected) =>
            !string.IsNullOrEmpty(candidate) && (string.Equals(candidate, expected, StringComparison.OrdinalIgnoreCase)
                || InputSystem.IsFirstLayoutBasedOnSecond(candidate, expected));

        public string DisplayBinding(string value) =>
            InputControlPath.ToHumanReadableString(string.IsNullOrEmpty(value) ? DefaultPath : value,
                InputControlPath.HumanReadableStringOptions.OmitDevice);
    }

    /// <summary>
    /// Keep one catalog per input owner. Authored actions/bindings use asset GUIDs and generated
    /// maps can supply stable keys. Separate live copies of a map share definitions and preferences.
    /// </summary>
    public sealed class InputBindingSettings
    {
        private readonly string id;
        private readonly string group;
        private readonly Dictionary<string, InputBindingSetting> definitions = new();

        public InputBindingSettings(string id, string group)
        {
            this.id = id;
            this.group = group;
        }

        public IDisposable Bind(SettingsService service, InputActionMap map, bool generated = false,
            Func<InputAction, string> actionLabel = null)
        {
            var entries = new List<(InputAction action, int index, InputBindingSetting definition)>();
            int order = 0;
            foreach (InputAction action in map.actions)
            {
                string compositeKey = null;
                for (int index = 0; index < action.bindings.Count; index++)
                {
                    InputBinding binding = action.bindings[index];
                    if (binding.isComposite)
                    {
                        compositeKey = binding.name;
                        continue;
                    }
                    if (string.IsNullOrEmpty(binding.path)) continue;
                    string device = InputControlPath.TryGetDeviceLayout(binding.path);
                    if (string.IsNullOrEmpty(device) || device == "*") continue;

                    string layout = binding.isPartOfComposite || action.type == InputActionType.Button
                        ? "Button" : action.expectedControlType;
                    if (string.IsNullOrEmpty(layout)) layout = InputControlPath.TryGetControlLayout(binding.path);
                    if (binding.path.EndsWith("/delta", StringComparison.OrdinalIgnoreCase)) layout = "Delta";
                    // Position and pose are continuously supplied by the pointer / tracked device.
                    if (string.IsNullOrEmpty(layout) || layout is "Vector3" or "Quaternion"
                        || binding.path.EndsWith("/position", StringComparison.OrdinalIgnoreCase)) continue;

                    string actionKey = generated ? action.name : action.id.ToString("N");
                    // Explicit names identify bindings added at runtime, including per-device usage bindings.
                    string bindingKey = generated || (!binding.isPartOfComposite && binding.name?.StartsWith("device-", StringComparison.Ordinal) == true)
                        ? binding.name : binding.id.ToString("N");
                    if (generated && binding.isPartOfComposite) bindingKey = $"{compositeKey}.{binding.name}";
                    if (string.IsNullOrEmpty(bindingKey))
                        throw new InvalidOperationException($"Generated input '{action.name}' needs a stable binding name.");
                    string key = $"{id}.{actionKey}.{bindingKey}";
                    if (!definitions.TryGetValue(key, out InputBindingSetting definition))
                    {
                        string label = InputControlPath.ToHumanReadableString(binding.path,
                            InputControlPath.HumanReadableStringOptions.OmitDevice);
                        string displayName = actionLabel?.Invoke(action) ?? Nicify(action.name);
                        string part = binding.isPartOfComposite ? $" {binding.name}" : "";
                        definition = new InputBindingSetting(key, $"{displayName}{part} / {label}",
                            $"{group}/{device}", binding.path, layout, order);
                        definitions.Add(key, definition);
                    }
                    order++;
                    entries.Add((action, index, definition));
                }
            }

            var lifetime = new BindingLifetime();
            try
            {
                lifetime.Registration = service.Register(entries.Select(entry => entry.definition));
                foreach (var entry in entries)
                {
                    lifetime.Subscriptions.Add(service.Observe(entry.definition, path =>
                    {
                        string overridePath = path.Length == 0 ? null : path;
                        if (entry.action.bindings[entry.index].overridePath == overridePath) return;
                        if (overridePath == null) entry.action.RemoveBindingOverride(entry.index);
                        else entry.action.ApplyBindingOverride(entry.index, overridePath);
                    }));
                }
                return lifetime;
            }
            catch
            {
                lifetime.Dispose();
                throw;
            }
        }

        private static string Nicify(string name)
        {
            var label = new System.Text.StringBuilder(name.Length + 6);
            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && (char.IsUpper(name[i]) || char.IsDigit(name[i]))
                    && char.IsLower(name[i - 1])) label.Append(' ');
                label.Append(name[i]);
            }
            return label.ToString();
        }

        private sealed class BindingLifetime : IDisposable
        {
            public IDisposable Registration;
            public readonly List<IDisposable> Subscriptions = new();

            public void Dispose()
            {
                foreach (IDisposable subscription in Subscriptions) subscription.Dispose();
                Subscriptions.Clear();
                Registration?.Dispose();
                Registration = null;
            }
        }
    }
}

