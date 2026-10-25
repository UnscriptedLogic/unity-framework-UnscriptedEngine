using System.Collections;
using UnityEngine.TestTools;
using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnscriptedEngine.UI;

namespace UnscriptedEngine.Tests
{
    public class WidgetCompositionTests
    {
        private GameObject owner;
        private GameObject events;
        private UCanvasController controller;
        private ScreenProbe screen;
        [UnitySetUp] public IEnumerator Setup()
        {
            yield return new EnterPlayMode();

            owner = new GameObject("Owner", typeof(CanvasTestController));
            CanvasTestController player = owner.GetComponent<CanvasTestController>();
            Assert.That(player, Is.Not.Null, "Runtime test controller could not be attached.");
            controller = UCanvasController.Create(player);
            events = new GameObject("Event system", typeof(EventSystem));
            screen = controller.CreateCanvas<ScreenProbe>();
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (controller != null) UnityEngine.Object.DestroyImmediate(controller.gameObject);
            if (events != null) UnityEngine.Object.DestroyImmediate(events);
            if (owner != null) UnityEngine.Object.DestroyImmediate(owner);

            controller = null;
            events = null;
            owner = null;
            screen = null;

            yield return new ExitPlayMode();
        }
        private WidgetProbe Widget(Transform parent)
        {
            var go = new GameObject("Widget", typeof(RectTransform), typeof(Button));
            go.transform.SetParent(parent, false);
            return go.AddComponent<WidgetProbe>();
        }
        private PlayerActionInputEvent Press(string name, PlayerInputPhase phase = PlayerInputPhase.Performed) =>
            new(controller.OwningController, "UI", name, Guid.Empty, phase, true, phase != PlayerInputPhase.Canceled, false, false);

        [Test] public void WidgetMembershipTracksDisableReenableAndReparent()
        {
            var widget = Widget(screen.transform);
            Assert.That(widget.OwnerCanvas, Is.SameAs(screen));
            Assert.That(screen.Widgets.Count, Is.EqualTo(1));
            widget.enabled = false;
            Assert.That(screen.Widgets.Count, Is.Zero);
            widget.enabled = true;
            Assert.That(screen.Widgets.Count, Is.EqualTo(1));
            var other = controller.CreateCanvas<ScreenProbe>();
            widget.transform.SetParent(other.transform, false);
            Assert.That(widget.OwnerCanvas, Is.SameAs(other));
            Assert.That(screen.Widgets.Count, Is.Zero);
            Assert.That(other.Widgets.Count, Is.EqualTo(1));
        }

        [Test] public void LocalMessagesBubbleUntilHandled()
        {
            var parent = Widget(screen.transform);
            var child = Widget(parent.transform);
            Assert.That(child.Raise("ItemSelected"), Is.True);
            Assert.That(parent.Messages, Is.EqualTo(1));
            Assert.That(screen.Messages, Is.EqualTo(1));
            parent.Consume = true;
            child.Raise(42);
            Assert.That(parent.Messages, Is.EqualTo(2));
            Assert.That(screen.Messages, Is.EqualTo(1));
        }

        [Test] public void NavigationSkipsDisabledWidgetsAndFocusedWidgetGetsInputFirst()
        {
            var first = Widget(screen.transform);
            var skipped = Widget(screen.transform);
            var last = Widget(screen.transform);
            skipped.GetComponent<Button>().interactable = false;
            first.RequestFocus();
            Assert.That(screen.Navigate(false), Is.True);
            Assert.That(screen.FocusedWidget, Is.SameAs(last));
            last.Consume = true;
            var input = Press("Custom");
            controller.RouteInput(input);
            Assert.That(input.IsCancelled, Is.True);
            Assert.That(last.Calls, Is.EqualTo(1));
            Assert.That(first.Calls, Is.Zero);
            Assert.That(screen.Navigate(false), Is.True);
            Assert.That(screen.FocusedWidget, Is.SameAs(first));
        }

        [Test] public void BackClosesTopWithoutReopeningBinding()
        {
            controller.BackAction = "UI/Back";
            int opened = 0;
            controller.Bind("UI/Back", () => { opened++; return controller.CreateCanvas<ScreenProbe>(); });
            var input = Press("Back");
            controller.RouteInput(input);
            Assert.That(input.IsCancelled, Is.True);
            Assert.That(opened, Is.Zero);
            Assert.That(controller.FocusedCanvas, Is.Null);
            controller.RouteInput(Press("Back"));
            Assert.That(opened, Is.EqualTo(1));
        }

        [Test] public void ModalBlocksUnrelatedBindingsButExplicitGlobalBindingCanOpen()
        {
            int opened = 0;
            controller.Bind("UI/Inventory", () => { opened++; return controller.CreateCanvas<ScreenProbe>(); });
            controller.Bind("UI/Console", () => { opened++; return controller.CreateCanvas<ScreenProbe>(); }, allowDuringModal: true);
            controller.RouteInput(Press("Inventory"));
            Assert.That(opened, Is.Zero);
            controller.RouteInput(Press("Console", PlayerInputPhase.Started));
            controller.RouteInput(Press("Console", PlayerInputPhase.Canceled));
            Assert.That(opened, Is.Zero);
            controller.RouteInput(Press("Console"));
            Assert.That(opened, Is.EqualTo(1));
        }

        [Test] public void ToggleClosesItsOwnScreenAndFocusRestoresAfterModal()
        {
            screen.Close();
            controller.Bind("UI/Inventory", () => controller.CreateCanvas<ScreenProbe>(), toggle: true);
            controller.RouteInput(Press("Inventory"));
            var inventory = controller.FocusedCanvas;
            var widget = Widget(inventory.transform);
            widget.RequestFocus();
            var modal = controller.CreateCanvas<ScreenProbe>();
            Assert.That(widget.IsFocused, Is.False);
            modal.Close();
            Assert.That(inventory.FocusedWidget, Is.SameAs(widget));
            Assert.That(widget.IsFocused, Is.True);
            controller.RouteInput(Press("Inventory"));
            Assert.That(controller.FocusedCanvas, Is.Null);
            Assert.That(InputFocus.IsBlocked(controller.OwningController), Is.False);
        }
    }
}
