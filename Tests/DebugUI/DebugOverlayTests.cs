using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnscriptedEngine.Settings;
using UnscriptedEngine.UI;

namespace UnscriptedEngine.Tests
{
    public sealed class DebugOverlayTests
    {
        private IDisposable registration;
        private GameObject first;
        private GameObject second;
        private bool previousVisibility;

        [SetUp]
        public void SetUp()
        {
            registration = SettingsRuntime.Service.Register(new SettingDefinition[] { DebugOverlay.VisibleSetting });
            previousVisibility = DebugOverlay.IsVisible;
            SettingsRuntime.Service.TrySet(DebugOverlay.VisibleSetting, true);
            DebugOverlay.Clear();
            first = new GameObject("First owner");
            second = new GameObject("Second owner");
        }

        [TearDown]
        public void TearDown()
        {
            DebugOverlay.Clear();
            UnityEngine.Object.DestroyImmediate(first);
            UnityEngine.Object.DestroyImmediate(second);
            SettingsRuntime.Service.TrySet(DebugOverlay.VisibleSetting, previousVisibility);
            registration.Dispose();
        }

        [Test]
        public void RepeatedNameUpdatesValueAndMovesColumnWithoutDuplicating()
        {
            DebugOverlay.Show(first, "Speed", 1);
            DebugOverlay.Show(first, "Speed", 2, DebugOverlaySide.TopRight);
            DebugOverlay.Entry entry = DebugOverlay.Entries.Single();
            Assert.AreEqual("2", entry.Value);
            Assert.AreEqual(DebugOverlaySide.TopRight, entry.Side);
        }

        [Test]
        public void SameNameFromDifferentOwnersStaysIndependent()
        {
            DebugOverlay.Show(first, "Speed", 1);
            DebugOverlay.Show(second, "Speed", 2);
            Assert.AreEqual(2, DebugOverlay.Entries.Count());
            DebugOverlay.Remove(first);
            Assert.AreSame(second, DebugOverlay.Entries.Single().Owner);
        }

        [Test]
        public void DestroyedAndInactiveOwnersArePruned()
        {
            DebugOverlay.Show(first.transform, "Position", Vector3.zero);
            DebugOverlay.Show(second, "Health", 10);
            first.SetActive(false);
            UnityEngine.Object.DestroyImmediate(second);
            DebugOverlay.Prune();
            Assert.IsEmpty(DebugOverlay.Entries);
        }

        [Test]
        public void EntriesExpireWhenPublisherStopsUpdating()
        {
            DebugOverlay.Show(first, "Speed", 1);
            DebugOverlay.Entries.Single().LastSeen = Time.unscaledTime - 1f;
            DebugOverlay.Prune();
            Assert.IsEmpty(DebugOverlay.Entries);
        }

        [Test]
        public void HiddenOverlayDoesNotFormatOrStoreValues()
        {
            SettingsRuntime.Service.TrySet(DebugOverlay.VisibleSetting, false);
            DebugOverlay.Show(first, "Expensive value", new ThrowOnFormat());
            Assert.IsEmpty(DebugOverlay.Entries);
        }

        private sealed class ThrowOnFormat
        {
            public override string ToString() => throw new InvalidOperationException("Should not format hidden values.");
        }
    }
}
