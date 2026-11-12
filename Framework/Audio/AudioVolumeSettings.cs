using UnscriptedEngine.Settings;

namespace UnscriptedEngine.Audio
{
    /// <summary>Stable definitions shared with the project's legacy General catalog.</summary>
    public static class AudioVolumeSettings
    {
        public static readonly Setting<float> MasterVolume = Setting.Float(
            "general.audio.master-volume", 1f, 0f, 1f, "Master volume", "General/Audio",
            description: "Overall playback volume.", order: 0);
        public static readonly Setting<float> MusicVolume = Setting.Float(
            "general.audio.music-volume", 0.8f, 0f, 1f, "Music volume", "General/Audio",
            description: "Music playback level.", order: 1);
        public static readonly Setting<float> SoundEffectsVolume = Setting.Float(
            "general.audio.sound-effects-volume", 1f, 0f, 1f, "Sound effects volume", "General/Audio",
            description: "Effects playback level.", order: 2);
    }

    public static class AudioCategories
    {
        public const string Ambience = "ambience";
        public const string UI = "ui";
        public const string Player = "player";
        public const string Entities = "entities";
        public const string Misc = "misc";
        public const string Music = "music";
        public const string SoundEffects = "sound-effects";
    }
}
