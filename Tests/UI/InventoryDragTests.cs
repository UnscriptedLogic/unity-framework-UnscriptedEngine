using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnscriptedEngine.UI;

namespace UnscriptedEngine.Tests
{
    public sealed class InventoryDragTests
    {
        private GameObject canvasObject;
        private GameObject inventoryObject;
        private GameObject eventObject;
        private UInventoryComponent inventory;
        private InventoryView view;
        private Transform grid;
        private EventSystem eventSystem;
        private ItemDataSO item;
        private ItemDataSO other;

        [SetUp]
        public void Setup()
        {
            inventoryObject = new GameObject("Inventory drag test");
            inventory = inventoryObject.AddComponent<UInventoryComponent>();
            inventory.Initialize();
            item = ScriptableObject.CreateInstance<ItemDataSO>();
            other = ScriptableObject.CreateInstance<ItemDataSO>();
            inventory.TryAddItem(item);
            eventObject = new GameObject("Drag test EventSystem", typeof(EventSystem));
            eventSystem = eventObject.GetComponent<EventSystem>();
            canvasObject = new GameObject("Drag test Canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            GameObject viewObject = new GameObject("Inventory view", typeof(RectTransform));
            viewObject.transform.SetParent(canvasObject.transform, false);
            view = viewObject.AddComponent<InventoryView>();
            GameObject gridObject = new GameObject("Grid", typeof(RectTransform));
            gridObject.transform.SetParent(view.transform, false);
            grid = gridObject.transform;
            GameObject template = new GameObject("Template", typeof(RectTransform), typeof(Image), typeof(InventorySlotView));
            template.transform.SetParent(canvasObject.transform, false);
            template.SetActive(false);
            SerializedObject settings = new SerializedObject(view);
            settings.FindProperty("slotPrefab").objectReferenceValue = template;
            settings.FindProperty("gridContainer").objectReferenceValue = grid;
            settings.ApplyModifiedPropertiesWithoutUndo();
            view.Initialize(inventory);
        }

        [TearDown]
        public void Cleanup()
        {
            Object.DestroyImmediate(canvasObject);
            Object.DestroyImmediate(eventObject);
            Object.DestroyImmediate(inventoryObject);
            Object.DestroyImmediate(item);
            Object.DestroyImmediate(other);
        }

        private PointerEventData BeginDrag(int sourceIndex)
        {
            InventorySlotDragHandler source = grid.GetChild(sourceIndex).GetComponent<InventorySlotDragHandler>();
            PointerEventData pointer = new PointerEventData(eventSystem)
            {
                button = PointerEventData.InputButton.Left, pointerDrag = source.gameObject,
                position = new Vector2(100f, 100f)
            };
            source.OnBeginDrag(pointer);
            source.OnDrag(pointer);
            return pointer;
        }

        [Test]
        public void StandaloneViewDragMovesItemAndDoesNotBlockDropRaycasts()
        {
            PointerEventData pointer = BeginDrag(0);
            Transform preview = canvasObject.transform.Find("Inventory Drag Preview");
            Assert.That(preview, Is.Not.Null);
            Assert.That(preview.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
            grid.GetChild(3).GetComponent<InventorySlotDragHandler>().OnDrop(pointer);
            Assert.That(inventory.Items.ContainsKey(0), Is.False);
            Assert.That(inventory.Items[3].Item, Is.SameAs(item));
            pointer.pointerDrag.GetComponent<InventorySlotDragHandler>().OnEndDrag(pointer);
            Assert.That(canvasObject.transform.Find("Inventory Drag Preview"), Is.Null);
        }

        [Test]
        public void DragOntoDifferentItemSwapsContents()
        {
            inventory.TryAddItem(other);
            PointerEventData pointer = BeginDrag(0);
            grid.GetChild(1).GetComponent<InventorySlotDragHandler>().OnDrop(pointer);
            Assert.That(inventory.Items[0].Item, Is.SameAs(other));
            Assert.That(inventory.Items[1].Item, Is.SameAs(item));
        }

        [Test]
        public void ChangedSourceCancelsDropWithoutMovingReplacementItem()
        {
            PointerEventData pointer = BeginDrag(0);
            inventory.TryRemoveItem(0);
            inventory.TryAddItem(other);
            grid.GetChild(3).GetComponent<InventorySlotDragHandler>().OnDrop(pointer);
            Assert.That(inventory.Items[0].Item, Is.SameAs(other));
            Assert.That(inventory.Items.ContainsKey(3), Is.False);
        }

        [Test]
        public void UnbindingViewCancelsDragAndRemovesPreview()
        {
            PointerEventData pointer = BeginDrag(0);
            view.Initialize(null);
            grid.GetChild(3).GetComponent<InventorySlotDragHandler>().OnDrop(pointer);
            Assert.That(inventory.Items[0].Item, Is.SameAs(item));
            Assert.That(canvasObject.transform.Find("Inventory Drag Preview"), Is.Null);
        }
    }
}
