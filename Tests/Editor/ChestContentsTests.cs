using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace UnscriptedEngine.Tests
{
    public sealed class ChestContentsTests
    {
        public sealed class CountingProvider : ChestLootProvider
        {
            private ItemDataSO item;
            private int calls;
            private bool reentryRejected;
            public int Calls => calls;
            public bool ReentryRejected => reentryRejected;
            public void SetItem(ItemDataSO value) => item = value;
            public override void Generate(UChest chest, GameObject interactor, List<InventorySlot> output)
            {
                calls++;
                reentryRejected = !chest.TryInitializeContents(interactor, out _);
                output.Add(new InventorySlot(item, 2));
            }
        }

        private GameObject host;
        private UChest chest;
        private ItemDataSO item;
        private ItemDataSO other;
        private ChestLootTable table;
        private CountingProvider provider;
        private Random.State randomState;

        [SetUp]
        public void Setup()
        {
            randomState = Random.state;
            host = new GameObject("Chest contents test");
            host.AddComponent<Unity.Netcode.NetworkObject>();
            chest = host.AddComponent<UChest>();
            item = ScriptableObject.CreateInstance<ItemDataSO>();
            other = ScriptableObject.CreateInstance<ItemDataSO>();
            table = ScriptableObject.CreateInstance<ChestLootTable>();
            provider = ScriptableObject.CreateInstance<CountingProvider>();
            provider.SetItem(item);
        }

        [TearDown]
        public void Cleanup()
        {
            Random.state = randomState;
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(item);
            Object.DestroyImmediate(other);
            Object.DestroyImmediate(table);
            Object.DestroyImmediate(provider);
        }

        private void Configure(ChestContentsMode mode, Object recipe = null)
        {
            SerializedObject settings = new SerializedObject(chest);
            settings.FindProperty("contentsMode").enumValueIndex = (int)mode;
            if (mode == ChestContentsMode.Chance) settings.FindProperty("lootTable").objectReferenceValue = recipe;
            if (mode == ChestContentsMode.Custom) settings.FindProperty("customProvider").objectReferenceValue = recipe;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        [Test]
        public void PredefinedContentsCombineDuplicatesAndDoNotRefill()
        {
            SerializedObject settings = new SerializedObject(chest);
            SerializedProperty entries = settings.FindProperty("predefinedItems");
            entries.arraySize = 2;
            for (int index = 0; index < 2; index++)
            {
                entries.GetArrayElementAtIndex(index).FindPropertyRelative("item").objectReferenceValue = item;
                entries.GetArrayElementAtIndex(index).FindPropertyRelative("quantity").intValue = 1;
            }
            settings.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(chest.TryInitializeContents(null, out string error), Is.True, error);
            Assert.That(chest.Inventory.Items.Count, Is.EqualTo(2));
            chest.Inventory.TryRemoveItem(0);
            chest.Inventory.TryRemoveItem(1);
            Assert.That(chest.TryInitializeContents(null, out error), Is.True, error);
            Assert.That(chest.Inventory.Items.Count, Is.Zero);
        }

        [Test]
        public void CapacityRetryUsesCachedCustomResultAndRejectsReentry()
        {
            Configure(ChestContentsMode.Custom, provider);
            SerializedObject settings = new SerializedObject(chest.Inventory);
            settings.FindProperty("inventorySize").intValue = 2;
            settings.ApplyModifiedPropertiesWithoutUndo();
            chest.Inventory.Initialize();
            chest.Inventory.TryAddItem(other);
            Assert.That(chest.TryInitializeContents(null, out _), Is.False);
            Assert.That(chest.Inventory.Items.Count, Is.EqualTo(1));
            chest.Inventory.TryRemoveItem(0);
            Assert.That(chest.TryInitializeContents(null, out string error), Is.True, error);
            Assert.That(provider.Calls, Is.EqualTo(1));
            Assert.That(provider.ReentryRejected, Is.True);
            Assert.That(chest.Inventory.Items.Count, Is.EqualTo(2));
        }

        [Test]
        public void BatchFailureLeavesContentsAndNotificationsUnchanged()
        {
            chest.Inventory.Initialize();
            int changes = 0;
            chest.Inventory.Changed += () => changes++;
            UInventoryComponent.OperationResult result = chest.Inventory.TryAddItems(new[]
            {
                new InventorySlot(item, 1), new InventorySlot(other, 100)
            });
            Assert.That(result.Success, Is.False);
            Assert.That(chest.Inventory.Items.Count, Is.Zero);
            Assert.That(changes, Is.Zero);
        }

        [Test]
        public void SuccessfulBatchPublishesOneChange()
        {
            chest.Inventory.Initialize();
            int changes = 0;
            chest.Inventory.Changed += () => changes++;
            Assert.That(chest.Inventory.TryAddItems(new[] { new InventorySlot(item, 1), new InventorySlot(other, 1) }).Success, Is.True);
            Assert.That(chest.Inventory.Items.Count, Is.EqualTo(2));
            Assert.That(changes, Is.EqualTo(1));
        }

        [Test]
        public void ChanceZeroNeverDropsAndChanceHundredUsesInclusiveQuantity()
        {
            SerializedObject settings = new SerializedObject(table);
            SerializedProperty entries = settings.FindProperty("entries");
            entries.arraySize = 2;
            for (int index = 0; index < 2; index++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("item").objectReferenceValue = index == 0 ? item : other;
                entry.FindPropertyRelative("chancePercent").floatValue = index == 0 ? 0f : 100f;
                entry.FindPropertyRelative("minimumQuantity").intValue = 3;
                entry.FindPropertyRelative("maximumQuantity").intValue = 3;
            }
            settings.ApplyModifiedPropertiesWithoutUndo();
            Configure(ChestContentsMode.Chance, table);
            Assert.That(chest.TryInitializeContents(null, out string error), Is.True, error);
            Assert.That(chest.Inventory.HasItem(item), Is.False);
            Assert.That(chest.Inventory.Items.Count, Is.EqualTo(3));
            Assert.That(chest.Inventory.Items[0].Item, Is.SameAs(other));
            Assert.That(chest.TryInitializeContents(null, out error), Is.True, error);
            Assert.That(chest.Inventory.Items.Count, Is.EqualTo(3));
        }

        [Test]
        public void InvalidCustomOutputCannotPartiallyPopulateOrRegenerate()
        {
            provider.SetItem(null);
            Configure(ChestContentsMode.Custom, provider);
            Assert.That(chest.TryInitializeContents(null, out _), Is.False);
            Assert.That(chest.TryInitializeContents(null, out _), Is.False);
            Assert.That(provider.Calls, Is.EqualTo(1));
            Assert.That(chest.Inventory.Items.Count, Is.Zero);
            Assert.That(chest.ContentsInitialized, Is.False);
        }
    }
}
