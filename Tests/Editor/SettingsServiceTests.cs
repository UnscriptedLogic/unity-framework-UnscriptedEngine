using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnscriptedEngine.Settings;
using UnscriptedEngine.Editor;

namespace UnscriptedEngine.Tests
{
    public sealed class SettingsServiceTests
    {
        private static Setting<float> Number(string id = "test.number", SettingScope scope = SettingScope.LocalUser,
            string group = "Prototype/Experimental")
        {
            return Setting.Float(id, 2f, 0f, 10f, "Number", group, scope: scope);
        }

        private static IDisposable Register(SettingsService service, params SettingDefinition[] definitions)
        {
            return service.Register(definitions);
        }

        [Test]
        public void ReadsAndSubscriptionsDoNotActivateCatalogs()
        {
            SettingsService service = new SettingsService();
            Setting<float> number = Number();
            using IDisposable subscription = service.Observe(number, value => Assert.AreEqual(2f, value));
            Assert.AreEqual(2f, service.Get(number));
            Assert.IsFalse(service.TrySet(number, 5f));
            Assert.IsEmpty(service.ActiveGroups);
        }

        [Test]
        public void LastOwnerRemovalRevertsThenReactivationRestoresPreference()
        {
            SettingsService service = new SettingsService();
            Setting<float> number = Number();
            List<float> changes = new List<float>();
            using IDisposable subscription = service.Observe(number, changes.Add);
            IDisposable first = Register(service, number);
            IDisposable second = Register(service, number);
            Assert.IsTrue(service.TrySet(number, 7f));
            first.Dispose();
            Assert.AreEqual(7f, service.Get(number));
            second.Dispose();
            second.Dispose();
            Assert.AreEqual(2f, service.Get(number));
            using IDisposable restored = Register(service, number);
            Assert.AreEqual(7f, service.Get(number));
            CollectionAssert.AreEqual(new float[] { 2f, 7f, 2f, 7f }, changes);
        }

        [Test]
        public void PrototypeSwitchRemovesUnrelatedSettingsAndRetainsSavedOverrides()
        {
            SettingsService service = new SettingsService();
            Setting<float> general = Number("general.value", group: "General");
            Setting<float> grapple = Number("grapple.value", group: "Grapple");
            Setting<float> racing = Number("racing.value", group: "Racing");
            IDisposable firstScene = Register(service, general, grapple);
            service.TrySet(grapple, 8f);
            firstScene.Dispose();
            using IDisposable secondScene = Register(service, general, racing);
            CollectionAssert.AreEquivalent(new SettingDefinition[] { general, racing }, service.GetActiveDefinitions());
            Assert.AreEqual(2f, service.Get(grapple));
            using IDisposable returnedScene = Register(service, grapple);
            Assert.AreEqual(8f, service.Get(grapple));
        }

        [Test]
        public void SharedCategoriesMergeAndDuplicateRegistrationsDoNotDuplicateRows()
        {
            SettingsService service = new SettingsService();
            Setting<float> first = Number("camera.one", group: "General/Camera");
            Setting<float> second = Number("camera.two", group: "General/Camera");
            using IDisposable ownerOne = Register(service, first, first);
            using IDisposable ownerTwo = Register(service, first, second);
            Assert.AreEqual(1, service.ActiveGroups.Count);
            SettingsGroup camera = service.ActiveGroups[0].Children.Single();
            Assert.AreEqual("General/Camera", camera.Path);
            Assert.AreEqual(2, camera.Settings.Count);
        }

        [Test]
        public void InvalidRegistrationDoesNotPartiallyActivateSettings()
        {
            SettingsService service = new SettingsService();
            Setting<float> first = Number();
            Setting<float> collision = Number();
            Assert.Throws<ArgumentException>(() => Register(service, first, collision));
            Assert.Throws<ArgumentException>(() => Register(service, first, null));
            Assert.IsEmpty(service.GetActiveDefinitions());
            service.Get(first);
            Assert.Throws<ArgumentException>(() => service.Get(collision));
        }

        [Test]
        public void ValidationRejectsNonFiniteAndWrongTypesClampsAndSuppressesDuplicateEvents()
        {
            SettingsService service = new SettingsService();
            Setting<float> number = Number();
            using IDisposable registration = Register(service, number);
            int events = 0;
            using IDisposable subscription = service.Observe(number, value => events++, false);
            Assert.IsFalse(service.TrySet(number, float.NaN));
            Assert.IsFalse(service.TrySet(number, float.PositiveInfinity));
            Assert.IsFalse(service.TrySet((SettingDefinition)number, "bad"));
            Assert.IsTrue(service.TrySet(number, 100f));
            Assert.AreEqual(10f, service.Get(number));
            service.TrySet(number, 50f);
            Assert.AreEqual(1, events);
            subscription.Dispose();
            service.TrySet(number, 5f);
            Assert.AreEqual(1, events);
        }

        [Test]
        public void ExistingJsonPayloadLoadsWithoutChangingStableIds()
        {
            SettingsService service = new SettingsService();
            Setting<float> number = Number("general.controls.mouse-sensitivity");
            SettingsPreferences preferences = new SettingsPreferences
            {
                values = new List<SettingValueRecord>
                {
                    new SettingValueRecord { id = number.Id, value = "{\"value\":6.0}" }
                }
            };
            service.LoadPreferences(preferences);
            Assert.AreEqual(2f, service.Get(number));
            using IDisposable registration = Register(service, number);
            Assert.AreEqual(6f, service.Get(number));
            SettingsPreferences roundTrip = JsonUtility.FromJson<SettingsPreferences>(JsonUtility.ToJson(service.ExportPreferences()));
            SettingsService restored = new SettingsService();
            restored.LoadPreferences(roundTrip);
            using IDisposable restoredRegistration = Register(restored, number);
            Assert.AreEqual(6f, restored.Get(number));
            Assert.IsTrue(restored.Reset(number));
            Assert.AreEqual(2f, restored.Get(number));
            Assert.IsEmpty(restored.ExportPreferences().values);
        }

