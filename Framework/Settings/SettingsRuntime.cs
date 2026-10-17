using System;
using System.IO;
using UnityEngine;

namespace UnscriptedEngine.Settings
{
    /// <summary>Application-wide default service. Tests can construct isolated services directly.</summary>
    public static class SettingsRuntime
    {
        private static SettingsService service;
        private static bool dirty;
        public static string FilePath => Path.Combine(Application.persistentDataPath, "Settings.v2.json");
        public static SettingsService Service
        {
            get
            {
                if (service != null) return service;
                service = new SettingsService();
                var preferences = new SettingsPreferences();
                try { new JsonSettingsStore(FilePath).LoadInto(preferences); service.LoadPreferences(preferences); }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
                { Debug.LogWarning($"Could not load settings. Using defaults. {e.Message}"); }
                service.PreferencesChanged += () => dirty = true;
                return service;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { service = null; dirty = false; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            _ = Service;
            var host = new GameObject("Settings Persistence");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<SettingsPersistenceDriver>();
        }

        public static bool Save()
        {
            if (service == null || !dirty) return true;
            try { new JsonSettingsStore(FilePath).Save(service.ExportPreferences()); dirty = false; return true; }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is NotSupportedException)
            { Debug.LogWarning($"Could not save settings. {e.Message}"); return false; }
        }

        public static void ImportPreferences(SettingsPreferences preferences)
        { Service.LoadPreferences(preferences); dirty = true; }
    }
}
