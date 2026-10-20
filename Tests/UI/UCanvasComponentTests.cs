using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnscriptedEngine.UI;

namespace UnscriptedEngine.Tests
{
    public sealed class CanvasTestController : UController { }
    public sealed class CanvasTestInput : PlayerInputEvent
    {
        public CanvasTestInput(UController controller) : base(controller) { }
    }
    public sealed class CanvasTestWidget : UCanvasComponent
    {
        public CanvasLayer TestLayer = CanvasLayer.Screen;
        public CanvasInputPolicy Policy = CanvasInputPolicy.Modal;
        public Action<CanvasTestWidget> OnInput;
        public int Calls;
        public bool Initialized;
        public bool ShownAfterInitialization;
        public override CanvasLayer Layer => TestLayer;
        public override CanvasInputPolicy InputPolicy => Policy;
        protected override void OnShown() => ShownAfterInitialization = Initialized && Controller != null;
        public override bool HandleInput(PlayerInputEvent input)
        {
            Calls++;
            OnInput?.Invoke(this);
            return false;
        }
    }

    public class UCanvasControllerTests
    {
        private readonly List<GameObject> objects = new();
        private UCanvasController canvas;
        private CanvasTestController controller;
        private CanvasTestWidget prefab;

        private GameObject Create(string name, params Type[] components)
        {
            var obj = new GameObject(name, components);
            objects.Add(obj);
            return obj;
        }

        [SetUp] public void Setup()
        {
            controller = Create("Controller", typeof(CanvasTestController)).GetComponent<CanvasTestController>();
            canvas = Create("Canvas", typeof(RectTransform), typeof(UCanvasController)).GetComponent<UCanvasController>();
            canvas.Initialize(controller);
            canvas.RegisterInput<CanvasTestInput>();
            prefab = Create("Prefab", typeof(RectTransform), typeof(CanvasTestWidget)).GetComponent<CanvasTestWidget>();
            prefab.gameObject.SetActive(false);
        }

        [TearDown] public void Cleanup()
        {
            for (int i = objects.Count - 1; i >= 0; i--)
                if (objects[i] != null) UnityEngine.Object.DestroyImmediate(objects[i]);
            objects.Clear();
        }

        [Test] public void NewestModalSwallowsAndClosingDoesNotReachOlderWidget()
        {
            var older = canvas.Show(prefab);
            var newer = canvas.Show(prefab);
            newer.OnInput = widget => UnityEngine.Object.DestroyImmediate(widget.gameObject);
            Assert.That(EventBus.Publish(new CanvasTestInput(controller)), Is.False);
            Assert.That(older.Calls, Is.Zero);
            Assert.That(canvas.FocusedCanvas, Is.SameAs(older));
            Assert.That(InputFocus.IsBlocked(controller), Is.True);
        }

