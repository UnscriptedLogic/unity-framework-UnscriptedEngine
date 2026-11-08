using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnscriptedEngine.UI;
using UnscriptedEngine.UI.Editor;
using Object = UnityEngine.Object;

namespace UnscriptedEngine.Tests
{
    public sealed class ChestCreationTests
    {
        private string folder;

        [TearDown]
        public void Cleanup()
        {
            if (!string.IsNullOrEmpty(folder)) AssetDatabase.DeleteAsset(folder);
            folder = null;
        }

        private ChestCreationPlan Plan(ChestOpenMode mode)
        {
            ChestCreationPlan plan = new ChestCreationPlan
            {
                name = "Test_" + Guid.NewGuid().ToString("N"),
                prototype = "ChestCreatorTests_" + Guid.NewGuid().ToString("N"), mode = mode, slots = 12
            };
            folder = "Assets/Prototypes/" + plan.prototype;
            string parent = folder + "/Chests";
            if (!AssetDatabase.IsValidFolder("Assets/Prototypes")) AssetDatabase.CreateFolder("Assets", "Prototypes");
            AssetDatabase.CreateFolder("Assets/Prototypes", plan.prototype);
            AssetDatabase.CreateFolder(folder, "Chests");
            AssetDatabase.CreateFolder(parent, plan.ClassName);
            return plan;
        }

        [Test]
        public void BurstPrefabWiresInventoryAndSpawnOrigin()
        {
            ChestCreationPlan plan = Plan(ChestOpenMode.BurstOpen);
            GameObject prefab = ChestCreation.BuildPrefab(plan, typeof(UChest));
            UChest chest = prefab.GetComponent<UChest>();
            Assert.That(chest.OpenMode, Is.EqualTo(ChestOpenMode.BurstOpen));
            Assert.That(chest.Inventory.InventorySize, Is.EqualTo(12));
            Assert.That(prefab.GetComponent<UInteractableComponent>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<InventoryLootRelease>(), Is.Not.Null);
            Assert.That(prefab.GetComponentInChildren<InventoryTransferPanel>(true), Is.Null);
            SerializedObject release = new SerializedObject(prefab.GetComponent<InventoryLootRelease>());
            Assert.That(release.FindProperty("inventory").objectReferenceValue, Is.EqualTo(chest.Inventory));
            Assert.That(release.FindProperty("origin").objectReferenceValue, Is.Not.Null);
        }

        [Test]
        public void SplitPrefabOpensTwoViewsForInteractorInventory()
        {
            ChestCreationPlan plan = Plan(ChestOpenMode.SplitInventory);
            GameObject prefab = ChestCreation.BuildPrefab(plan, typeof(UChest));
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            GameObject player = new GameObject("Chest test player");
            player.AddComponent<Unity.Netcode.NetworkObject>();
            UInventoryComponent inventory = player.AddComponent<UInventoryComponent>();
            inventory.Initialize();
            try
            {
                InventoryTransferPanel panel = instance.GetComponentInChildren<InventoryTransferPanel>(true);
                Assert.That(panel, Is.Not.Null);
                Assert.That(panel.gameObject.activeSelf, Is.False);
                // Edit Mode does not invoke ordinary runtime OnEnable methods: subscribe explicitly.
                ChestInventoryPresenter presenter = instance.GetComponent<ChestInventoryPresenter>();
                typeof(ChestInventoryPresenter).GetMethod("OnEnable", System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.NonPublic).Invoke(presenter, null);
                Assert.That(instance.GetComponent<UChest>().TryOpen(player), Is.True);
                Assert.That(panel.Displays(instance.GetComponent<UChest>().Inventory), Is.True);
                Assert.That(panel.GetComponentsInChildren<InventoryView>().Length, Is.EqualTo(2));
                Assert.That(panel.GetComponentsInChildren<InventorySlotDragHandler>().Length, Is.EqualTo(22));
                panel.Close();
                Assert.That(panel.gameObject.activeSelf, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(instance);
                Object.DestroyImmediate(player);
            }
        }

        [TestCase("../Unsafe")]
        [TestCase("Editor")]
        [TestCase("CON")]
        public void InvalidPrototypeCannotEscapeRuntimeOutput(string prototype)
        {
            ChestCreationPlan plan = new ChestCreationPlan { prototype = prototype };
            Assert.Throws<ArgumentException>(() => plan.Validate());
        }

        [Test]
        public void ExistingOutputIsRejected()
        {
            ChestCreationPlan plan = Plan(ChestOpenMode.BurstOpen);
            Assert.Throws<System.IO.IOException>(() => plan.ValidateNew());
        }

        [TestCase(ChestContentsMode.Predefined)]
        [TestCase(ChestContentsMode.Chance)]
        [TestCase(ChestContentsMode.Custom)]
        public void ContentReferencesSurvivePlanSerialization(ChestContentsMode mode)
        {
            ChestCreationPlan plan = Plan(ChestOpenMode.BurstOpen);
            ItemDataSO item = ScriptableObject.CreateInstance<ItemDataSO>();
            ChestLootTable table = ScriptableObject.CreateInstance<ChestLootTable>();
            string itemPath = folder + "/TestItem.asset";
            string tablePath = folder + "/TestTable.asset";
            AssetDatabase.CreateAsset(item, itemPath);
            AssetDatabase.CreateAsset(table, tablePath);
            plan.contentsMode = mode;
            plan.contents.Add(new ChestCreationItem { itemGuid = AssetDatabase.AssetPathToGUID(itemPath), quantity = 3 });
            plan.lootTableGuid = AssetDatabase.AssetPathToGUID(tablePath);
            plan.customProviderGuid = plan.lootTableGuid;
            ChestCreationPlan restored = JsonUtility.FromJson<ChestCreationPlan>(JsonUtility.ToJson(plan));
            restored.Validate();
            GameObject prefab = ChestCreation.BuildPrefab(restored, typeof(UChest));
            SerializedObject settings = new SerializedObject(prefab.GetComponent<UChest>());
            Assert.That(settings.FindProperty("contentsMode").enumValueIndex, Is.EqualTo((int)mode));
            SerializedProperty first = settings.FindProperty("predefinedItems").GetArrayElementAtIndex(0);
            Assert.That(first.FindPropertyRelative("item").objectReferenceValue, Is.EqualTo(item));
            Assert.That(first.FindPropertyRelative("quantity").intValue, Is.EqualTo(3));
            Assert.That(settings.FindProperty(mode == ChestContentsMode.Custom ? "customProvider" : "lootTable").objectReferenceValue, Is.EqualTo(table));
        }
    }
}
