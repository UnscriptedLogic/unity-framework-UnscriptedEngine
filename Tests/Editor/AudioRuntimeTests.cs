using System;
using NUnit.Framework;
using UnscriptedEngine.Audio;
using UnscriptedEngine.Settings;
using UnityEngine;

namespace UnscriptedEngine.Tests
{
    public sealed class AudioRuntimeTests
    {
        [Test]
        public void LegacyAudioDefinitionsPreserveIdsAndDefaults()
        {
            Assert.AreEqual("general.audio.master-volume", AudioVolumeSettings.MasterVolume.Id);
            Assert.AreEqual("general.audio.music-volume", AudioVolumeSettings.MusicVolume.Id);
            Assert.AreEqual(0.8f, AudioVolumeSettings.MusicVolume.DefaultValue);
        }

        [Test]
        public void AudioVolumeSettingsApplySavedValuesWhenRegistered()
        {
            SettingsService service = new SettingsService();
            SettingsPreferences preferences = new SettingsPreferences();
            preferences.values.Add(new SettingValueRecord
            {
                id = AudioVolumeSettings.MasterVolume.Id,
                value = AudioVolumeSettings.MasterVolume.SerializeValue(0.25f)
            });
            service.LoadPreferences(preferences);
            using (IDisposable registration = service.Register(new[] { AudioVolumeSettings.MasterVolume }))
            {
                Assert.AreEqual(0.25f, service.Get(AudioVolumeSettings.MasterVolume));
                Assert.IsTrue(service.TrySet(AudioVolumeSettings.MasterVolume, 0f));
                Assert.AreEqual(0f, service.Get(AudioVolumeSettings.MasterVolume));
            }
        }
    }
}