        [Test] public void LayerOutranksRecencyAndPassiveOverlayDoesNotStealFocus()
        {
            var modal = canvas.Show(prefab, widget => widget.TestLayer = CanvasLayer.Modal);
            var screen = canvas.Show(prefab);
            var toast = canvas.Show(prefab, widget =>
            {
                widget.TestLayer = CanvasLayer.Overlay;
                widget.Policy = CanvasInputPolicy.Passive;
            });
            EventBus.Publish(new CanvasTestInput(controller));
            Assert.That(modal.Calls, Is.EqualTo(1));
            Assert.That(screen.Calls, Is.Zero);
            Assert.That(toast.Calls, Is.Zero);
            Assert.That(canvas.FocusedCanvas, Is.SameAs(modal));
            Assert.That(screen.GetComponent<CanvasGroup>().interactable, Is.False);
            Assert.That(toast.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
        }

        [Test] public void RemovingMiddleThenAttachingStillGivesNewestFirst()
        {
            var first = canvas.Show(prefab);
            var middle = canvas.Show(prefab);
            var third = canvas.Show(prefab);
            UnityEngine.Object.DestroyImmediate(middle.gameObject);
            var newest = canvas.Show(prefab);
            EventBus.Publish(new CanvasTestInput(controller));
            Assert.That(newest.Calls, Is.EqualTo(1));
            Assert.That(third.Calls + first.Calls, Is.Zero);
            canvas.BringToFront(first);
            EventBus.Publish(new CanvasTestInput(controller));
            Assert.That(first.Calls, Is.EqualTo(1));
        }

        [Test] public void HideAndDestroyReleaseOnlyTheirOwnFocusLeases()
        {
            var first = canvas.Show(prefab);
            var second = canvas.Show(prefab);
            second.Hide();
            Assert.That(InputFocus.IsBlocked(controller), Is.True);
            first.Hide();
            Assert.That(InputFocus.IsBlocked(controller), Is.False);
            second.Show();
            Assert.That(InputFocus.IsBlocked(controller), Is.True);
            UnityEngine.Object.DestroyImmediate(second.gameObject);
            Assert.That(InputFocus.IsBlocked(controller), Is.False);
        }

        [Test] public void OtherControllerIsNotBlockedOrConsumed()
        {
            var other = Create("Other", typeof(CanvasTestController)).GetComponent<CanvasTestController>();
            var widget = canvas.Show(prefab);
            Assert.That(EventBus.Publish(new CanvasTestInput(other)), Is.True);
            Assert.That(widget.Calls, Is.Zero);
            Assert.That(InputFocus.IsBlocked(other), Is.False);
        }

        [Test] public void InitializationPrecedesShowingAndDisableUnsubscribes()
        {
            var widget = canvas.Show(prefab, instance => instance.Initialized = true);
            Assert.That(widget.ShownAfterInitialization, Is.True);
            canvas.enabled = false;
            Assert.That(InputFocus.IsBlocked(controller), Is.False);
            Assert.That(EventBus.Publish(new CanvasTestInput(controller)), Is.True);
            canvas.enabled = true;
            EventBus.Publish(new CanvasTestInput(controller));
            Assert.That(widget.Calls, Is.EqualTo(1));
        }

        [Test] public void WidgetAttachedDuringDispatchDoesNotReceiveOpeningEvent()
        {
            var widget = canvas.Show(prefab, instance => instance.Policy = CanvasInputPolicy.HandleOnly);
            CanvasTestWidget opened = null;
            widget.OnInput = _ => opened = canvas.Show(prefab);
            EventBus.Publish(new CanvasTestInput(controller));
            Assert.That(opened.Calls, Is.Zero);
        }

        [Test] public void ClosingModalRestoresSelectionAndLeavesOnePointerBackdrop()
        {
            var eventSystem = Create("EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();
            var first = canvas.Show(prefab);
            var button = Create("Button", typeof(RectTransform), typeof(Button));
            button.transform.SetParent(first.transform, false);
            eventSystem.SetSelectedGameObject(button);
            var second = canvas.Show(prefab);
            Assert.That(eventSystem.currentSelectedGameObject, Is.Null);
            Assert.That(first.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
            int activeBackdrops = 0;
            foreach (var image in canvas.GetComponentsInChildren<Image>())
                if (image.name == "Modal backdrop" && image.raycastTarget) activeBackdrops++;
            Assert.That(activeBackdrops, Is.EqualTo(1));
            second.Close();
            Assert.That(eventSystem.currentSelectedGameObject, Is.SameAs(button));
            Assert.That(first.GetComponent<CanvasGroup>().blocksRaycasts, Is.True);
        }

        [Test] public void FailedInitializationLeavesNoWidgetOrFocusLease()
        {
            Assert.Throws<InvalidOperationException>(() => canvas.Show(prefab,
                _ => throw new InvalidOperationException("Initialization failed")));
            Assert.That(canvas.FocusedCanvas, Is.Null);
            Assert.That(InputFocus.IsBlocked(controller), Is.False);
            Assert.That(canvas.GetComponentsInChildren<UCanvasComponent>(true), Is.Empty);
        }
    }
}
