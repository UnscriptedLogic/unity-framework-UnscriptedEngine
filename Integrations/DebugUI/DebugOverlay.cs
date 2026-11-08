using System;
using System.Collections.Generic;
using UnityEngine;
using UnscriptedEngine.Settings;

namespace UnscriptedEngine.UI
{
    public enum DebugOverlaySide { TopLeft, TopRight }

    /// <summary>Main-thread, frame-based debug values. Call Show from Update, LateUpdate or FixedUpdate.</summary>
    public static class DebugOverlay
    {
        internal sealed class Entry
        {
            internal UnityEngine.Object Owner;
            internal string Name;
            internal string Value;
            internal DebugOverlaySide Side;
            internal float LastSeen;
        }

        private static readonly Dictionary<(UnityEngine.Object, string), Entry> entries = new Dictionary<(UnityEngine.Object, string), Entry>();
        private static readonly List<(UnityEngine.Object, string)> expired = new List<(UnityEngine.Object, string)>();
        internal static IEnumerable<Entry> Entries => entries.Values;

        public static readonly Setting<bool> VisibleSetting = Setting.Bool(
            "debug.overlay.visible", false, "Debug overlay (F3)", "Debug",
            description: "Show live debug values in the upper corners. F3 also toggles this setting.");

        public static bool IsVisible => SettingsRuntime.Service.Get(VisibleSetting);

        /// <summary>Updates one named value for this owner. Entries expire after updates stop.</summary>
        public static void Show<T>(UnityEngine.Object owner, string name, T value,
            DebugOverlaySide side = DebugOverlaySide.TopLeft)
        {
            if (!IsVisible || owner == null) return;
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A debug value name is required.", nameof(name));
            (UnityEngine.Object, string) key = (owner, name);
            if (!entries.TryGetValue(key, out Entry entry))
            {
                entry = new Entry { Owner = owner, Name = name };
                entries.Add(key, entry);
            }
            entry.Value = value == null ? "null" : value.ToString();
            entry.Side = side;
            entry.LastSeen = Time.unscaledTime;
        }

        /// <summary>Immediately removes all values belonging to an owner.</summary>
        public static void Remove(UnityEngine.Object owner)
        {
            if (owner == null) return;
            expired.Clear();
            foreach (KeyValuePair<(UnityEngine.Object, string), Entry> pair in entries)
                if (pair.Value.Owner == owner) expired.Add(pair.Key);
            RemoveExpired();
        }

        internal static void Prune()
        {
            expired.Clear();
            foreach (KeyValuePair<(UnityEngine.Object, string), Entry> pair in entries)
            {
                Entry entry = pair.Value;
                if (entry.Owner == null || Time.unscaledTime - entry.LastSeen > 0.5f ||
                    entry.Owner is Behaviour behaviour && !behaviour.isActiveAndEnabled ||
                    entry.Owner is Component component && !component.gameObject.activeInHierarchy)
                    expired.Add(pair.Key);
            }
            RemoveExpired();
        }

        private static void RemoveExpired()
        {
            foreach ((UnityEngine.Object, string) key in expired) entries.Remove(key);
            expired.Clear();
        }

        internal static void Clear() => entries.Clear();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            entries.Clear();
            expired.Clear();
        }
    }
}
