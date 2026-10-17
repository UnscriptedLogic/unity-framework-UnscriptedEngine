using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace UnscriptedEngine.Tests
{
    public class ReusableServicesTests
    {
        [Serializable] private class Settings { public float volume = 1; }
        [Serializable] private class SavedEnvelope { public int schemaVersion; public string payload; }

        [Test] public void DisposingOnePanelKeepsOtherPanelBlocked()
        {
            var first = new GameObject("First panel");
            var second = new GameObject("Second panel");
            var a = InputFocus.Acquire(first);
            var b = InputFocus.Acquire(second);
            try
            {
                a.Dispose(); a.Dispose();
                Assert.That(InputFocus.IsBlocked(null), Is.True);
                b.Dispose();
                Assert.That(InputFocus.IsBlocked(null), Is.False);
            }
            finally { a.Dispose(); b.Dispose(); UnityEngine.Object.DestroyImmediate(first); UnityEngine.Object.DestroyImmediate(second); }
        }

        [Test] public void DestroyedPanelDoesNotRetainInputFocus()
        {
            var panel = new GameObject("Panel");
            using var lease = InputFocus.Acquire(panel);
            UnityEngine.Object.DestroyImmediate(panel);
            Assert.That(InputFocus.IsBlocked(null), Is.False);
        }

        [TestCase("{\"volume\":0.25}")]
        [TestCase("{\"schemaVersion\":1,\"payload\":\"{\\\"volume\\\":0.25}\"}")]
        public void SettingsLoadsIndependentFixtures(string fixture)
        {
            string directory = Path.Combine(Path.GetTempPath(), "UnscriptedSettings-" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(directory, "settings.json");
            Directory.CreateDirectory(directory);
            try
            {
                File.WriteAllText(path, fixture);
                var value = new Settings();
                var store = new JsonSettingsStore(path);
                store.LoadInto(value);
                Assert.That(value.volume, Is.EqualTo(0.25f));
            }
            finally { File.Delete(path); Directory.Delete(directory); }
        }

        [Test] public void SettingsWritesDocumentedEnvelopeAndReplacesOldContents()
        {
            WithSettingsFile(path =>
            {
                new JsonSettingsStore(path).Save(new Settings { volume = 0.75f });
                // Read the file format independently of JsonSettingsStore.LoadInto.
                var envelope = JsonUtility.FromJson<SavedEnvelope>(File.ReadAllText(path));
                Assert.That(envelope.schemaVersion, Is.EqualTo(1));
                Assert.That(envelope.payload, Is.Not.Null.And.Not.Empty);
                var payload = JsonUtility.FromJson<Settings>(envelope.payload);
                Assert.That(payload.volume, Is.EqualTo(0.75f));
                Assert.That(Directory.GetFiles(Path.GetDirectoryName(path), "*.tmp"), Is.Empty);
            });
        }

        [TestCase(true)] [TestCase(false)]
        public void FailedSavePreservesOriginalFileAndRemovesTemporaryFile(bool failWrite)
        {
            WithSettingsFile(path =>
            {
                string original = File.ReadAllText(path);
                int writes = 0, replacements = 0;
                var store = new JsonSettingsStore(path, 1,
                    (temporary, json) =>
                    {
                        writes++;
                        Assert.That(temporary, Is.Not.EqualTo(path));
                        Assert.That(File.ReadAllText(path), Is.EqualTo(original));
                        File.WriteAllText(temporary, failWrite ? "partial write" : json);
                        if (failWrite) throw new IOException("write failed");
                    },
                    (temporary, destination) =>
                    {
                        replacements++;
                        Assert.That(File.Exists(temporary), Is.True);
                        Assert.That(destination, Is.EqualTo(path));
                        throw new IOException("replace failed");
                    });
                Assert.Throws<IOException>(() => store.Save(new Settings { volume = 0.75f }));
                Assert.That(writes, Is.EqualTo(1));
                Assert.That(replacements, Is.EqualTo(failWrite ? 0 : 1));
                Assert.That(File.ReadAllText(path), Is.EqualTo(original));
                Assert.That(Directory.GetFiles(Path.GetDirectoryName(path), "*.tmp"), Is.Empty);
            });
        }

        [Test] public void UnsupportedSettingsVersionDoesNotChangeTarget()
        {
            WithSettingsFile(path =>
            {
                File.WriteAllText(path, "{\"schemaVersion\":99,\"payload\":\"{\\\"volume\\\":0.25}\"}");
                var target = new Settings { volume = 0.9f };
                Assert.Throws<ArgumentException>(() => new JsonSettingsStore(path).LoadInto(target));
                Assert.That(target.volume, Is.EqualTo(0.9f));
            });
        }

        private static void WithSettingsFile(Action<string> test)
        {
            string directory = Path.Combine(Path.GetTempPath(), "UnscriptedSettings-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "settings.json");
            File.WriteAllText(path, "{\"volume\":0.25}");
            try { test(path); }
            finally
            {
                foreach (string file in Directory.GetFiles(directory)) File.Delete(file);
                Directory.Delete(directory);
            }
        }

        [Test] public void ValveClampsAndReportsOnlyValueTransitions()
        {
            var obj = new GameObject("Valve");
            try
            {
                var valve = obj.AddComponent<ValveControl>();
                int opened = 0, changed = 0;
                valve.OnOpened.AddListener(() => opened++);
                valve.OnValueChanged.AddListener(_ => changed++);
                valve.SetValue(2); valve.SetValue(1);
                Assert.That(valve.Value, Is.EqualTo(1));
                Assert.That(opened, Is.EqualTo(1));
                Assert.That(changed, Is.EqualTo(1));
                valve.SetValue(-1);
                Assert.That(valve.Value, Is.Zero);
            }
            finally { UnityEngine.Object.DestroyImmediate(obj); }
        }
    }
}
