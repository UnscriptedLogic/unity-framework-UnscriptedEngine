using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Framework.GameplayTag;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnscriptedEngine.Configuration;
using UnscriptedEngine.Gameplay;
using UnscriptedEngine.Settings;
using Object = UnityEngine.Object;

namespace UnscriptedEngine.Tests
{
    public sealed class ConfigurationEditorSaveTests
    {
        private string scenePath;

        [UnityTest]
        public IEnumerator SavedSnapshotSurvivesPlayModeAndSupportsUndo()
        {
            scenePath = AssetDatabase.GenerateUniqueAssetPath("Assets/__ConfigurationEditorSaveTest.unity");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            GameObject root = new GameObject("Saved configuration");
            SceneManager.MoveGameObjectToScene(root, scene);
            TestConfigurableFields values = root.AddComponent<TestConfigurableFields>();
            root.AddComponent<ConfigurableObject>();
            root.AddComponent<BoxCollider>().isTrigger = true;
            root.AddComponent<ColliderZone>();
            root.AddComponent<DamageZone>();
            Assert.IsTrue(EditorSceneManager.SaveScene(scene, scenePath));
            GlobalObjectId id = GlobalObjectId.GetGlobalObjectIdSlow(values);

            yield return new EnterPlayMode();
            values = (TestConfigurableFields)GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id);
            Assert.IsNotNull(values);
            values.Amount = 8f;
            values.Position3 = new Vector3(9f, 8f, 7f);
            DamageZone damage = values.GetComponent<DamageZone>();
            damage.ValueChange.Amount = -42f;
            values.GetComponent<ColliderZone>().Filter.Layers = 5;
            Assert.IsTrue(ConfigurationEditorSave.TrySave(values.GetComponent<ConfigurableObject>(), out string message), message);
            values.Amount = 9f;

            GameObject spawned = new GameObject("Runtime-only target");
            spawned.AddComponent<TestConfigurableFields>();
            Assert.IsFalse(ConfigurationEditorSave.TrySave(spawned.AddComponent<ConfigurableObject>(), out _));

