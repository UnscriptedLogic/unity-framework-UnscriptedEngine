using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnscriptedEngine.Configuration;
using UnscriptedEngine.Settings;
using Object = UnityEngine.Object;

namespace UnscriptedEngine.Tests
{
    public sealed class ObjectConfigurationTests
    {
        private GameObject root;
        private GameObject observer;
        private TestConfigurableFields values;
        private ConfigurableObject target;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return new EnterPlayMode();
            root = new GameObject("Configurable target");
            observer = new GameObject("Observer");
            values = root.AddComponent<TestConfigurableFields>();
            target = root.AddComponent<ConfigurableObject>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(observer);
            yield return new ExitPlayMode();
        }

        private SettingDefinition Definition(string name) => target.Fields.Single(field => field.Field.Name == name).Definition;
        private ObjectConfigurationSession Open(Func<bool> authorized = null) => new ObjectConfigurationSession(target, observer.transform, 10f, authorized);

        [Test]
        public void DiscoversSerializedFieldsAndReportsUnsupportedWithoutExposingHiddenState()
        {
            CollectionAssert.AreEquivalent(new[] { "Flag", "Amount", "count", "Choice", "Text" }, target.Fields.Select(field => field.Field.Name));
            Assert.AreEqual(1, target.UnsupportedFields.Count);
            StringAssert.Contains("Unsupported", target.UnsupportedFields[0]);
            using ObjectConfigurationSession session = Open();
            SettingDefinition unsupported = session.ActiveGroups.Single(group => group.DisplayName == "Unsupported fields").Settings.Single();
            Assert.IsFalse(session.CanWrite(unsupported));
        }

        [Test]
        public void WritesLiveValuesAndValidatesTypesRangesAndEnumNames()
        {
            using ObjectConfigurationSession session = Open();
            Assert.IsTrue(session.TrySet(Definition("Amount"), 100f));
            Assert.AreEqual(10f, values.Amount);
            Assert.IsFalse(session.TrySet(Definition("Amount"), float.NaN));
            Assert.IsFalse(session.TrySet(Definition("Amount"), 4));
            Assert.IsTrue(session.TrySet(Definition("count"), 23));
            Assert.AreEqual(23, values.Count);
            Assert.IsTrue(session.TrySet(Definition("Choice"), "Second"));
            Assert.AreEqual(TestConfigurableFields.Mode.Second, values.Choice);
            Assert.IsFalse(session.TrySet(Definition("Choice"), "Missing"));
        }

        [Test]
        public void UndoAndResetUseSessionBaselineRatherThanFirstDiscoveredDefault()
        {
            SettingDefinition amount = Definition("Amount");
            values.Amount = 4f;
            using ObjectConfigurationSession session = Open();
            Assert.IsTrue(session.TrySet(amount, 7f));
            Assert.IsTrue(session.Undo());
            Assert.AreEqual(4f, values.Amount);
            Assert.IsTrue(session.TrySet(amount, 8f));
            Assert.IsTrue(session.Reset(amount));
            Assert.AreEqual(4f, values.Amount);
            Assert.IsTrue(session.Undo());
            Assert.AreEqual(8f, values.Amount);
        }

        [Test]
        public void RejectsOutOfRangeDisabledUnauthorizedAndDisposedEdits()
        {
            using ObjectConfigurationSession session = Open();
            SettingDefinition amount = Definition("Amount");
            observer.transform.position = Vector3.right * 11f;
            Assert.IsFalse(session.TrySet(amount, 8f));
            observer.transform.position = Vector3.zero;
            target.enabled = false;
            Assert.IsFalse(session.TrySet(amount, 8f));
            target.enabled = true;
            using ObjectConfigurationSession unauthorized = Open(() => false);
            Assert.IsFalse(unauthorized.TrySet(amount, 8f));
            session.Dispose();
            Assert.IsFalse(session.TrySet(amount, 8f));
            Assert.AreEqual(2f, values.Amount);
        }

        [Test]
        public void ObservesGameplayChangesAndReleasesSubscriptions()
        {
            using ObjectConfigurationSession session = Open();
            int notifications = 0;
            IDisposable subscription = session.Observe(Definition("Amount"), value => notifications++);
            values.Amount = 6f;
            session.Refresh();
            session.Refresh();
            Assert.AreEqual(2, notifications);
            subscription.Dispose();
            values.Amount = 7f;
            session.Refresh();
            Assert.AreEqual(2, notifications);
        }

        [Test]
        public void ExplicitComponentsCannotReachUnrelatedOrNestedConfigurableObjects()
        {
            GameObject child = new GameObject("Child");
            child.transform.SetParent(root.transform);
            TestConfigurableFields childValues = child.AddComponent<TestConfigurableFields>();
            child.AddComponent<ConfigurableObject>();
            TestConfigurableFields unrelated = observer.AddComponent<TestConfigurableFields>();
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty components = serialized.FindProperty("components");
            components.arraySize = 4;
            components.GetArrayElementAtIndex(0).objectReferenceValue = values;
            components.GetArrayElementAtIndex(1).objectReferenceValue = values;
            components.GetArrayElementAtIndex(2).objectReferenceValue = childValues;
            components.GetArrayElementAtIndex(3).objectReferenceValue = unrelated;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.AreEqual(5, target.Fields.Count);
            Assert.IsTrue(target.Fields.All(field => field.Component == values));
        }

        [Test]
        public void ComponentAdapterCanRejectEditsWithoutChangingValueOrUndoHistory()
        {
            TestConfigurableAdapter adapter = root.AddComponent<TestConfigurableAdapter>();
            using ObjectConfigurationSession session = Open();
            SettingDefinition number = Definition("Number");
            Assert.IsFalse(session.TrySet(number, -1));
            Assert.AreEqual(2, adapter.Number);
            Assert.IsFalse(session.CanUndo);
            Assert.IsTrue(session.TrySet(number, 12));
            Assert.AreEqual(12, adapter.Number);
            Assert.IsTrue(session.Undo());
            Assert.AreEqual(2, adapter.Number);
        }

        [Test]
        public void InstancesHaveIndependentValuesAndStableSchemaIds()
        {
            TestConfigurableFields secondValues = observer.AddComponent<TestConfigurableFields>();
            ConfigurableObject second = observer.AddComponent<ConfigurableObject>();
            using ObjectConfigurationSession firstSession = Open();
            using ObjectConfigurationSession secondSession = new ObjectConfigurationSession(second, root.transform, 10f);
            SettingDefinition first = Definition("Amount");
            SettingDefinition other = second.Fields.Single(field => field.Field.Name == "Amount").Definition;
            Assert.AreEqual(first.Id, other.Id);
            Assert.IsTrue(firstSession.TrySet(first, 9f));
            Assert.AreEqual(2f, secondValues.Amount);
            Assert.AreEqual(2f, secondSession.Get(other));
        }
    }
}
