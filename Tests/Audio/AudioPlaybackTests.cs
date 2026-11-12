using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnscriptedEngine.Audio;
using UnscriptedEngine.Settings;
using UnityEngine;
using UnityEngine.TestTools;

namespace UnscriptedEngine.Tests
{
    public sealed class AudioPlaybackTests
    {
        [TestCase("")]
        [TestCase("UI")]
        [TestCase("bad category")]
        public void InvalidCategoryIdsAreRejected(string category)
        {
            Assert.Throws<ArgumentException>(() => AudioRuntime.RegisterCategory(category));
        }

        [UnityTest]
        public IEnumerator FinishedOneShotReleasesItsHandle()
        {
            AudioClip clip = AudioClip.Create("Short audio test", 441, 1, 44100, false);
            AudioVoice voice = AudioRuntime.Play(clip);
            try
            {
                Assert.IsNotNull(voice);
                yield return new WaitForSecondsRealtime(0.2f);
                Assert.IsFalse(voice.IsValid);
            }
            finally
            {
                voice?.Dispose();
                UnityEngine.Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void BindingMultipliesCategoryAndBaseAndHonorsMaster()
        {
            SettingsPreferences original = SettingsRuntime.Service.ExportPreferences();
            GameObject host = new GameObject("Audio binding test");
            AudioSource source = host.AddComponent<AudioSource>();
            source.volume = 0.5f;
            source.ignoreListenerVolume = true;
            AudioVoice voice = null;
            try
            {
                Setting<float> category = AudioRuntime.RegisterCategory("audio-test", "Audio test");
                Assert.AreSame(category, AudioRuntime.RegisterCategory("audio-test"));
                Assert.IsTrue(SettingsRuntime.Service.IsActive(category));
                AudioRuntime.SetCategoryVolume("audio-test", 0.4f);
                voice = AudioRuntime.Bind(source, "audio-test");
                Assert.AreEqual(0.2f, source.volume, 0.0001f);
                Assert.IsFalse(source.ignoreListenerVolume);
                AudioRuntime.SetMasterVolume(0.3f);
                Assert.AreEqual(0.3f, AudioListener.volume, 0.0001f);
                voice.Volume = 0.25f;
                Assert.AreEqual(0.1f, source.volume, 0.0001f);
                AudioRuntime.SetCategoryVolume("audio-test", 0f);
                Assert.AreEqual(0f, source.volume);
                Assert.Throws<InvalidOperationException>(() => AudioRuntime.Bind(source));
                voice.Dispose();
                Assert.IsFalse(voice.IsValid);
                Assert.AreEqual(0.25f, source.volume, 0.0001f);
                voice.Dispose();
            }
            finally
            {
                voice?.Dispose();
                UnityEngine.Object.DestroyImmediate(host);
                SettingsRuntime.ImportPreferences(original);
            }
        }

        [Test]
        public void PoolLimitsVoicesAndReleasedHandlesCannotControlReusedSources()
        {
            AudioClip clip = AudioClip.Create("Pool test", 44100, 1, 44100, false);
            List<AudioVoice> voices = new List<AudioVoice>();
            try
            {
                for (int index = 0; index < AudioRuntime.MaxPooledVoices; index++)
                {
                    AudioVoice voice = AudioRuntime.Play(clip, loop: true);
                    Assert.IsNotNull(voice);
                    voices.Add(voice);
                }
                Assert.IsNull(AudioRuntime.Play(clip));
                AudioVoice released = voices[0];
                released.Stop();
                AudioVoice replacement = AudioRuntime.Play(clip, loop: true);
                Assert.IsNotNull(replacement);
                voices.Add(replacement);
                released.Stop();
                Assert.IsTrue(replacement.IsValid);
                Assert.IsFalse(released.IsValid);
            }
            finally
            {
                foreach (AudioVoice voice in voices) voice.Dispose();
                UnityEngine.Object.DestroyImmediate(clip);
            }
        }
    }
}
