using System;
using UnityEngine;
using UnscriptedEngine.Settings;

namespace UnscriptedEngine.UI.Settings
{
    /// <summary>Base contract for themed controls. Own only UI here; writes go through the service.</summary>
    public abstract class SettingsWidget : MonoBehaviour
    {
        protected SettingsService Service { get; private set; }
        protected SettingDefinition Definition { get; private set; }
        private IDisposable subscription;
        public void Bind(SettingsService service, SettingDefinition definition)
        {
            Unbind();
            Service = service;
            Definition = definition;
            OnBound();
            if (isActiveAndEnabled) Subscribe();
        }
        private void Subscribe()
        {
            subscription?.Dispose();
            subscription = Service.Observe(Definition, DisplayValue);
        }
        public void Unbind()
        {
            subscription?.Dispose(); subscription = null;
            if (Service != null) OnUnbound();
            Service = null; Definition = null;
        }
        protected bool Write(object value)
        {
            bool accepted = Service.TrySet(Definition, value);
            DisplayValue(Service.Get(Definition));
            return accepted;
        }
        protected abstract void OnBound();
        protected abstract void DisplayValue(object value);
        protected virtual void OnUnbound() { }
        protected virtual void OnEnable() { if (Service != null) Subscribe(); }
        protected virtual void OnDisable() { subscription?.Dispose(); subscription = null; }
        protected virtual void OnDestroy() => Unbind();
    }
}
