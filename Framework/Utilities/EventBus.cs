using UnityEngine;
using System;
using System.Collections.Generic;
using Object = UnityEngine.Object;

namespace UnscriptedEngine
{
    public static class EventBus
    {
        private static readonly Dictionary<Type, List<ListenerEntry>> _listeners = new();
        private static long _nextSubscriptionOrder;

        // Register a method to listen for an event
        public static void Subscribe<T>(Action<T> listener, int priority = 0)
        {
            var type = typeof(T);
            if (!_listeners.TryGetValue(type, out var listeners))
            {
                listeners = new List<ListenerEntry>();
                _listeners[type] = listeners;
            }

            RemoveDeadListeners(type, listeners);
            listeners.Add(new ListenerEntry(listener, priority, _nextSubscriptionOrder++));
            listeners.Sort(CompareListeners);
            _listeners[type] = listeners;
        }

        // Unregister a method to prevent memory leaks
        public static void Unsubscribe<T>(Action<T> listener)
        {
            var type = typeof(T);
            if (!_listeners.TryGetValue(type, out var listeners))
            {
                return;
            }

            RemoveDeadListeners(type, listeners);

            for (var i = listeners.Count - 1; i >= 0; i--)
            {
                if (listeners[i].Callback.Equals(listener))
                {
                    listeners.RemoveAt(i);
                    break;
                }
            }

            if (listeners.Count == 0)
            {
                _listeners.Remove(type);
            }
        }

        // Broadcast the event to all subscribers
        public static bool Publish<T>(T eventData)
        {
            var type = typeof(T);
            if (!_listeners.TryGetValue(type, out var listeners))
            {
                return true;
            }

            RemoveDeadListeners(type, listeners);

            foreach (var listener in listeners.ToArray())
            {
                if (!IsDeadUnityObject(listener.Callback.Target))
                {
                    ((Action<T>)listener.Callback).Invoke(eventData);

                    if (eventData is ICancellableEvent { IsCancelled: true })
                    {
                        return false;
                    }
                }
            }

            if (listeners.Count == 0)
            {
                _listeners.Remove(type);
            }

            return true;
        }

        private static void RemoveDeadListeners(Type type, List<ListenerEntry> listeners)
        {
            for (var i = listeners.Count - 1; i >= 0; i--)
            {
                if (IsDeadUnityObject(listeners[i].Callback.Target))
                {
                    listeners.RemoveAt(i);
                }
            }

            if (listeners.Count == 0)
            {
                _listeners.Remove(type);
            }
        }

        private static bool IsDeadUnityObject(object target)
        {
            return target is Object unityObject && unityObject == null;
        }

        private static int CompareListeners(ListenerEntry left, ListenerEntry right)
        {
            var priorityComparison = right.Priority.CompareTo(left.Priority);
            return priorityComparison != 0
                ? priorityComparison
                : left.SubscriptionOrder.CompareTo(right.SubscriptionOrder);
        }

        private readonly struct ListenerEntry
        {
            public ListenerEntry(Delegate callback, int priority, long subscriptionOrder)
            {
                Callback = callback;
                Priority = priority;
                SubscriptionOrder = subscriptionOrder;
            }

            public Delegate Callback { get; }
            public int Priority { get; }
            public long SubscriptionOrder { get; }
        }
    }

    public interface ICancellableEvent
    {
        bool IsCancelled { get; }
        void Cancel();
    }

}