        [Test]
        public void InvalidSavedValueFallsBackWithoutBreakingRegistration()
        {
            SettingsService service = new SettingsService();
            Setting<float> number = Number();
            service.LoadPreferences(new SettingsPreferences
            {
                values = new List<SettingValueRecord> { new SettingValueRecord { id = number.Id, value = "broken" } }
            });
            using IDisposable registration = Register(service, number);
            Assert.AreEqual(2f, service.Get(number));
        }

        [Test]
        public void ClientCannotWriteServerSettingsAndLocalRemovalDoesNotResetReplication()
        {
            SettingsService service = new SettingsService(SettingsRole.Client);
            Setting<float> number = Number(scope: SettingScope.ServerSession);
            IDisposable registration = Register(service, number);
            Assert.IsFalse(service.CanWrite(number));
            Assert.IsFalse(service.TrySet(number, 7f));
            Assert.IsFalse(service.Reset(number));
            Assert.IsTrue(service.ApplyServerValue(number, 8f));
            registration.Dispose();
            Assert.AreEqual(8f, service.Get(number));
            Assert.IsEmpty(service.ActiveGroups);
            Assert.IsEmpty(service.ExportPreferences().values);
            service.SetRole(SettingsRole.Standalone);
            Assert.AreEqual(2f, service.Get(number));
        }

        [Test]
        public void ServerSettingsNeverPersistAndClientPreferencesRemainWritable()
        {
            Setting<float> serverNumber = Number("server", SettingScope.ServerSession);
            SettingsService server = new SettingsService(SettingsRole.Server);
            using IDisposable serverRegistration = Register(server, serverNumber);
            Assert.IsTrue(server.TrySet(serverNumber, 5f));
            Assert.IsFalse(server.ApplyServerValue(serverNumber, 9f));
            Assert.IsEmpty(server.ExportPreferences().values);

            Setting<float> localNumber = Number("local");
            SettingsService client = new SettingsService(SettingsRole.Client);
            using IDisposable localRegistration = Register(client, localNumber);
            Assert.IsTrue(client.TrySet(localNumber, 4f));
            Assert.AreEqual(4f, client.Get(localNumber));
        }

        [Test]
        public void ChoiceDefinitionsCopyOptionsAndRejectUnknownValues()
        {
            string[] choices = { "low", "high" };
            Setting<string> choice = Setting.Choice("choice", "low", choices, "Choice", "General");
            choices[0] = "changed";
            SettingsService service = new SettingsService();
            using IDisposable registration = Register(service, choice);
            Assert.AreEqual("low", service.Get(choice));
            Assert.IsFalse(service.TrySet(choice, "changed"));
            Assert.IsTrue(service.TrySet(choice, "high"));
            Assert.AreEqual("high", service.Get(choice));
        }

        [Test]
        public void InvalidDefinitionsFailAtDeclaration()
        {
            Assert.Throws<ArgumentException>(() => Setting.Bool("", true, "Label", "General"));
            Assert.Throws<ArgumentException>(() => Setting.Bool("id", true, "Label", "General//Camera"));
            Assert.Throws<ArgumentException>(() => Setting.Float("id", 1f, 10f, 0f, "Label", "General"));
            Assert.Throws<ArgumentException>(() => Setting.Float("id", float.NaN, 0f, 1f, "Label", "General"));
            Assert.Throws<ArgumentException>(() => Setting.Choice("id", "missing", new string[] { "one" }, "Label", "General"));
        }

        public static class TestCatalog
        {
            public static readonly Setting<bool> Top = Setting.Bool("catalog.top", true, "Top", "General");
            public static class Camera
            {
                public static readonly Setting<float> Fov = Number("catalog.fov", group: "General/Camera");
            }
        }

        public static class InvalidCatalog
        {
            public static Setting<bool> Mutable = Setting.Bool("catalog.mutable", true, "Mutable", "General");
        }

        [Test]
        public void CatalogDiscoveryIncludesNestedTypesAndDoesNotActivateThem()
        {
            SettingsService service = new SettingsService();
            IReadOnlyList<SettingDefinition> definitions = SettingsCatalog.GetDefinitions(typeof(TestCatalog));
            Assert.AreEqual(2, definitions.Count);
            Assert.AreSame(definitions, SettingsCatalog.GetDefinitions(typeof(TestCatalog)));
            Assert.IsEmpty(service.GetActiveDefinitions());
            Assert.Throws<ArgumentException>(() => SettingsCatalog.GetDefinitions(typeof(InvalidCatalog)));
            using IDisposable registration = service.Register(SettingsCatalog.GetDefinitions(typeof(TestCatalog.Camera)));
            CollectionAssert.AreEqual(new SettingDefinition[] { TestCatalog.Camera.Fov }, service.GetActiveDefinitions());
        }

        [Test]
        public void LinkerPreservationIncludesNestedCatalogs()
        {
            string xml = SettingsCatalogLinkerProcessor.BuildPreservationXml(new Type[] { typeof(TestCatalog) });
            StringAssert.Contains(typeof(TestCatalog).FullName.Replace('+', '/'), xml);
            StringAssert.Contains(typeof(TestCatalog.Camera).FullName.Replace('+', '/'), xml);
            StringAssert.Contains("preserve=\"all\"", xml);
        }
    }
}
