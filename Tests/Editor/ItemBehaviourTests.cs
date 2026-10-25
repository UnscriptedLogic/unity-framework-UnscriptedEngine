using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnscriptedEngine.Item;
using Object = UnityEngine.Object;

namespace UnscriptedEngine.Tests
{
    public class ItemBehaviourTests
    {
        private GameObject host;
        private ItemEquipComponent equipment;
        private UInventoryComponent inventory;
        private ItemDataSO item;
        private ProbeFactory factory;

        [SetUp]
        public void Setup()
        {
            host = new GameObject("Equipment test");
            inventory = host.AddComponent<UInventoryComponent>();
            inventory.SetLoggingEnabled(false);
            inventory.Initialize();
            equipment = host.AddComponent<ItemEquipComponent>();
            item = ScriptableObject.CreateInstance<ItemDataSO>();
            factory = new ProbeFactory();
            item.fragments.Add(factory);
            inventory.TryAddItem(item, 2);
            equipment.SetInventory(inventory);
        }

        [TearDown]
        public void Cleanup()
        {
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(item);
        }

        [Test]
        public void SameDefinitionInDifferentSlotStartsNewSession()
        {
            Probe first = factory.Created[0];
            equipment.SelectSlot(1);
            Assert.That(first.Calls, Is.EqualTo(new[] { "equip", "cancel", "unequip", "dispose" }));
            Assert.That(factory.Created.Count, Is.EqualTo(2));
            Assert.That(factory.Contexts[1].SlotIndex, Is.EqualTo(1));
            Assert.That(factory.Contexts[1].Inventory, Is.SameAs(inventory));
        }

        [Test]
        public void UnrelatedInventoryChangesAndReselectionPreserveSession()
        {
            inventory.TryRemoveItem(1);
            equipment.SelectSlot(0);
            Assert.That(factory.Created.Count, Is.EqualTo(1));
            Assert.That(factory.Created[0].Calls, Is.EqualTo(new[] { "equip" }));
        }

        [Test]
        public void QuantityChangesPreserveSessionUntilStackEmpty()
        {
            var stack = new UItemFragStack();
            typeof(UItemFragStack).GetField("_stackSize",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(stack, 3);
            item.fragments.Add(stack);
            inventory.TryAddItem(item);
            inventory.TryRemoveItem(0);
            Assert.That(factory.Created.Count, Is.EqualTo(1));
            inventory.TryRemoveItem(0);
            Assert.That(factory.Created[0].Calls, Is.EqualTo(new[] { "equip", "cancel", "unequip", "dispose" }));
            Assert.That(equipment.HeldItem, Is.Null);
        }

        [Test]
        public void DisableCleansOnceAndEnableCreatesFreshSession()
        {
            Probe first = factory.Created[0];
            SetEnabled(false);
            equipment.CancelUse();
            Assert.That(first.Calls, Is.EqualTo(new[] { "equip", "cancel", "unequip", "dispose" }));
            SetEnabled(true);
            Assert.That(factory.Created.Count, Is.EqualTo(2));
        }

        [Test]
        public void UseBeforeNetworkGameplayIsRejected()
        {
            Assert.That(equipment.Use("primary", true), Is.False);
            Assert.That(factory.Created[0].Calls, Is.EqualTo(new[] { "equip" }));
        }

        [Test]
        public void CleanupContinuesAfterCallbackFailure()
        {
            factory.Created[0].ThrowOnCancel = true;
            UnityEngine.TestTools.LogAssert.Expect(LogType.Exception,
                new System.Text.RegularExpressions.Regex("InvalidOperationException: cancel failed"));
            SetEnabled(false);
            Assert.That(factory.Created[0].Calls, Is.EqualTo(new[] { "equip", "cancel", "unequip", "dispose" }));
        }

        [Test]
        public void VisualBindingsExistDuringEquipAndUnequipThenAreDestroyed()
        {
            var prefab = new GameObject("Held visual");
            prefab.AddComponent<Light>();
            try
            {
                var equip = new UItemFragEquip();
                typeof(UItemFragEquip).GetField("visualPrefab",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .SetValue(equip, prefab);
                item.fragments.Add(equip);
                equipment.SelectSlot(1);
                var context = factory.Contexts[1];
                Assert.That(context.Visual, Is.Not.SameAs(prefab));
                Assert.That(context.GetVisualComponent<Light>(), Is.Not.Null);
                factory.Created[1].OnUnequip = () => Assert.That(context.Visual.activeSelf, Is.True);
                SetEnabled(false);
                Assert.That(context.Visual == null, Is.True);
                Assert.That(prefab != null, Is.True);
            }
            finally { Object.DestroyImmediate(prefab); }
        }

        [Test]
        public void FailedEquipDisposesPartialSession()
        {
            factory.ThrowOnEquip = true;
            Assert.Throws<InvalidOperationException>(() => equipment.SelectSlot(1));
            Assert.That(factory.Created[1].Calls, Is.EqualTo(new[] { "equip", "cancel", "unequip", "dispose" }));
            Assert.That(equipment.HeldItem, Is.Null);
        }

        private void SetEnabled(bool enabled)
        {
            equipment.enabled = enabled;
            // EditMode does not dispatch these callbacks for non-ExecuteAlways components.
            typeof(ItemEquipComponent).GetMethod(enabled ? "OnEnable" : "OnDisable",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(equipment, null);
        }

        [Serializable]
        private sealed class ProbeFactory : UItemFragBehaviour
        {
            public readonly List<Probe> Created = new();
            public readonly List<ItemBehaviourContext> Contexts = new();
            public bool ThrowOnEquip;
            public override ItemBehaviour CreateBehaviour(ItemBehaviourContext context)
            {
                Contexts.Add(context);
                var probe = new Probe { ThrowOnEquip = ThrowOnEquip };
                Created.Add(probe);
                return probe;
            }
        }

        private sealed class Probe : ItemBehaviour
        {
            public readonly List<string> Calls = new();
            public bool ThrowOnCancel;
            public bool ThrowOnEquip;
            public Action OnUnequip;
            public override void Equip()
            {
                Calls.Add("equip");
                if (ThrowOnEquip) throw new InvalidOperationException("equip failed");
            }
            public override void CancelUse()
            {
                Calls.Add("cancel");
                if (ThrowOnCancel) throw new InvalidOperationException("cancel failed");
            }
            public override void Unequip()
            {
                Calls.Add("unequip");
                OnUnequip?.Invoke();
            }
            public override void Dispose() => Calls.Add("dispose");
        }
    }
}
