using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnscriptedEngine
{
    /// <summary>
    /// Main-thread logging with session-local controls. Player logs require per-call opt-in.
    /// These controls affect only calls made through this service.
    /// </summary>
    public static class LogService
    {
        private static readonly HashSet<string> _categories = new(StringComparer.OrdinalIgnoreCase);

        public static bool Enabled { get; set; } = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSessionState()
        {
            Enabled = true;
            _categories.Clear();
        }

        /// <summary>Replaces the filter. Null or no categories allows every category.</summary>
        public static void SetCategoryFilter(params string[] categories)
        {
            _categories.Clear();
            if (categories == null) return;
            foreach (string category in categories)
                _categories.Add(NormalizeCategory(category));
        }

        public static void ClearCategoryFilter() => _categories.Clear();

        /// <summary>Use before constructing expensive messages. Build opt-in never overrides other controls.</summary>
        public static bool IsEnabled(string category = "General", bool allowInBuild = false)
        {
            if (!Enabled) return false;
#if !UNITY_EDITOR
            if (!allowInBuild) return false;
#endif
            return _categories.Count == 0 || _categories.Contains(NormalizeCategory(category));
        }

        /// <summary>Logs the source's runtime type, or the represented type when source is a Type.</summary>
        public static void Log(this object source, string message, string category = "General", bool allowInBuild = false)
            => Write(source, message, category, allowInBuild, LogType.Log);

        public static void Warning(this object source, string message, string category = "General", bool allowInBuild = false)
            => Write(source, message, category, allowInBuild, LogType.Warning);

        public static void Error(this object source, string message, string category = "General", bool allowInBuild = false)
            => Write(source, message, category, allowInBuild, LogType.Error);

        public static void Log<T>(string message, string category = "General", bool allowInBuild = false)
            => Write(typeof(T), message, category, allowInBuild, LogType.Log);

        public static void Warning<T>(string message, string category = "General", bool allowInBuild = false)
            => Write(typeof(T), message, category, allowInBuild, LogType.Warning);

        public static void Error<T>(string message, string category = "General", bool allowInBuild = false)
            => Write(typeof(T), message, category, allowInBuild, LogType.Error);

        private static void Write(object source, string message, string category, bool allowInBuild, LogType level)
        {
            if (!IsEnabled(category, allowInBuild)) return;
            Type type = source as Type ?? source?.GetType();
            string text = $"[{NormalizeCategory(category)}] [{type?.Name ?? "Unknown"}] {message}";
            var context = source as UnityEngine.Object;
            switch (level)
            {
                case LogType.Warning: Debug.LogWarning(text, context); break;
                case LogType.Error: Debug.LogError(text, context); break;
                default: Debug.Log(text, context); break;
            }
        }

        private static string NormalizeCategory(string category)
            => string.IsNullOrWhiteSpace(category) ? "General" : category.Trim();
    }
}
