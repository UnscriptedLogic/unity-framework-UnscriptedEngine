using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace UnscriptedEngine.Tests
{
    public class InventoryTests
    {
        private GameObject host;
        private UInventoryComponent inventory;
        private ItemDataSO item;
        private int changes;
        [SetUp] public void Setup()
        {
            host = new GameObject("Inventory test");
            inventory = host.AddComponent<UInventoryComponent>();
            inventory.SetLoggingEnabled(false);
            typeof(UInventoryComponent).GetField("inventorySize", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(inventory, 2);
            item = ScriptableObject.CreateInstance<ItemDataSO>();
            var stack = new UItemFragStack();
            typeof(UItemFragStack).GetField("_stackSize", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(stack, 3);
            item.fragments.Add(stack);
            inventory.Initialize();
            changes = 0;
            inventory.Changed += Changed;
        }
        private void Changed() => changes++;
        [TearDown] public void Cleanup()
        {
            inventory.Changed -= Changed;
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(item);
        }
        [Test] public void AddSpillsAcrossStacks()
        {
            Assert.That(inventory.TryAddItem(item, 5).Success, Is.True);
            Assert.That(inventory.Items[0].Quantity, Is.EqualTo(3));
            Assert.That(inventory.Items[1].Quantity, Is.EqualTo(2));
            Assert.That(changes, Is.EqualTo(1));
        }
        [Test] public void RepeatedInitializationPreservesContentsAndDoesNotNotify()
        {
            inventory.TryAddItem(item, 2); changes = 0;
            inventory.Initialize();
            Assert.That(inventory.Items[0].Quantity, Is.EqualTo(2));
            Assert.That(changes, Is.Zero);
        }
        [Test] public void ChangeNotificationCannotMutateCommittedInventory()
        {
            UInventoryComponent.OperationResult nested = default;
            int attempts = 0;
            inventory.Changed += () => { attempts++; nested = inventory.TryRemoveItem(0); };
            Assert.That(inventory.TryAddItem(item, 2).Success, Is.True);
            Assert.That(attempts, Is.EqualTo(1), "The nested mutation must actually be attempted.");
            Assert.That(nested.Success, Is.False);
            Assert.That(inventory.Items[0].Quantity, Is.EqualTo(2));
        }
        [Test] public void ThrowingNotificationDoesNotPermanentlyLockInventory()
        {
            System.Action throwing = () => throw new System.InvalidOperationException("subscriber failed");
            inventory.Changed += throwing;
            int laterSubscriberCalls = 0;
            inventory.Changed += () => laterSubscriberCalls++;
            UnityEngine.TestTools.LogAssert.Expect(LogType.Exception,
                new System.Text.RegularExpressions.Regex("InvalidOperationException: subscriber failed"));
            Assert.That(inventory.TryAddItem(item).Success, Is.True);
            Assert.That(laterSubscriberCalls, Is.EqualTo(1));
            inventory.Changed -= throwing;
            Assert.That(inventory.TryRemoveItem(0).Success, Is.True);
        }
        [Test] public void AddFillsExistingStackFirst()
        {
            inventory.TryAddItem(item, 2); inventory.TryAddItem(item, 2);
            Assert.That(inventory.Items[0].Quantity, Is.EqualTo(3));
            Assert.That(inventory.Items[1].Quantity, Is.EqualTo(1));
        }
        [Test] public void CapacityFailureIsAtomic()
        {
            inventory.TryAddItem(item, 2); changes = 0;
            Assert.That(inventory.TryAddItem(item, 5).Success, Is.False);
            Assert.That(inventory.Items.Count, Is.EqualTo(1));
            Assert.That(inventory.Items[0].Quantity, Is.EqualTo(2));
            Assert.That(changes, Is.Zero);
        }
        [TestCase(0)] [TestCase(-1)] public void InvalidAddQuantityDoesNotMutate(int quantity)
        {
            Assert.That(inventory.TryAddItem(item, quantity).Success, Is.False);
            Assert.That(inventory.Items, Is.Empty); Assert.That(changes, Is.Zero);
        }
        [Test] public void NullItemRejected() => Assert.That(inventory.TryAddItem(null).Success, Is.False);
        [Test] public void CanAddDoesNotMutateOrNotify()
        {
            Assert.That(inventory.CanAddItem(item, 4).Success, Is.True);
            Assert.That(inventory.Items, Is.Empty); Assert.That(changes, Is.Zero);
        }
        [Test] public void RemovePreservesPreviousSnapshot()
        {
            inventory.TryAddItem(item, 3); var snapshot = inventory.Items[0];
            Assert.That(inventory.TryRemoveItem(0).Success, Is.True);
            Assert.That(snapshot.Quantity, Is.EqualTo(3));
            Assert.That(inventory.Items[0].Quantity, Is.EqualTo(2));
        }
        [Test] public void RemoveFinalItemFreesSlot()
        {
            inventory.TryAddItem(item); inventory.TryRemoveItem(0);
            Assert.That(inventory.HasEmptySlot(out var index), Is.True);
            Assert.That(index, Is.Zero); Assert.That(inventory.HasItem(item), Is.False);
        }
        [TestCase(-1, 1)] [TestCase(2, 1)] [TestCase(0, 0)] [TestCase(0, 4)]
        public void InvalidRemovalIsAtomic(int index, int quantity)
        {
            inventory.TryAddItem(item, 3); changes = 0;
            Assert.That(inventory.TryRemoveItem(index, quantity).Success, Is.False);
            Assert.That(inventory.Items[0].Quantity, Is.EqualTo(3)); Assert.That(changes, Is.Zero);
        }
        [Test] public void SplitConservesQuantity()
        {
            inventory.TryAddItem(item, 3);
            var result = inventory.TrySplitStack(0, 1);
            Assert.That(result.Success, Is.True); Assert.That(result.SlotIndex, Is.EqualTo(1));
            Assert.That(inventory.Items[0].Quantity, Is.EqualTo(2));
            Assert.That(inventory.Items[1].Quantity, Is.EqualTo(1));
        }
        [Test] public void SplitWholeStackRejected()
        {
            inventory.TryAddItem(item, 3);
            Assert.That(inventory.TrySplitStack(0, 3).Success, Is.False);
            Assert.That(inventory.Items.Count, Is.EqualTo(1));
        }
        [Test] public void SingularItemRejectsSecondAddAndSplit()
        {
            item.fragments.Add(new UItemFragForceSingular());
            Assert.That(inventory.TryAddItem(item, 2).Success, Is.True);
            Assert.That(inventory.TryAddItem(item).Success, Is.False);
            Assert.That(inventory.TrySplitStack(0, 1).Success, Is.False);
            Assert.That(inventory.Items[0].Quantity, Is.EqualTo(2));
        }
        [Test] public void ItemWithoutStackFragmentUsesOnePerSlot()
        {
            item.fragments.Clear(); inventory.TryAddItem(item, 2);
            Assert.That(inventory.Items.Count, Is.EqualTo(2));
            Assert.That(inventory.Items[0].Quantity, Is.EqualTo(1));
        }
    }
}
