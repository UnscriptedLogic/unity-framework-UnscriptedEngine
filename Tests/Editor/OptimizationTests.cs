using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnscriptedEngine.Tests
{
    public class OptimizationTests
    {
        private readonly List<GameObject> objects = new();
        private GameObject Create(string name)
        {
            var go = new GameObject(name);
            objects.Add(go);
            return go;
        }
        private static object Field(object target, string name) => target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void Call(object target, string name, params object[] args) => target.GetType()
            .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
        [TearDown] public void Cleanup()
        {
            foreach (var go in objects) if (go != null) Object.DestroyImmediate(go);
            objects.Clear();
        }

        private class FrameOnly : UObjectComponent { protected override void Tick(float dt) { } }
        private class InheritedFrame : FrameOnly { }
        private class Both : FrameOnly { protected override void ServerFixedTick(float dt) { } }
        private class Hidden : FrameOnly { protected new void Tick(float dt) { } }

        [Test] public void TickCapabilitiesIncludeInheritedAndAuthorityHooks()
        {
            Assert.That(TickCapabilities.For(typeof(UInventoryComponent), typeof(UObjectComponent)), Is.EqualTo((false, false)));
            Assert.That(TickCapabilities.For(typeof(InheritedFrame), typeof(UObjectComponent)), Is.EqualTo((true, false)));
            Assert.That(TickCapabilities.For(typeof(Hidden), typeof(UObjectComponent)), Is.EqualTo((true, false)));
            Assert.That(TickCapabilities.For(typeof(Both), typeof(UObjectComponent)), Is.EqualTo((true, true)));
        }

        [Test] public void MovementAllocatesLazilyAndReusesMatchingBuffers()
        {
            var movement = Create("Movement buffers").AddComponent<CharacterMovementComponent>();
            Assert.That(Field(movement, "inputBuffer"), Is.Null);
            Call(movement, "AllocatePredictionBuffers");
            var inputs = Field(movement, "inputBuffer");
            var states = Field(movement, "stateBuffer");
            Call(movement, "AllocatePredictionBuffers");
            Assert.That(Field(movement, "inputBuffer"), Is.SameAs(inputs));
            Assert.That(Field(movement, "stateBuffer"), Is.SameAs(states));
            Call(movement, "ResetPredictionState");
            Assert.That(Field(movement, "inputBuffer"), Is.Null, "Non-owner must not retain prediction history.");
        }

        [Test] public void NestedPublishSeesNewSubscriptionsButOuterSnapshotDoesNot()
        {
            var calls = new List<string>();
            bool nested = false;
            Action<OptimizationTests> added = _ => calls.Add("added");
            Action<OptimizationTests> first = _ =>
            {
                calls.Add("first");
                if (nested) return;
                nested = true;
                EventBus.Subscribe(added);
                EventBus.Publish(this);
            };
            EventBus.Subscribe(first);
            try
            {
                EventBus.Publish(this);
                Assert.That(calls, Is.EqualTo(new[] { "first", "first", "added" }));
            }
            finally { EventBus.Unsubscribe(first); EventBus.Unsubscribe(added); }
        }

        [Test] public void WarmEventDispatchDoesNotAllocate()
        {
            Action<OptimizationTests> listener = _ => { };
            EventBus.Subscribe(listener);
            try
            {
                EventBus.Publish(this);
                EventBus.PublishNotification(this);
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 100; i++)
                {
                    EventBus.Publish(this);
                    EventBus.PublishNotification(this);
                }
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.That(allocated, Is.Zero);
            }
            finally { EventBus.Unsubscribe(listener); }
        }

        [Test] public void InteractionGrowsBufferDeduplicatesAndKeepsOldSnapshots()
        {
            Vector3 origin = new Vector3(43210, 43210, 43210);
            var scanner = Create("Scanner").AddComponent<InteractionComponent>();
            scanner.transform.position = origin;
            for (int i = 0; i < 40; i++)
            {
                var target = Create("Target " + i);
                target.transform.position = origin + Vector3.right * (i * .02f);
                target.AddComponent<UInteractableComponent>();
                target.AddComponent<BoxCollider>();
                target.AddComponent<SphereCollider>();
            }
            Physics.SyncTransforms();
            Call(scanner, "Tick", 0f);
            Assert.That(Field(scanner, "_buttonComponents"), Is.Null, "Focused queries should not create legacy arrays.");
            var previous = scanner.ButtonComponents;
            Assert.That(previous.Length, Is.EqualTo(40));
            var nearest = scanner.FirstInteractable;
            previous[0] = null;
            Assert.That(scanner.FirstInteractable, Is.Null, "Legacy array mutation remains observable within its tick.");
            Call(scanner, "Tick", 0f);
            Assert.That(scanner.FirstInteractable, Is.SameAs(nearest));
            Assert.That(previous[0], Is.Null, "A new tick must not mutate retained arrays.");
            Assert.That(scanner.ButtonComponents, Is.Not.SameAs(previous));
        }
    }
}
