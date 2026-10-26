using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnscriptedEngine.Editor.Scaffolding;
using UnscriptedEngine.Item;
using Object = UnityEngine.Object;

namespace UnscriptedEngine.Tests
{
    public class ItemScaffoldTests
    {
        private string folder;
        private ItemScaffoldPlan Plan() => new ItemScaffoldPlan
        {
            folder = folder, name = "ScaffoldProbe", displayName = "Test Item", stackSize = 4, pickupQuantity = 2
        };

        [SetUp]
        public void Setup()
        {
            folder = "Assets/ItemScaffoldTests_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
        }

        [TearDown] public void Cleanup() => AssetDatabase.DeleteAsset(folder);

        [Test]
        public void CreatesExactFolderLayoutAndReusesFolders()
        {
            var plan = Plan();
            plan.EnsureFolders();
            string guid = AssetDatabase.AssetPathToGUID(plan.ScriptFolder);
            plan.EnsureFolders();
            Assert.That(AssetDatabase.AssetPathToGUID(plan.ScriptFolder), Is.EqualTo(guid));
            Assert.That(plan.ScriptFolder, Is.EqualTo(folder + "/Scripts/Items/ScaffoldProbe"));
            Assert.That(plan.DataPath, Is.EqualTo(folder + "/Data/Items/ScaffoldProbe.asset"));
            Assert.That(plan.PrefabFolder, Is.EqualTo(folder + "/Prefabs/Items/ScaffoldProbe"));
            Assert.That(plan.OutputPaths.Count(), Is.EqualTo(7));
        }

        [TestCase("../Escape")]
        [TestCase("Bad Name")]
        [TestCase("1Item")]
        [TestCase("CON")]
        [TestCase("Editor")]
        [TestCase("StreamingAssets")]
        public void InvalidNamesFailBeforeWriting(string name)
        {
            var plan = Plan(); plan.name = name;
            Assert.Throws<ArgumentException>(() => plan.ValidateNew());
            Assert.That(Directory.GetFiles(folder).Length, Is.Zero);
        }

        [TestCase("Assets/../Outside")]
        [TestCase("Packages")]
        [TestCase("Assets\\Wrong")]
        public void InvalidRootsFail(string root)
        {
            var plan = Plan(); plan.folder = root;
            Assert.Throws<ArgumentException>(() => plan.Validate());
        }

        [Test]
        public void ExistingOutputAndOrphanMetadataAreProtected()
        {
            var plan = Plan(); plan.EnsureFolders();
            File.WriteAllText(plan.DataPath + ".meta", "preserve me");
            Assert.Throws<IOException>(() => plan.ValidateNew());
            Assert.That(File.ReadAllText(plan.DataPath + ".meta"), Is.EqualTo("preserve me"));
            Assert.That(File.Exists(plan.ScriptPath("Behaviour")), Is.False);
        }

        [Test]
        public void GeneratedSourcesMatchFilesAndKeepDisplayTextOutOfCode()
        {
            var plan = Plan(); plan.displayName = "\"; injected text";
            var sources = ItemScaffoldSources.Create(plan);
            foreach (string suffix in ItemScaffoldPlan.ScriptSuffixes)
            {
                string source = sources[plan.ScriptPath(suffix)];
                StringAssert.Contains("class " + plan.name + suffix, source);
                StringAssert.DoesNotContain(plan.displayName, source);
            }
            var restored = JsonUtility.FromJson<ItemScaffoldPlan>(JsonUtility.ToJson(plan));
            CollectionAssert.AreEqual(plan.OutputPaths, restored.OutputPaths);
            Assert.That(restored.pickupQuantity, Is.EqualTo(2));
        }

