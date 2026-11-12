using System;
using System.Collections.Generic;
using UnscriptedEngine.Settings;
using UnityEngine;

namespace UnscriptedEngine.Audio
{
    /// <summary>Main-thread shared audio playback. No scene manager or mixer asset required.</summary>
    public static class AudioRuntime
    {
        private sealed class CategoryState
        {
            public Setting<float> Definition;
            public float Volume;
            public IDisposable Registration;
            public IDisposable Subscription;
        }

        private static readonly Dictionary<string, CategoryState> categories = new Dictionary<string, CategoryState>(StringComparer.Ordinal);
        private static readonly List<AudioVoice> voices = new List<AudioVoice>();
        private static readonly Stack<AudioSource> pool = new Stack<AudioSource>();
        private static AudioRuntimeDriver driver;
        private static IDisposable masterRegistration;
        private static IDisposable masterSubscription;
        private static bool initialized;
        public const int MaxPooledVoices = 64;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Shutdown();
            AudioListener.volume = 1f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap() => EnsureInitialized();

        private static void EnsureInitialized()
        {
            if (initialized) return;
            if (!Application.isPlaying) throw new InvalidOperationException("AudioRuntime requires Play mode.");
            initialized = true;
            GameObject host = new GameObject("Audio Runtime");
            UnityEngine.Object.DontDestroyOnLoad(host);
            driver = host.AddComponent<AudioRuntimeDriver>();
            masterRegistration = SettingsRuntime.Service.Register(new[] { AudioVolumeSettings.MasterVolume });
            masterSubscription = SettingsRuntime.Service.Observe(AudioVolumeSettings.MasterVolume, value => AudioListener.volume = value);
            RegisterCategory(AudioCategories.Music);
            RegisterCategory(AudioCategories.SoundEffects);
            RegisterCategory(AudioCategories.Ambience);
            RegisterCategory(AudioCategories.UI, "UI");
            RegisterCategory(AudioCategories.Player);
            RegisterCategory(AudioCategories.Entities);
            RegisterCategory(AudioCategories.Misc);
        }

        /// <summary>Registers a stable lowercase ID and its slider for the remainder of this session.</summary>
        public static Setting<float> RegisterCategory(string category, string label = null)
        {
            ValidateCategory(category);
            EnsureInitialized();
            if (categories.TryGetValue(category, out CategoryState existing)) return existing.Definition;
            Setting<float> definition = category == AudioCategories.Music ? AudioVolumeSettings.MusicVolume :
                category == AudioCategories.SoundEffects ? AudioVolumeSettings.SoundEffectsVolume :
                Setting.Float("general.audio.category." + category, 1f, 0f, 1f,
                    (label ?? char.ToUpperInvariant(category[0]) + category.Substring(1)) + " volume",
                    "General/Audio", description: "Playback level for " + category + ".", order: 10);
            CategoryState state = new CategoryState { Definition = definition, Volume = definition.DefaultValue };
            categories.Add(category, state);
            state.Registration = SettingsRuntime.Service.Register(new[] { definition });
            state.Subscription = SettingsRuntime.Service.Observe(definition, value =>
            {
                state.Volume = value;
                for (int index = 0; index < voices.Count; index++)
                    if (voices[index].Category == category) voices[index].RefreshVolume();
            });
            return definition;
        }

        private static void ValidateCategory(string category)
        {
            if (string.IsNullOrEmpty(category)) throw new ArgumentException("Category ID is required.", nameof(category));
            foreach (char character in category)
                if (!(character >= 'a' && character <= 'z') && !(character >= '0' && character <= '9') && character != '-')
                    throw new ArgumentException("Use lowercase ASCII letters, digits, and hyphens for category IDs.", nameof(category));
        }

        public static float GetCategoryVolume(string category)
        {
            RegisterCategory(category);
            return categories[category].Volume;
        }

        internal static float GetRegisteredCategoryVolume(string category) => categories[category].Volume;

        public static bool SetCategoryVolume(string category, float volume) =>
            SettingsRuntime.Service.TrySet(RegisterCategory(category), volume);

        public static bool SetMasterVolume(float volume)
        {
            EnsureInitialized();
            return SettingsRuntime.Service.TrySet(AudioVolumeSettings.MasterVolume, volume);
        }

        internal static float ClampVolume(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Clamp01(value);

        /// <summary>Returns null for a missing clip or when the pooled voice limit is reached.</summary>
        public static AudioVoice Play(AudioClip clip, string category = AudioCategories.Misc, float volume = 1f,
            bool loop = false, Vector3? position = null)
        {
            if (clip == null) return null;
            RegisterCategory(category);
            int pooledCount = pool.Count;
            for (int index = 0; index < voices.Count; index++) if (voices[index].Pooled) pooledCount++;
            if (pool.Count == 0 && pooledCount >= MaxPooledVoices) return null;
            AudioSource source;
            if (pool.Count > 0) source = pool.Pop();
            else
            {
                GameObject host = new GameObject("Audio Voice");
                host.transform.SetParent(driver.transform, false);
                source = host.AddComponent<AudioSource>();
                source.playOnAwake = false;
            }
            source.transform.position = position ?? Vector3.zero;
            source.clip = clip;
            source.loop = loop;
            source.spatialBlend = position.HasValue ? 1f : 0f;
            source.ignoreListenerVolume = false;
            AudioVoice voice = new AudioVoice(source, category, volume, true);
            voices.Add(voice);
            source.Play();
            return voice;
        }

        /// <summary>Binds an existing source without starting it. Change volume through the returned handle.</summary>
        public static AudioVoice Bind(AudioSource source, string category = AudioCategories.Misc)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            RegisterCategory(category);
            for (int index = 0; index < voices.Count; index++)
                if (voices[index].Source == source) throw new InvalidOperationException("AudioSource is already bound.");
            source.ignoreListenerVolume = false;
            AudioVoice voice = new AudioVoice(source, category, source.volume, false);
            voices.Add(voice);
            return voice;
        }

        internal static void Release(AudioVoice voice)
        {
            if (!voices.Remove(voice)) return;
            AudioSource source = voice.Source;
            if (source != null)
            {
                source.Stop();
                if (voice.Pooled) { source.clip = null; pool.Push(source); }
                else source.volume = voice.Volume;
            }
            voice.Invalidate();
        }

        internal static void Tick()
        {
            for (int index = voices.Count - 1; index >= 0; index--)
            {
                AudioVoice voice = voices[index];
                if (!voice.IsValid || (voice.Pooled && !voice.IsPlaying && !AudioListener.pause)) Release(voice);
            }
        }

        internal static void Shutdown()
        {
            while (voices.Count > 0) Release(voices[voices.Count - 1]);
            masterSubscription?.Dispose();
            masterRegistration?.Dispose();
            foreach (CategoryState state in categories.Values)
            {
                state.Subscription?.Dispose();
                state.Registration?.Dispose();
            }
            categories.Clear();
            pool.Clear();
            masterRegistration = null;
            masterSubscription = null;
            driver = null;
            initialized = false;
        }
    }
}
