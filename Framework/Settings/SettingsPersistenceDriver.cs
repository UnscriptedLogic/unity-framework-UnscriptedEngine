using UnityEngine;

namespace UnscriptedEngine.Settings
{
    internal sealed class SettingsPersistenceDriver : MonoBehaviour
    {
        private float nextSave;
        private void Update()
        {
            if (Time.unscaledTime < nextSave) return;
            nextSave = Time.unscaledTime + 2f;
            SettingsRuntime.Save();
        }
        private void OnApplicationPause(bool paused) { if (paused) SettingsRuntime.Save(); }
        private void OnApplicationQuit() => SettingsRuntime.Save();
    }
}
