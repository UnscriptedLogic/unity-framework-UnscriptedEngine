using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnscriptedEngine
{
    // Allocates on subscription changes, not on steady-state dispatch.
    internal sealed class UpdateChannel
    {
        private sealed class Entry
        {
            public Behaviour Owner;
            public Action<float> Callback;
            public bool Active = true;
            public int Order;
            public long Sequence;
        }

        private readonly List<Entry> entries = new();
        private readonly Dictionary<Behaviour, Entry> registered = new();
        private long sequence;
        private bool needsSort;
        private static readonly Comparison<Entry> CompareEntries = (a, b) =>
            a.Order != b.Order ? a.Order.CompareTo(b.Order) : a.Sequence.CompareTo(b.Sequence);
        private bool dispatching;
        private bool needsCompaction;

        public int Count => registered.Count;

        public void Register(Behaviour owner, Action<float> callback, int order = 0)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            if (registered.TryGetValue(owner, out var existing))
            {
                if (existing.Callback == callback && existing.Order == order) return;
                Unregister(owner);
            }
            var entry = new Entry { Owner = owner, Callback = callback, Order = order, Sequence = sequence++ };
            registered.Add(owner, entry);
            entries.Add(entry);
            needsSort = true;
        }

        public void Unregister(Behaviour owner)
        {
            if (ReferenceEquals(owner, null) || !registered.TryGetValue(owner, out var entry)) return;
            registered.Remove(owner);
            entry.Active = false;
            entry.Callback = null;
            needsCompaction = true;
            if (!dispatching && registered.Count == 0)
            {
                entries.Clear();
                needsCompaction = false;
            }
        }

        public void Dispatch(float deltaTime)
        {
            if (dispatching) throw new InvalidOperationException("Update dispatch cannot be reentered.");
            if (needsSort) { entries.Sort(CompareEntries); needsSort = false; }
            dispatching = true;
            try
            {
                int count = entries.Count;
                for (int i = 0; i < count; i++)
                {
                    var entry = entries[i];
                    if (!entry.Active) continue;
                    if (entry.Owner == null)
                    {
                        Unregister(entry.Owner);
                        continue;
                    }
                    if (!entry.Owner.isActiveAndEnabled) continue;
                    try { entry.Callback(deltaTime); }
                    catch (Exception exception) { Debug.LogException(exception, entry.Owner); }
                }
            }
            finally
            {
                dispatching = false;
                if (needsCompaction) Compact();
            }
        }

        public void Clear()
        {
            foreach (var entry in entries)
            {
                entry.Active = false;
                entry.Callback = null;
            }
            registered.Clear();
            needsCompaction = true;
            if (!dispatching) Compact();
        }

        private void Compact()
        {
            int destination = 0;
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].Active) entries[destination++] = entries[i];
            if (destination < entries.Count) entries.RemoveRange(destination, entries.Count - destination);
            needsCompaction = false;
        }
    }
}
