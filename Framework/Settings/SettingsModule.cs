using System;
using UnityEngine;

namespace UnscriptedEngine.Settings
{
    public sealed class SettingsModule : MonoBehaviour
    {
        [SerializeField, Tooltip("Group tree activated while this component is enabled during play. Removing the last module hides its settings and restores local defaults while retaining saved overrides. Keep this object outside menus that are hidden when closed.")]
        private SettingsGroup group;
        private IDisposable registration;
        public SettingsGroup Group => group;
        public void SetGroup(SettingsGroup value)
        {
            registration?.Dispose(); registration = null;
            group = value;
            if (isActiveAndEnabled) Register();
        }
        private void OnEnable() => Register();
        private void Register()
        {
            if (!Application.isPlaying || group == null || registration != null) return;
            try { registration = SettingsRuntime.Service.Register(group); }
            catch (ArgumentException exception) { Debug.LogError(exception.Message, this); }
        }
        private void OnDisable() { registration?.Dispose(); registration = null; }
    }
}
