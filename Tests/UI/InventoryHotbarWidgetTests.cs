using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnscriptedEngine.UI;
using Object = UnityEngine.Object;

namespace UnscriptedEngine.Tests
{
    public class InventoryHotbarWidgetTests
    {
        private GameObject root, pawn;
        private ItemDataSO item;
        private InventoryHotbarWidget widget;

        private sealed class Inventory : IInventory
        {
            public int InventorySize { get; set; } = 5;
            public Dictionary<int, InventorySlot> Slots = new();
            public IReadOnlyDictionary<int, InventorySlot> Items => Slots;
            private Action changed;
            public int Subscribers => changed?.GetInvocationList().Length ?? 0;
            public event Action Changed { add => changed += value; remove => changed -= value; }
            public void Notify() => changed?.Invoke();
        }

        [UnitySetUp] public IEnumerator Setup()
        {
            yield return new EnterPlayMode();
            root = new GameObject("HUD", typeof(RectTransform));
            item = ScriptableObject.CreateInstance<ItemDataSO>();
            item.fragments.Add(new UItemFragDetails { Name = "Apple" });
            widget = InventoryHotbarWidget.CreateDefault(root.transform);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (root != null) Object.DestroyImmediate(root);
            if (pawn != null) Object.DestroyImmediate(pawn);
            if (item != null) Object.DestroyImmediate(item);
            yield return new ExitPlayMode();
        }
        private string Text(int slot, string name) => widget.transform.Find($"Slot {slot}")
            .GetComponentsInChildren<TMP_Text>(true).Single(label => label.name == name).text;

        [Test] public void CustomSlotPrefabPreservesItsDesignAndCanBeReplaced()
        {
            var inventory = new Inventory();
            widget.Bind(inventory);
            Transform original = widget.transform.Find("Slot 1");
            var template = Object.Instantiate(Resources.Load<GameObject>("UnscriptedEngine/InventoryHotbarSlot"), root.transform);
            template.SetActive(false);
            var custom = template.GetComponent<InventoryHotbarSlotWidget>();
            JsonUtility.FromJsonOverwrite("{\"emptyLabel\":\"Vacant\"}", custom);
            new GameObject("Custom decoration", typeof(RectTransform)).transform.SetParent(template.transform, false);
            widget.SetSlotPrefab(custom);
            var rendered = widget.transform.Find("Slot 1").GetComponent<InventoryHotbarSlotWidget>();
            Assert.That(rendered.transform, Is.Not.SameAs(original));
            Assert.That(rendered.transform.Find("Custom decoration"), Is.Not.Null);
            Assert.That(Text(1, "Name"), Is.EqualTo("Vacant"));
            inventory.Slots[0] = new InventorySlot(item, 4);
            inventory.Notify();
            Assert.That(rendered.Slot, Is.SameAs(inventory.Slots[0]));
            Assert.That(Text(1, "Quantity"), Is.EqualTo("4"));
            widget.SetSlotPrefab(null);
            Assert.That(widget.transform.Find("Slot 1/Custom decoration"), Is.Null);
            Assert.That(widget.VisibleSlotCount, Is.EqualTo(3));
            Assert.That(Text(2, "Name"), Is.EqualTo("Empty"));
        }

        [Test] public void ShowsFirstThreeSlotsWithoutCompactingEmptySlots()
        {
            var inventory = new Inventory();
            inventory.Slots[0] = new InventorySlot(item, 2);
            inventory.Slots[2] = new InventorySlot(item, 5);
            inventory.Slots[4] = new InventorySlot(item, 9);
            widget.Bind(inventory);
            Assert.That(widget.VisibleSlotCount, Is.EqualTo(3));
            Assert.That(Text(1, "Name"), Is.EqualTo("Apple"));
            Assert.That(Text(1, "Quantity"), Is.EqualTo("2"));
            Assert.That(Text(2, "Name"), Is.EqualTo("Empty"));
            Assert.That(Text(2, "Quantity"), Is.Empty);
            Assert.That(Text(3, "Quantity"), Is.EqualTo("5"));
            Assert.That(widget.transform.Find("Slot 4"), Is.Null);
            Assert.That(widget.Focusable, Is.False);
            Assert.That(widget.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
        }

        [Test] public void ConfigurableCountReusesSlotsAndBindingReleasesSubscriptions()
        {
            var first = new Inventory { InventorySize = 4 };
            var second = new Inventory();
            widget.Bind(first);
            widget.SetSlotCount(10);
            Assert.That(widget.VisibleSlotCount, Is.EqualTo(4));
            Transform existing = widget.transform.Find("Slot 4");
            widget.SetSlotCount(1);
            Assert.That(existing.gameObject.activeSelf, Is.False);
            widget.SetSlotCount(4);
            Assert.That(widget.transform.Find("Slot 4"), Is.SameAs(existing));
            widget.enabled = false;
            Assert.That(first.Subscribers, Is.Zero);
            first.Slots[0] = new InventorySlot(item, 7);
            widget.enabled = true;
            Assert.That(first.Subscribers, Is.EqualTo(1));
            Assert.That(Text(1, "Quantity"), Is.EqualTo("7"));
            widget.Bind(second);
            Assert.That(first.Subscribers, Is.Zero);
            second.Slots[0] = new InventorySlot(item, 3);
            second.Notify();
            Assert.That(Text(1, "Quantity"), Is.EqualTo("3"));
            widget.Bind(null);
            Assert.That(widget.VisibleSlotCount, Is.Zero);
            Assert.That(second.Subscribers, Is.Zero);
        }

        [UnityTest] public IEnumerator SelectionHighlightTracksEquipment()
        {
            pawn = new GameObject("Player");
            var inventory = pawn.AddComponent<UInventoryComponent>();
            inventory.SetLoggingEnabled(false);
            inventory.Initialize();
            inventory.TryAddItem(item, 2);
            var equipment = pawn.AddComponent<ItemEquipComponent>();
            equipment.SetInventory(inventory);
            widget.Bind(inventory, equipment);
            var first = widget.transform.Find("Slot 1").GetComponentInChildren<Image>();
            var second = widget.transform.Find("Slot 2").GetComponentInChildren<Image>();
            Color selected = first.color;
            Color normal = second.color;
            Assert.That(selected, Is.Not.EqualTo(normal));
            equipment.SelectSlot(1);
            yield return null;
            Assert.That(first.color, Is.EqualTo(normal));
            Assert.That(second.color, Is.EqualTo(selected));
        }
    }
}
