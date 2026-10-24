using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnscriptedEngine.Settings
{
    /// <summary>Add a concrete provider to a scene-local Settings Container GameObject.</summary>
    public abstract class SettingsProvider : MonoBehaviour
    {
        private IDisposable registration;
        protected abstract Type CatalogType { get; }

        public virtual IReadOnlyList<SettingDefinition> GetDefinitions()
        {
            return SettingsCatalog.GetDefinitions(CatalogType);
        }

        protected virtual void OnEnable() => RegisterSettings();
        protected virtual void OnDisable() => UnregisterSettings();
        protected virtual void OnDestroy() => UnregisterSettings();

        private void RegisterSettings()
        {
            if (!Application.isPlaying || registration != null) return;
            try
            {
                registration = SettingsRuntime.Service.Register(GetDefinitions());
            }
            catch (Exception exception)
            {
                Debug.LogError($"Could not register settings provider '{GetType().Name}': {exception.Message}", this);
            }
        }

        private void UnregisterSettings()
        {
            registration?.Dispose();
            registration = null;
        }
    }
}
