using System.Collections;
using UnityEngine.TestTools;
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnscriptedEngine.UI;

namespace UnscriptedEngine.Tests
{
    public sealed class CanvasTestInput : PlayerInputEvent
    {
        public CanvasTestInput(UController controller) : base(controller) { }
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

        [UnitySetUp] public IEnumerator Setup()
        {
            yield return new EnterPlayMode();

            controller = Create("Controller", typeof(CanvasTestController)).GetComponent<CanvasTestController>();
            canvas = Create("Canvas", typeof(RectTransform), typeof(UCanvasController)).GetComponent<UCanvasController>();
            Assert.That(controller, Is.Not.Null, "Runtime test controller could not be attached.");
            Assert.That(canvas, Is.Not.Null);
            canvas.Initialize(controller);
            canvas.RegisterInput<CanvasTestInput>();
            prefab = Create("Prefab", typeof(RectTransform), typeof(CanvasTestWidget)).GetComponent<CanvasTestWidget>();
            Assert.That(prefab, Is.Not.Null, "Runtime test screen could not be attached.");
            prefab.gameObject.SetActive(false);
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            for (int i = objects.Count - 1; i >= 0; i--)
                if (objects[i] != null) UnityEngine.Object.DestroyImmediate(objects[i]);
            objects.Clear();
            canvas = null;
            controller = null;
            prefab = null;

            yield return new ExitPlayMode();
        }

        [Test] public void NullOwnerFactoryDoesNotCreateOrLeakRoot()
        {
            int before = Resources.FindObjectsOfTypeAll<UCanvasController>().Length;
            Assert.Throws<ArgumentNullException>(() => UCanvasController.Create(null));
            Assert.That(Resources.FindObjectsOfTypeAll<UCanvasController>().Length, Is.EqualTo(before));
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
            canvas.SendMessage("LateUpdate");
            foreach (var image in canvas.GetComponentsInChildren<Image>())
                if (image.name == "Modal backdrop" && image.raycastTarget) activeBackdrops++;
            Assert.That(activeBackdrops, Is.EqualTo(1));
            second.Close();
            Assert.That(eventSystem.currentSelectedGameObject, Is.SameAs(button));
            Assert.That(first.GetComponent<CanvasGroup>().blocksRaycasts, Is.True);
        }

        [Test] public void ParentActivationDoesNotReorderChildrenDuringUnityTraversal()
        {
            CanvasTestWidget hud = canvas.Show(prefab, instance =>
            {
                instance.TestLayer = CanvasLayer.HUD;
                instance.Policy = CanvasInputPolicy.Passive;
            });
            CanvasTestWidget modal = canvas.Show(prefab);
            GameObject hudLayer = hud.transform.parent.gameObject;

            hudLayer.SetActive(false);
            hudLayer.SetActive(true);
            canvas.gameObject.SetActive(false);

            Assert.That(InputFocus.IsBlocked(controller), Is.False);

            canvas.gameObject.SetActive(true);
            Assert.That(InputFocus.IsBlocked(controller), Is.True);
            Assert.That(canvas.FocusedCanvas, Is.SameAs(modal));

            canvas.SendMessage("LateUpdate");
            UnityEngine.TestTools.LogAssert.NoUnexpectedReceived();
        }

        [Test] public void BringToFrontUpdatesInputImmediatelyAndVisualOrderInLateUpdate()
        {
            CanvasTestWidget first = canvas.Show(prefab);
            CanvasTestWidget second = canvas.Show(prefab);
            canvas.SendMessage("LateUpdate");

            canvas.BringToFront(first);
            Assert.That(canvas.FocusedCanvas, Is.SameAs(first));
            Assert.That(first.transform.GetSiblingIndex(), Is.LessThan(second.transform.GetSiblingIndex()));

            canvas.SendMessage("LateUpdate");

            Assert.That(first.transform.GetSiblingIndex(), Is.GreaterThan(second.transform.GetSiblingIndex()));
            Assert.That(first.transform.parent.GetChild(first.transform.GetSiblingIndex() - 1).name,
                Is.EqualTo("Modal backdrop"));
        }

        [UnityTest] public IEnumerator FailedInitializationLeavesNoWidgetOrFocusLease()
        {
            Assert.Throws<InvalidOperationException>(() => canvas.Show(prefab,
                _ => throw new InvalidOperationException("Initialization failed")));
            Assert.That(canvas.FocusedCanvas, Is.Null);
            Assert.That(InputFocus.IsBlocked(controller), Is.False);
            yield return null; // Destroy completes at the end of the frame in Play Mode.
            Assert.That(canvas.GetComponentsInChildren<UCanvasComponent>(true), Is.Empty);
        }
    }
}
