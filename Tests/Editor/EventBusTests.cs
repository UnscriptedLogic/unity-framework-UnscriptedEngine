using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace UnscriptedEngine.Tests
{
    public class EventBusTests
    {
        private sealed class Message : ICancellableEvent
        {
            public bool IsCancelled { get; private set; }
            public void Cancel() => IsCancelled = true;
        }
        private readonly List<Action<Message>> subscriptions = new();
        private void Subscribe(Action<Message> action, int priority = 0)
        {
            subscriptions.Add(action);
            EventBus.Subscribe(action, priority);
        }
        [TearDown] public void Cleanup()
        {
            foreach (var action in subscriptions) EventBus.Unsubscribe(action);
            subscriptions.Clear();
        }
        [Test] public void NoListenersAllowsPublish() => Assert.That(EventBus.Publish(new Message()), Is.True);
        [Test] public void PriorityDescendingAndEqualPriorityStable()
        {
            var calls = new List<int>();
            Subscribe(_ => calls.Add(1), 0);
            Subscribe(_ => calls.Add(2), 10);
            Subscribe(_ => calls.Add(3), 10);
            EventBus.Publish(new Message());
            Assert.That(calls, Is.EqualTo(new[] { 2, 3, 1 }));
        }
        [TestCase(AppendStyle.Latest, 2, 1)]
        [TestCase(AppendStyle.Earliest, 1, 2)]
        [TestCase(AppendStyle.Force, 1, 2)]
        public void PlacementResolvesOccupiedPriority(AppendStyle style, int first, int second)
        {
            var calls = new List<int>();
            Subscribe(_ => calls.Add(1), 5);
            Action<Message> added = _ => calls.Add(2);
            subscriptions.Add(added);
            EventBus.Subscribe(added, 5, style);
            EventBus.Publish(new Message());
            Assert.That(calls, Is.EqualTo(new[] { first, second }));
        }
        [Test] public void UnsubscribeRemovesOnlyOneDuplicate()
        {
            int calls = 0;
            Action<Message> action = _ => calls++;
            Subscribe(action); Subscribe(action);
            EventBus.Unsubscribe(action);
            EventBus.Publish(new Message());
            Assert.That(calls, Is.EqualTo(1));
        }
        [Test] public void CancellationStopsLowerPriorityListeners()
        {
            int calls = 0;
            Subscribe(message => message.Cancel(), 10);
            Subscribe(_ => calls++);
            Assert.That(EventBus.Publish(new Message()), Is.False);
            Assert.That(calls, Is.Zero);
        }
        [Test] public void SubscribeDuringPublishAppliesToNextPublish()
        {
            int calls = 0;
            Subscribe(_ => Subscribe(__ => calls++));
            EventBus.Publish(new Message());
            Assert.That(calls, Is.Zero);
            EventBus.Publish(new Message());
            Assert.That(calls, Is.EqualTo(1));
        }
        [Test] public void UnsubscribeDuringPublishUsesDispatchSnapshot()
        {
            int calls = 0;
            Action<Message> other = _ => calls++;
            Subscribe(_ => EventBus.Unsubscribe(other), 10);
            Subscribe(other);
            EventBus.Publish(new Message());
            EventBus.Publish(new Message());
            Assert.That(calls, Is.EqualTo(1));
        }
        [Test] public void NullListenerRejected() => Assert.Throws<ArgumentNullException>(() => EventBus.Subscribe<Message>(null));
        [Test] public void NotificationFailuresDoNotSkipLaterSubscribers()
        {
            int calls = 0;
            Action<int> throwing = _ => throw new InvalidOperationException("notification failed");
            Action<int> next = _ => calls++;
            EventBus.Subscribe(throwing, 10); EventBus.Subscribe(next);
            try
            {
                UnityEngine.TestTools.LogAssert.Expect(LogType.Exception,
                    new System.Text.RegularExpressions.Regex("InvalidOperationException: notification failed"));
                EventBus.PublishNotification(1);
                Assert.That(calls, Is.EqualTo(1));
            }
            finally { EventBus.Unsubscribe(throwing); EventBus.Unsubscribe(next); }
        }
        [Test] public void CancellableEventsCannotUseNotificationDispatch()
        {
            Assert.Throws<ArgumentException>(() => EventBus.PublishNotification(new Message()));
        }
        [Test] public void RegularPublishStillPropagatesExceptions()
        {
            Subscribe(_ => throw new InvalidOperationException("validation failed"));
            Assert.Throws<InvalidOperationException>(() => EventBus.Publish(new Message()));
        }
        [Test] public void DestroyedUnityListenerIsSkipped()
        {
            var target = new GameObject("Destroyed event listener");
            Action<string> callback = target.SendMessage;
            EventBus.Subscribe(callback);
            try
            {
                UnityEngine.Object.DestroyImmediate(target);
                Assert.That(EventBus.Publish("NoSuchMethod"), Is.True);
            }
            finally
            {
                EventBus.Unsubscribe(callback);
                if (target != null) UnityEngine.Object.DestroyImmediate(target);
            }
        }
        [Test] public void InvalidPlacementRejected() => Assert.Throws<ArgumentOutOfRangeException>(() => EventBus.Subscribe<Message>(_ => { }, 0, (AppendStyle)99));
        [Test] public void PriorityOverflowDoesNotAlterExistingListeners()
        {
            int calls = 0;
            Subscribe(_ => calls++, int.MaxValue);
            Assert.Throws<OverflowException>(() => EventBus.Subscribe<Message>(_ => { }, int.MaxValue, AppendStyle.Force));
            EventBus.Publish(new Message());
            Assert.That(calls, Is.EqualTo(1));
        }
    }
}
