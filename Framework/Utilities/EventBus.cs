using UnityEngine;
using System;
using System.Collections.Generic;
using Object = UnityEngine.Object;

namespace UnscriptedEngine
{
    public enum AppendStyle
    {
        Force,
        Latest,
        Earliest
    }

    public static class EventBus
    {
        private static readonly Dictionary<Type, List<ListenerEntry>> _listeners = new();
        private static long _nextSubscriptionOrder;

        // Register a method to listen for an event
        public static void Subscribe<T>(Action<T> listener, int priority = 0)
        {
            SubscribeInternal(listener, priority, null);
        }

        /// <summary>
        /// Registers a listener with explicit priority placement. Latest searches upward
        /// for a free priority; Earliest searches downward. Force shifts all existing
        /// priorities at or above the requested priority up by one.
        /// Dispatch still runs from highest priority to lowest priority.
        /// </summary>
        /// <exception cref="OverflowException">Placement exceeds the integer priority range.</exception>
        public static void Subscribe<T>(Action<T> listener, int priority, AppendStyle appendStyle)
        {
            if (appendStyle != AppendStyle.Force && appendStyle != AppendStyle.Latest &&
                appendStyle != AppendStyle.Earliest)
            {
                throw new ArgumentOutOfRangeException(nameof(appendStyle));
            }

            SubscribeInternal(listener, priority, appendStyle);
        }

        private static void SubscribeInternal<T>(Action<T> listener, int priority, AppendStyle? appendStyle)
        {
            if (listener == null)
            {
                throw new ArgumentNullException(nameof(listener));
            }

            var type = typeof(T);
            if (!_listeners.TryGetValue(type, out var listeners))
            {
                listeners = new List<ListenerEntry>();
                _listeners[type] = listeners;
            }

            RemoveDeadListeners(type, listeners);
            if (appendStyle == AppendStyle.Force)
            {
                // Check before shifting so overflow cannot leave a partially updated list.
                foreach (var entry in listeners)
                {
                    if (entry.Priority == int.MaxValue)
                    {
                        throw new OverflowException("Cannot shift a subscriber beyond int.MaxValue.");
                    }
                }

                for (var i = 0; i < listeners.Count; i++)
                {
                    var entry = listeners[i];
                    if (entry.Priority >= priority)
                    {
                        listeners[i] = new ListenerEntry(entry.Callback, entry.Priority + 1,
                            entry.SubscriptionOrder);
                    }
                }
            }
            else if (appendStyle.HasValue)
            {
                var occupiedPriorities = new HashSet<int>();
                foreach (var entry in listeners)
                {
                    occupiedPriorities.Add(entry.Priority);
                }

                var step = appendStyle == AppendStyle.Latest ? 1 : -1;
                while (occupiedPriorities.Contains(priority))
                {
                    priority = checked(priority + step);
                }
            }

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
