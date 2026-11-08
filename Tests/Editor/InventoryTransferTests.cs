using NUnit.Framework;
using UnityEngine;

namespace UnscriptedEngine.Tests
{
    public sealed class InventoryTransferTests
    {
        private GameObject firstHost;
        private GameObject secondHost;
        private UInventoryComponent first;
        private UInventoryComponent second;
        private ItemDataSO item;
        private ItemDataSO other;

        [SetUp]
        public void Setup()
        {
            firstHost = new GameObject("Source");
            secondHost = new GameObject("Destination");
            first = firstHost.AddComponent<UInventoryComponent>();
            second = secondHost.AddComponent<UInventoryComponent>();
            first.Initialize();
            second.Initialize();
            item = ScriptableObject.CreateInstance<ItemDataSO>();
            other = ScriptableObject.CreateInstance<ItemDataSO>();
            first.TryAddItem(item);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(firstHost);
            Object.DestroyImmediate(secondHost);
            Object.DestroyImmediate(item);
            Object.DestroyImmediate(other);
        }

        [Test]
        public void BothInventoriesCommitBeforeNotificationsAndRejectReentry()
        {
            int destinationChanges = 0;
            bool committed = false;
            bool reentryRejected = false;
            first.Changed += () =>
            {
                committed = !first.Items.ContainsKey(0) && second.Items[2].Item == item;
                reentryRejected = !second.TryRemoveItem(2).Success;
            };
            second.Changed += () => destinationChanges++;
            Assert.That(first.TryTransferItem(0, second, 2).Success, Is.True);
            Assert.That(destinationChanges, Is.EqualTo(1));
            Assert.That(committed, Is.True);
            Assert.That(reentryRejected, Is.True);
        }

        [Test]
        public void OccupiedDifferentItemSwapsWholeStacks()
        {
            second.TryAddItem(other);
            Assert.That(first.TryTransferItem(0, second, 0).Success, Is.True);
            Assert.That(first.Items[0].Item, Is.SameAs(other));
            Assert.That(second.Items[0].Item, Is.SameAs(item));
        }

        [Test]
        public void OverflowLeavesBothInventoriesUnchanged()
        {
            second.TryAddItem(item);
            Assert.That(first.TryTransferItem(0, second, 0).Success, Is.False);
            Assert.That(first.Items[0].Quantity, Is.EqualTo(1));
            Assert.That(second.Items[0].Quantity, Is.EqualTo(1));
        }

        [Test]
        public void SameInventoryMovePreservesItemAndRejectsSameSlot()
        {
            Assert.That(first.TryTransferItem(0, first, 0).Success, Is.False);
            Assert.That(first.TryTransferItem(0, first, 3).Success, Is.True);
            Assert.That(first.Items.Count, Is.EqualTo(1));
            Assert.That(first.Items[3].Item, Is.SameAs(item));
        }

        [Test]
        public void PartialMergeConservesQuantityAndRejectsPartialSwap()
        {
            UItemFragStack stack = new UItemFragStack();
            typeof(UItemFragStack).GetField("_stackSize", System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic).SetValue(stack, 5);
            item.fragments.Add(stack);
            first.TryAddItem(item, 3);
            second.TryAddItem(item);
            second.TryAddItem(other);
            Assert.That(first.TryTransferItem(0, second, 1, 2).Success, Is.False);
            Assert.That(first.TryTransferItem(0, second, 0, 2).Success, Is.True);
            Assert.That(first.Items[0].Quantity, Is.EqualTo(2));
            Assert.That(second.Items[0].Quantity, Is.EqualTo(3));
        }

        [Test]
        public void SingularItemCannotBeDuplicatedAcrossSlots()
        {
            item.fragments.Add(new UItemFragForceSingular());
            second.TryAddItem(item);
            Assert.That(first.TryTransferItem(0, second, 1).Success, Is.False);
            Assert.That(first.Items[0].Item, Is.SameAs(item));
            Assert.That(second.Items.Count, Is.EqualTo(1));
        }

        [Test]
        public void BurstCapsPickupsAndPreservesUndroppedContents()
        {
            InventoryLootRelease release = firstHost.AddComponent<InventoryLootRelease>();
            System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic;
            typeof(InventoryLootRelease).GetField("inventory", flags).SetValue(release, first);
            typeof(InventoryLootRelease).GetField("maxPickups", flags).SetValue(release, 1);
            // Missing world prefab must not consume contents.
            Assert.That(release.Release(), Is.Zero);
            Assert.That(first.Items.Count, Is.EqualTo(1));
            GameObject prefabObject = new GameObject("Loot test pickup");
            UnscriptedEngine.Item.UWorldItem prefab = prefabObject.AddComponent<UnscriptedEngine.Item.UWorldItem>();
            prefabObject.AddComponent<Rigidbody>();
            UItemFragDrop drop = new UItemFragDrop();
            typeof(UItemFragDrop).GetField("worldPrefab", flags).SetValue(drop, prefab);
            item.fragments.Add(drop);
            first.TryAddItem(item);
            try
            {
                Assert.That(release.Release(), Is.EqualTo(1));
                Assert.That(first.Items.Count, Is.EqualTo(1));
                foreach (UnscriptedEngine.Item.UWorldItem pickup in Object.FindObjectsByType<UnscriptedEngine.Item.UWorldItem>())
                {
                    if (pickup.ItemData != item) continue;
                    Assert.That(pickup.GetComponent<Rigidbody>().linearVelocity.y, Is.GreaterThan(0f));
                }
            }
            finally
            {
                foreach (UnscriptedEngine.Item.UWorldItem pickup in Object.FindObjectsByType<UnscriptedEngine.Item.UWorldItem>())
                    if (pickup.ItemData == item) Object.DestroyImmediate(pickup.gameObject);
                Object.DestroyImmediate(prefabObject);
            }
        }
    }
}