            yield return new ExitPlayMode();
            values = (TestConfigurableFields)GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id);
            Assert.AreEqual(8f, values.Amount);
            Assert.AreEqual(new Vector3(9f, 8f, 7f), values.Position3);
            Assert.AreEqual(-42f, values.GetComponent<DamageZone>().ValueChange.Amount);
            Assert.AreEqual(5, values.GetComponent<ColliderZone>().Filter.Layers.value);
            Assert.IsTrue(values.gameObject.scene.isDirty);
            Undo.FlushUndoRecordObjects();
            Undo.PerformUndo();
            Assert.AreEqual(2f, values.Amount);
            Assert.AreEqual(new Vector3(1f, 2f, 3f), values.Position3);
            Assert.AreEqual(-10f, values.GetComponent<DamageZone>().ValueChange.Amount);
            Assert.AreEqual(-1, values.GetComponent<ColliderZone>().Filter.Layers.value);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (EditorApplication.isPlaying) yield return new ExitPlayMode();
            if (string.IsNullOrEmpty(scenePath)) yield break;
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            AssetDatabase.DeleteAsset(scenePath);
        }
    }

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
            CollectionAssert.AreEquivalent(new[] { "Flag", "Amount", "count", "Choice", "Text",
                "Position2", "Position3", "Position4", "Rotation", "Tint" }, target.Fields.Select(field => field.Field.Name));
            Assert.AreEqual(1, target.UnsupportedFields.Count);
            StringAssert.Contains("Unsupported", target.UnsupportedFields[0]);
            using ObjectConfigurationSession session = Open();
            SettingDefinition unsupported = session.ActiveGroups.Single(group => group.DisplayName == "Unsupported fields").Settings.Single();
            Assert.IsFalse(session.CanWrite(unsupported));
        }

        [Test]
        public void NestedZoneSettingsSupportLayersTagFiltersDamageValuesUndoAndReset()
        {
            root.AddComponent<BoxCollider>().isTrigger = true;
            ColliderZone zone = root.AddComponent<ColliderZone>();
            DamageZone damage = root.AddComponent<DamageZone>();
            GameplayTag tag = ScriptableObject.CreateInstance<GameplayTag>();
            typeof(GameplayTag).GetField("path", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(tag, "Tests.ZoneTarget");
            try
            {
                using ObjectConfigurationSession session = Open();
                SettingDefinition layers = target.Fields.Single(field => field.Path == "filter.layers").Definition;
                SettingDefinition requireAny = target.Fields.Single(field => field.Path == "filter.requireAny").Definition;
                SettingDefinition amount = target.Fields.Single(field => field.Path == "valueChanges[0].amount").Definition;
                SettingDefinition operation = target.Fields.Single(field => field.Path == "valueChanges[0].operation").Definition;
                SettingDefinition valueTag = target.Fields.Single(field => field.Path == "valueChanges[0].valueTag").Definition;
                Assert.IsTrue(session.TrySet(layers, 1 << observer.layer));
                Assert.AreEqual(1 << observer.layer, zone.Filter.Layers.value);
                Assert.IsTrue(session.TrySet(requireAny, "Tests.ZoneTarget"));
                Assert.IsFalse(zone.Filter.Allows(observer.AddComponent<BoxCollider>()));
                Assert.AreEqual("Tests.ZoneTarget", session.Get(requireAny));
                Assert.IsFalse(session.TrySet(requireAny, "Tests.DoesNotExist"));
                Assert.AreEqual("Tests.ZoneTarget", session.Get(requireAny));
                Assert.IsTrue(session.Reset(requireAny));
                Assert.IsTrue(zone.Filter.Allows(observer.GetComponent<BoxCollider>()));
                Assert.IsTrue(session.TrySet(valueTag, "Tests.ZoneTarget"));
                Assert.AreSame(tag, damage.ValueChange.ValueTag);
                Assert.IsTrue(session.TrySet(operation, "Set"));
                Assert.AreEqual(TaggedValueOperation.Set, damage.ValueChange.Operation);
                Assert.IsTrue(session.TrySet(amount, -25f));
                Assert.AreEqual(-25f, damage.ValueChange.Amount);
                Assert.IsTrue(session.Undo());
                Assert.AreEqual(-10f, damage.ValueChange.Amount);
                Assert.IsTrue(session.TrySet(amount, -30f));
                Assert.IsTrue(session.Reset(amount));
                Assert.AreEqual(-10f, damage.ValueChange.Amount);
            }
            finally { Object.DestroyImmediate(tag); }
        }

        [Test]
        public void RemovedDamageListEntryRejectsEditsWithoutBreakingRefresh()
        {
            root.AddComponent<BoxCollider>();
            root.AddComponent<ColliderZone>();
            DamageZone damage = root.AddComponent<DamageZone>();
            using ObjectConfigurationSession session = Open();
            SettingDefinition amount = target.Fields.Single(field => field.Path == "valueChanges[0].amount").Definition;
            using SerializedObject serialized = new SerializedObject(damage);
            serialized.FindProperty("valueChanges").ClearArray();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.DoesNotThrow(session.Refresh);
            Assert.IsFalse(session.TrySet(amount, -12f));
        }

        [Test]
        public void ZoneTargetingIgnoresUnmarkedTriggersButRespectsWallsAndIgnoredHierarchy()
        {
            root.transform.position = Vector3.forward * 5f;
            root.AddComponent<BoxCollider>().isTrigger = true;
            root.AddComponent<ColliderZone>();
            GameObject foreground = new GameObject("Foreground");
            foreground.transform.SetParent(observer.transform);
            foreground.transform.position = Vector3.forward * 2f;
            BoxCollider collider = foreground.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            Physics.SyncTransforms();
            Ray ray = new Ray(Vector3.zero, Vector3.forward);
            Assert.AreSame(target, ConfigurableObject.RaycastTarget(ray, 10f));
            collider.isTrigger = false;
            Physics.SyncTransforms();
            Assert.IsNull(ConfigurableObject.RaycastTarget(ray, 10f));
            Assert.AreSame(target, ConfigurableObject.RaycastTarget(ray, 10f, observer.transform));
            Assert.IsNull(ConfigurableObject.RaycastTarget(ray, 3f, observer.transform));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TriggerAndSolidZonesAreDetectedAndExposeSupportedBehaviorFields(bool trigger)
        {
            root.transform.position = Vector3.forward * 5f;
            root.AddComponent<BoxCollider>().isTrigger = trigger;
            ColliderZone zone = root.AddComponent<ColliderZone>();
            Physics.SyncTransforms();
            Assert.AreSame(target, ConfigurableObject.RaycastTarget(new Ray(Vector3.zero, Vector3.forward), 10f));
            using ObjectConfigurationSession session = Open();
            Assert.IsTrue(session.TrySet(Definition("showGizmos"), false));
            Assert.IsFalse(zone.ShowGizmos);
            Assert.IsTrue(session.Undo());
            Assert.IsTrue(zone.ShowGizmos);
        }

        [Test]
        public void CompositeValuesSupportEditingSerializationUndoAndReset()
        {
            string[] names = { "Position2", "Position3", "Position4", "Rotation", "Tint" };
            object[] edits = { new Vector2(-2f, 5f), new Vector3(4f, 5f, 6f),
                new Vector4(5f, 6f, 7f, 8f), Quaternion.Euler(10f, 20f, 30f), new Color(2f, .3f, .4f, .5f) };
            using ObjectConfigurationSession session = Open();
            for (int index = 0; index < names.Length; index++)
            {
                SettingDefinition definition = Definition(names[index]);
                object baseline = session.Get(definition);
                Assert.IsTrue(session.TrySet(definition, edits[index]), names[index]);
                Assert.AreEqual(edits[index], session.Get(definition));
                Assert.IsTrue(definition.TryDeserializeValue(definition.SerializeValue(session.Get(definition)), out object decoded));
                Assert.AreEqual(edits[index], decoded);
                Assert.IsTrue(session.Undo());
                Assert.AreEqual(baseline, session.Get(definition));
                Assert.IsTrue(session.TrySet(definition, edits[index]));
                Assert.IsTrue(session.Reset(definition));
                Assert.AreEqual(baseline, session.Get(definition));
            }
            Assert.IsFalse(session.TrySet(Definition("Position2"), new Vector2(float.NaN, 0f)));
            Assert.IsFalse(session.TrySet(Definition("Position3"), new Vector3(0f, float.PositiveInfinity, 0f)));
            Assert.IsFalse(session.TrySet(Definition("Position4"), new Vector4(0f, 0f, 0f, float.NaN)));
            Assert.IsFalse(session.TrySet(Definition("Rotation"), new Quaternion(0f, 0f, float.NaN, 1f)));
            Assert.IsFalse(session.TrySet(Definition("Tint"), new Color(0f, 0f, 0f, float.NegativeInfinity)));
            Assert.IsFalse(session.TrySet(Definition("Position3"), Vector2.one));
        }

        [Test]
        public void InvalidCompositeDefaultsAreReportedWithoutBreakingDiscovery()
        {
            values.Position2 = new Vector2(float.NaN, 0f);
            values.Position3 = new Vector3(0f, float.PositiveInfinity, 0f);
            values.Position4 = new Vector4(0f, 0f, 0f, float.NaN);
            values.Rotation = new Quaternion(float.NaN, 0f, 0f, 1f);
            values.Tint = new Color(0f, 0f, 0f, float.NaN);
            Assert.AreEqual(5, target.Fields.Count);
            Assert.AreEqual(6, target.UnsupportedFields.Count);
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
            Assert.AreEqual(10, target.Fields.Count);
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
