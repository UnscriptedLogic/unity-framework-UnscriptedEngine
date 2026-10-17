using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace UnscriptedEngine.Tests
{
    public class UpdateChannelTests
    {
        private UpdateChannel channel;
        private readonly List<GameObject> objects = new();

        [SetUp] public void SetUp() => channel = new UpdateChannel();

        private Behaviour Owner()
        {
            var obj = new GameObject("Update channel test");
            objects.Add(obj);
            return obj.AddComponent<TestUpdateProbe>();
        }

        [TearDown] public void TearDown()
        {
            channel.Clear();
            foreach (var obj in objects) if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
            objects.Clear();
        }

        [Test] public void RegistrationIsIdempotentAndPreservesOrderAndDeltaTime()
        {
            var calls = new List<int>();
            var first = Owner();
            float receivedDelta = -1;
            Action<float> callback = delta => { receivedDelta = delta; calls.Add(1); };
            channel.Register(first, callback);
            channel.Register(first, callback);
            channel.Register(Owner(), _ => calls.Add(2));
            channel.Dispatch(0.125f);
            Assert.That(channel.Count, Is.EqualTo(2));
            Assert.That(calls, Is.EqualTo(new[] { 1, 2 }));
            Assert.That(receivedDelta, Is.EqualTo(0.125f));
        }

        [Test] public void PriorityOrdersListenersAndDefersEarlierPriorityAddedDuringDispatch()
        {
            var calls = new List<int>();
            var added = Owner();
            Action<float> callback = _ => calls.Add(-100);
            channel.Register(Owner(), _ => calls.Add(0));
            channel.Register(Owner(), _ => { calls.Add(-50); channel.Register(added, callback, -100); }, -50);
            channel.Register(Owner(), _ => calls.Add(-40), -40);
            channel.Dispatch(0.1f);
            Assert.That(calls, Is.EqualTo(new[] { -50, -40, 0 }));
            calls.Clear();
            channel.Dispatch(0.1f);
            Assert.That(calls, Is.EqualTo(new[] { -100, -50, -40, 0 }));
        }

        [Test] public void RemovingLaterListenerSkipsItInCurrentDispatch()
        {
            var later = Owner(); int calls = 0;
            channel.Register(Owner(), _ => channel.Unregister(later));
            channel.Register(later, _ => calls++);
            channel.Dispatch(0.1f);
            Assert.That(calls, Is.Zero);
            Assert.That(channel.Count, Is.EqualTo(1));
        }

        [Test] public void AddedListenerStartsOnNextDispatch()
        {
            int calls = 0; var later = Owner(); Action<float> callback = _ => calls++;
            channel.Register(Owner(), _ => channel.Register(later, callback));
            channel.Dispatch(0.1f); Assert.That(calls, Is.Zero);
            channel.Dispatch(0.1f); Assert.That(calls, Is.EqualTo(1));
        }

        [Test] public void RemovedAndReaddedListenerWaitsUntilNextDispatch()
        {
            var first = Owner(); var later = Owner(); int calls = 0;
            Action<float> callback = _ => calls++;
            channel.Register(first, _ =>
            {
                channel.Unregister(later);
                channel.Register(later, callback);
                channel.Unregister(first);
            });
            channel.Register(later, callback);
            channel.Dispatch(0.1f); Assert.That(calls, Is.Zero);
            channel.Dispatch(0.1f); Assert.That(calls, Is.EqualTo(1));
        }

        [Test] public void DisabledAndInactiveOwnersAreSkipped()
        {
            var owner = Owner(); int calls = 0;
            channel.Register(owner, _ => calls++);
            owner.enabled = false; channel.Dispatch(0.1f);
            owner.enabled = true; owner.gameObject.SetActive(false); channel.Dispatch(0.1f);
            Assert.That(calls, Is.Zero);
            owner.gameObject.SetActive(true); channel.Dispatch(0.1f);
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test] public void DestroyedOwnerIsPruned()
        {
            var owner = Owner(); int calls = 0;
            channel.Register(owner, _ => calls++);
            UnityEngine.Object.DestroyImmediate(owner.gameObject);
            channel.Dispatch(0.1f);
            Assert.That(calls, Is.Zero); Assert.That(channel.Count, Is.Zero);
        }

        [Test] public void ClearDuringDispatchStopsOldCallbacksButKeepsNewRegistrations()
        {
            var later = Owner(); int calls = 0;
            channel.Register(Owner(), _ => { channel.Clear(); channel.Register(later, __ => calls++); });
            channel.Register(Owner(), _ => calls += 100);
            channel.Dispatch(0.1f); Assert.That(calls, Is.Zero);
            channel.Dispatch(0.1f); Assert.That(calls, Is.EqualTo(1));
        }

        [Test] public void ListenerFailureDoesNotStopOtherListeners()
        {
            int calls = 0;
            channel.Register(Owner(), _ => throw new InvalidOperationException("tick failed"));
            channel.Register(Owner(), _ => calls++);
            LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("InvalidOperationException: tick failed"));
            channel.Dispatch(0.1f);
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test] public void FrameAndFixedChannelsRemainIndependent()
        {
            var fixedChannel = new UpdateChannel(); var owner = Owner();
            float frameDelta = 0; float fixedDelta = 0;
            channel.Register(owner, delta => frameDelta = delta);
            fixedChannel.Register(owner, delta => fixedDelta = delta);
            channel.Dispatch(0.125f); fixedChannel.Dispatch(0.02f);
            Assert.That(frameDelta, Is.EqualTo(0.125f));
            Assert.That(fixedDelta, Is.EqualTo(0.02f));
            channel.Unregister(owner);
            Assert.That(fixedChannel.Count, Is.EqualTo(1));
            fixedChannel.Clear();
        }
    }
}
