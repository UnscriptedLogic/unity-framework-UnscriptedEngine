using System;
using System.Collections.Generic;
using UnscriptedEngine.Settings;
using UnityEngine.InputSystem;

namespace UnscriptedEngine.Input
{
    /// <summary>Share a local shortcut map between a settings screen and the tool that polls it.</summary>
    public sealed class LocalInputShortcuts
    {
        private readonly InputActionMap map;
        private readonly InputBindingSettings settings;
        private readonly List<Action> pendingBindings = new();
        private IDisposable bindingLease;
        private int users;

        public LocalInputShortcuts(string key, string label)
        {
            map = new InputActionMap(label);
            settings = new InputBindingSettings($"controls.{key}", $"Bindings/{label}");
        }

        public InputAction AddButton(string actionKey, string path, string bindingKey)
        {
            InputAction action = map.FindAction(actionKey, false) ?? map.AddAction(actionKey, InputActionType.Button);
            pendingBindings.Add(() => action.AddBinding(path).WithName(bindingKey));
            return action;
        }

        public InputAction AddChord(string actionKey, string key, string modifier, string secondModifier = null)
        {
            InputAction action = map.AddAction(actionKey, InputActionType.Button);
            pendingBindings.Add(() =>
            {
                if (secondModifier == null)
                    action.AddCompositeBinding("OneModifier(modifiersOrder=1)").With("modifier", modifier).With("binding", key);
                else
                    action.AddCompositeBinding("TwoModifiers(modifiersOrder=1)").With("modifier1", modifier)
                        .With("modifier2", secondModifier).With("binding", key);
                action.ChangeBinding(0).WithName("chord");
            });
            return action;
        }

        public IDisposable Acquire()
        {
            if (users == 0)
            {
                // Static shortcut declarations can run before the Input System initializes.
                foreach (Action addBinding in pendingBindings) addBinding();
                pendingBindings.Clear();
                bindingLease = settings.Bind(SettingsRuntime.Service, map, generated: true);
                try { map.Enable(); }
                catch { bindingLease.Dispose(); bindingLease = null; throw; }
            }
            users++;
            return new Lease(this);
        }

        private sealed class Lease : IDisposable
        {
            private LocalInputShortcuts owner;
            public Lease(LocalInputShortcuts owner) => this.owner = owner;

            public void Dispose()
            {
                if (owner == null) return;
                if (--owner.users == 0)
                {
                    owner.map.Disable();
                    owner.bindingLease.Dispose();
                    owner.bindingLease = null;
                }
                owner = null;
            }
        }
    }
}
