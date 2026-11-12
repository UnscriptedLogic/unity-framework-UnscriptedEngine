using System;
using UnityEngine;

namespace UnscriptedEngine.Audio
{
    /// <summary>A single playback or external-source binding. Dispose to stop and release it.</summary>
    public sealed class AudioVoice : IDisposable
    {
        private AudioSource source;
        private float volume;
        internal readonly string Category;
        internal readonly bool Pooled;
        public bool IsValid => source != null;
        public bool IsPlaying => source != null && source.isPlaying;
        public float Volume
        {
            get => volume;
            set { volume = AudioRuntime.ClampVolume(value); RefreshVolume(); }
        }

        internal AudioVoice(AudioSource source, string category, float volume, bool pooled)
        {
            this.source = source;
            Category = category;
            Pooled = pooled;
            Volume = volume;
        }

        internal AudioSource Source => source;
        internal void RefreshVolume()
        {
            if (source != null) source.volume = volume * AudioRuntime.GetRegisteredCategoryVolume(Category);
        }

        internal void Invalidate() => source = null;
        public void Dispose() => AudioRuntime.Release(this);
        public void Stop() => Dispose();
    }
}