        [TestCase(WorldItemInteraction.None)]
        [TestCase(WorldItemInteraction.PickUp)]
        public void AssetsAreWiredAndActiveSceneIsUntouched(WorldItemInteraction defaultBehaviour)
        {
            var plan = Plan();
            plan.defaultBehaviour = defaultBehaviour;
            plan = JsonUtility.FromJson<ItemScaffoldPlan>(JsonUtility.ToJson(plan));
            var scene = SceneManager.GetActiveScene();
            int rootCount = scene.rootCount;
            bool wasDirty = scene.isDirty;
            var data = ItemScaffoldAssets.Build(plan, typeof(TestItemVisual), typeof(UWorldItem), typeof(TestFragment));
            Assert.That(data.fragments.OfType<UItemFragDetails>().Single().Name, Is.EqualTo("Test Item"));
            Assert.That(data.fragments.OfType<UItemFragStack>().Single().StackSize, Is.EqualTo(4));
            Assert.That(data.fragments.OfType<TestFragment>().Count(), Is.EqualTo(1));
            var held = AssetDatabase.LoadAssetAtPath<GameObject>(plan.HeldPath);
            var world = AssetDatabase.LoadAssetAtPath<GameObject>(plan.WorldPath);
            Assert.That(data.fragments.OfType<UItemFragEquip>().Single().VisualPrefab, Is.EqualTo(held));
            Assert.That(held.GetComponent<TestItemVisual>(), Is.Not.Null);
            Assert.That(held.GetComponentInChildren<Collider>(), Is.Null);
            Assert.That(held.GetComponent<NetworkObject>(), Is.Null);
            Assert.That(world.GetComponent<NetworkObject>(), Is.Not.Null);
            Assert.That(world.GetComponent<BoxCollider>(), Is.Not.Null);
            Assert.That(world.GetComponent<UInteractableComponent>(), Is.Not.Null);
            Assert.That(world.GetComponent<UWorldItem>().ItemData, Is.EqualTo(data));
            Assert.That(world.GetComponent<UWorldItem>().Quantity, Is.EqualTo(2));
            Assert.That(world.GetComponent<UWorldItem>().DefaultBehaviour, Is.EqualTo(defaultBehaviour));
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(scene));
            Assert.That(scene.rootCount, Is.EqualTo(rootCount));
            Assert.That(scene.isDirty, Is.EqualTo(wasDirty));
            byte[] original = File.ReadAllBytes(plan.DataPath);
            Assert.Throws<IOException>(() => ItemScaffoldAssets.Build(plan, typeof(TestItemVisual), typeof(UWorldItem), typeof(TestFragment)));
            CollectionAssert.AreEqual(original, File.ReadAllBytes(plan.DataPath));
        }

        [Test]
        public void FailedAssetBuildRollsBackItsOutputs()
        {
            var plan = Plan();
            Assert.Throws<System.Reflection.TargetInvocationException>(() =>
                ItemScaffoldAssets.Build(plan, typeof(TestItemVisual), typeof(UWorldItem), typeof(ThrowingFragment)));
            foreach (string path in plan.AssetPaths)
            {
                Assert.That(File.Exists(path), Is.False, path);
                Assert.That(File.Exists(path + ".meta"), Is.False, path + ".meta");
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ModelOverrideIsSharedByBothPrefabsAndSurvivesSerialization(bool importedModel)
        {
            GameObject artwork;

            if (importedModel)
            {
                string path = folder + "/Artwork.obj";
                File.WriteAllText(path, "o Artwork\nv -1 -2 -3\nv 1 -2 -3\nv 1 2 -3\nv -1 2 -3\n"
                    + "v -1 -2 3\nv 1 -2 3\nv 1 2 3\nv -1 2 3\n"
                    + "f 1 2 3 4\nf 5 8 7 6\nf 1 5 6 2\nf 4 3 7 8\nf 1 4 8 5\nf 2 6 7 3\n");
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                artwork = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            else
            {
                GameObject temporary = GameObject.CreatePrimitive(PrimitiveType.Cube);
                try
                {
                    temporary.transform.localPosition = new Vector3(0.3f, -0.2f, 0.1f);
                    temporary.transform.localRotation = Quaternion.Euler(0f, 25f, 0f);
                    temporary.transform.localScale = new Vector3(1.5f, 2f, 0.5f);
                    temporary.AddComponent<Rigidbody>();
                    artwork = PrefabUtility.SaveAsPrefabAsset(temporary, folder + "/Artwork.prefab");
                }
                finally { Object.DestroyImmediate(temporary); }
            }

            Assert.That(artwork, Is.Not.Null);
            ItemScaffoldPlan plan = Plan();
            plan.modelGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(artwork));
            plan = JsonUtility.FromJson<ItemScaffoldPlan>(JsonUtility.ToJson(plan));

            Assert.That(plan.ResolveModel(), Is.EqualTo(artwork));
            ItemScaffoldAssets.Build(plan, typeof(TestItemVisual), typeof(UWorldItem), typeof(TestFragment));

            foreach (string path in new[] { plan.HeldPath, plan.WorldPath })
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(prefab.transform.childCount, Is.EqualTo(1));
                GameObject model = prefab.transform.GetChild(0).gameObject;
                Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(model), Is.EqualTo(artwork));
                Assert.That(model.transform.localPosition, Is.EqualTo(artwork.transform.localPosition));
                Assert.That(model.transform.localRotation, Is.EqualTo(artwork.transform.localRotation));
                Assert.That(model.transform.localScale, Is.EqualTo(artwork.transform.localScale));
                Assert.That(model.GetComponentInChildren<MeshFilter>().sharedMesh,
                    Is.EqualTo(artwork.GetComponentInChildren<MeshFilter>().sharedMesh));

                foreach (Collider collider in model.GetComponentsInChildren<Collider>(true))
                    Assert.That(collider.enabled, Is.False);

                foreach (Rigidbody body in model.GetComponentsInChildren<Rigidbody>(true))
                {
                    Assert.That(body.isKinematic, Is.True);
                    Assert.That(body.useGravity, Is.False);
                }
            }

            GameObject world = AssetDatabase.LoadAssetAtPath<GameObject>(plan.WorldPath);
            BoxCollider pickupCollider = world.GetComponent<BoxCollider>();
            Renderer renderer = world.GetComponentInChildren<Renderer>();
            Assert.That(Vector3.Distance(pickupCollider.center, renderer.bounds.center), Is.LessThan(0.001f));
            Assert.That(Vector3.Distance(pickupCollider.size, renderer.bounds.size), Is.LessThan(0.001f));

            if (!importedModel)
            {
                Assert.That(artwork.GetComponent<Collider>().enabled, Is.True);
                Assert.That(artwork.GetComponent<Rigidbody>().isKinematic, Is.False);
                Assert.That(artwork.GetComponent<Rigidbody>().useGravity, Is.True);
            }
        }

