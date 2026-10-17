using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnscriptedEngine.Settings;

namespace UnscriptedEngine.Tests
{
    public sealed class SettingsServiceTests
    {
        private readonly List<UnityEngine.Object> objects = new();
        private T Asset<T>() where T : ScriptableObject
        { var asset = ScriptableObject.CreateInstance<T>(); objects.Add(asset); return asset; }
        private FloatSettingDefinition Number(string id = "test.number", SettingScope scope = SettingScope.LocalUser)
        {
            var result = Asset<FloatSettingDefinition>(); result.Configure(id, "Number", scope);
            result.SetRange(0, 10); result.SetDefault(2); return result;
        }
        private SettingsGroup Group(params SettingDefinition[] definitions)
        { var group = Asset<SettingsGroup>(); group.Configure("Test", definitions); return group; }
        [TearDown] public void TearDown()
        { foreach (var asset in objects) UnityEngine.Object.DestroyImmediate(asset); objects.Clear(); }

        [Test] public void InactiveSettingsReadDefaultsAndRejectWrites()
        {
            var service = new SettingsService(); var number = Number();
            Assert.AreEqual(2, service.Get(number)); Assert.IsFalse(service.TrySet(number, 5f));
        }

        [Test] public void LastOwnerRemovalRevertsThenReactivationRestoresPreference()
        {
            var service = new SettingsService(); var number = Number(); var group = Group(number);
            var changes = new List<float>();
            using var subscription = service.Observe(number, changes.Add);
            var first = service.Register(group); var second = service.Register(group);
            Assert.IsTrue(service.TrySet(number, 7f));
            first.Dispose(); Assert.AreEqual(7, service.Get(number));
            second.Dispose(); second.Dispose(); Assert.AreEqual(2, service.Get(number));
            using var restored = service.Register(group); Assert.AreEqual(7, service.Get(number));
            CollectionAssert.AreEqual(new[] { 2f, 7f, 2f, 7f }, changes);
        }

        [Test] public void OverlappingGroupsDoNotDeactivateEachOther()
        {
            var service = new SettingsService(); var number = Number();
            var first = service.Register(Group(number)); var second = service.Register(Group(number));
            service.TrySet(number, 4f); first.Dispose(); Assert.AreEqual(4, service.Get(number));
            second.Dispose(); Assert.AreEqual(2, service.Get(number));
        }

        [Test] public void ValidationRejectsNonFiniteAndWrongTypesClampsRangeAndSuppressesDuplicateEvents()
        {
            var service = new SettingsService(); var number = Number(); using var module = service.Register(Group(number));
            int events = 0; using var subscription = service.Observe(number, _ => events++, false);
            Assert.IsFalse(service.TrySet(number, float.NaN)); Assert.IsFalse(service.TrySet(number, float.PositiveInfinity));
            Assert.IsFalse(service.TrySet((SettingDefinition)number, "bad"));
            Assert.IsTrue(service.TrySet(number, 100f)); Assert.AreEqual(10, service.Get(number));
            service.TrySet(number, 50f); Assert.AreEqual(1, events);
            subscription.Dispose(); service.TrySet(number, 5f); Assert.AreEqual(1, events);
        }

        [Test] public void InvalidRegistrationDoesNotPartiallyActivateSettings()
        {
            var service = new SettingsService(); var first = Number(); var collision = Number();
            Assert.Throws<ArgumentException>(() => service.Register(Group(first, collision)));
            Assert.IsFalse(service.IsActive(first));
            var root = Group(first); root.Configure("Cycle", new[] { first }, root);
            Assert.Throws<ArgumentException>(() => service.Register(root));
            Assert.IsFalse(service.IsActive(first));
        }

        [Test] public void ConflictingDefinitionsCannotBeReadWithSameId()
        {
            var service = new SettingsService(); service.Get(Number());
            Assert.Throws<ArgumentException>(() => service.Get(Number()));
        }

        [Test] public void NestedGroupsActivateLeavesAndDisposeAllLeaves()
        {
            var service = new SettingsService(); var number = Number(); var nested = Group(number);
            var root = Group(); root.Configure("Root", null, nested);
            var module = service.Register(root); Assert.IsTrue(service.IsActive(number));
            module.Dispose(); Assert.IsFalse(service.IsActive(number));
        }

        [Test] public void PreferencesRoundTripWhileInactiveAndResetRemovesSavedOverride()
        {
            var number = Number(); var group = Group(number); var original = new SettingsService();
            using var first = original.Register(group); original.TrySet(number, 6f);
            string json = JsonUtility.ToJson(original.ExportPreferences());
            var loaded = new SettingsService(); loaded.LoadPreferences(JsonUtility.FromJson<SettingsPreferences>(json));
            Assert.AreEqual(2, loaded.Get(number));
            using var second = loaded.Register(group); Assert.AreEqual(6, loaded.Get(number));
            Assert.IsTrue(loaded.Reset(number)); Assert.AreEqual(2, loaded.Get(number));
            Assert.IsEmpty(loaded.ExportPreferences().values);
        }

        [Test] public void InvalidSavedValueFallsBackWithoutBreakingOtherSettings()
        {
            var service = new SettingsService(); var number = Number();
            service.LoadPreferences(new SettingsPreferences { values = new() { new() { id = number.Id, value = "broken" } } });
            using var module = service.Register(Group(number)); Assert.AreEqual(2, service.Get(number));
        }

        [Test] public void ClientsCannotWriteServerSettingsAndLocalRemovalCannotResetReplicatedValue()
        {
            var service = new SettingsService(SettingsRole.Client); var number = Number(scope: SettingScope.ServerSession);
            var module = service.Register(Group(number));
            Assert.IsFalse(service.CanWrite(number)); Assert.IsFalse(service.TrySet(number, 7f));
            Assert.IsFalse(service.Reset(number)); Assert.IsTrue(service.ApplyServerValue(number, 8f));
            module.Dispose(); Assert.AreEqual(8, service.Get(number));
            Assert.IsEmpty(service.ExportPreferences().values);
            service.SetRole(SettingsRole.Standalone); Assert.AreEqual(2, service.Get(number));
        }

        [Test] public void ServerSettingsNeverPersistAndOnlyClientAdapterAcceptsReplication()
        {
            var service = new SettingsService(SettingsRole.Server); var number = Number(scope: SettingScope.ServerSession);
            using var module = service.Register(Group(number)); Assert.IsTrue(service.TrySet(number, 5f));
            Assert.IsFalse(service.ApplyServerValue(number, 9f)); Assert.IsEmpty(service.ExportPreferences().values);
        }

        [Test] public void ClientLocalPreferencesRemainWritable()
        {
            var service = new SettingsService(SettingsRole.Client); var number = Number();
            using var module = service.Register(Group(number)); Assert.IsTrue(service.TrySet(number, 4f));
            Assert.AreEqual(4, service.Get(number));
        }

        [Test] public void ChoiceValidationUsesStableStrings()
        {
            var choice = Asset<StringSettingDefinition>(); choice.Configure("choice", "Choice");
            choice.SetChoices("low", "high"); choice.SetDefault("low");
            var service = new SettingsService(); using var module = service.Register(Group(choice));
            Assert.IsFalse(service.TrySet(choice, "unknown")); Assert.IsTrue(service.TrySet(choice, "high"));
            Assert.AreEqual("high", service.Get(choice));
        }
    }
}