        [Test]
        public void MissingModelOverrideCannotSilentlyFallBackToCube()
        {
            ItemScaffoldPlan plan = Plan();
            plan.modelGuid = Guid.NewGuid().ToString("N");

            Assert.Throws<ArgumentException>(() => ItemScaffoldAssets.Build(plan,
                typeof(TestItemVisual), typeof(UWorldItem), typeof(TestFragment)));
            Assert.That(Directory.GetFiles(folder).Length, Is.Zero);
        }

        [TestCase(WorldItemInteraction.None, 10, true)]
        [TestCase(WorldItemInteraction.PickUp, 0, true)]
        [TestCase(WorldItemInteraction.PickUp, 10, false)]
        public void NoActionOrRejectedPickupLeavesWorldItem(WorldItemInteraction behaviour, int capacity, bool hasInventory)
        {
            GameObject world = new GameObject("World item");
            GameObject source = new GameObject("Non-player interactor");
            ItemDataSO data = ScriptableObject.CreateInstance<ItemDataSO>();
            UWorldItem item = world.AddComponent<UWorldItem>();
            UInventoryComponent inventory = null;
            System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;

            try
            {
                SerializedObject serialized = new SerializedObject(item);
                serialized.FindProperty("itemData").objectReferenceValue = data;
                serialized.FindProperty("defaultBehaviour").intValue = (int)behaviour;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                if (hasInventory)
                {
                    inventory = source.AddComponent<UInventoryComponent>();
                    inventory.SetLoggingEnabled(false);
                    SerializedObject serializedInventory = new SerializedObject(inventory);
                    serializedInventory.FindProperty("inventorySize").intValue = capacity;
                    serializedInventory.ApplyModifiedPropertiesWithoutUndo();
                    inventory.Initialize();
                }

                // Explicitly bind the lifecycle hook: non-ExecuteAlways scripts do not enable in EditMode.
                typeof(UWorldItem).GetMethod("OnEnable", flags).Invoke(item, null);
                Assert.That(world.GetComponent<UInteractableComponent>().TryBeginInteraction(source), Is.True);
                Assert.That(world.activeSelf, Is.True);
                if (inventory != null) Assert.That(inventory.Items.Count, Is.Zero);
            }
            finally
            {
                typeof(UWorldItem).GetMethod("OnDisable", flags).Invoke(item, null);
                Object.DestroyImmediate(world);
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(data);
            }
        }

        [Serializable]
        public sealed class TestFragment : UItemFragBehaviour
        {
            public override ItemBehaviour CreateBehaviour(ItemBehaviourContext context) => null;
        }

        [Serializable]
        public sealed class ThrowingFragment : UItemFragBehaviour
        {
            public ThrowingFragment() => throw new InvalidOperationException("test construction failure");
            public override ItemBehaviour CreateBehaviour(ItemBehaviourContext context) => null;
        }
    }
}
