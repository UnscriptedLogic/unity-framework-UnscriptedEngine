using System;
using System.Collections.Generic;
using UnityEngine;

namespace Framework.GameplayTag
{
    [Serializable]
    public sealed class GameplayTagContainer : ISerializationCallbackReceiver
    {
        [SerializeField] private List<GameplayTag> tags = new();

        [NonSerialized] private HashSet<ulong> expandedIds;
        [NonSerialized] private HashSet<ulong> exactIds;

        public IReadOnlyList<GameplayTag> Tags => tags;
        public int Count => tags.Count;

        public bool Add(GameplayTag tag)
        {
            if (tag == null || !tag.IsValid || ContainsExact(tag))
            {
                return false;
            }

            tags.Add(tag);
            AddToRuntimeSets(tag);
            return true;
        }

        public bool Remove(GameplayTag tag)
        {
            if (tag == null || !tags.Remove(tag))
            {
                return false;
            }

            RebuildRuntimeSets();
            return true;
        }

        public void Clear()
        {
            tags.Clear();
            RebuildRuntimeSets();
        }

        public bool ContainsExact(GameplayTag tag)
        {
            EnsureRuntimeSets();
            return tag != null && tag.IsValid && exactIds.Contains(tag.Id);
        }

        public bool Has(GameplayTag tag)
        {
            EnsureRuntimeSets();
            return tag != null && tag.IsValid && expandedIds.Contains(tag.Id);
        }

        public bool HasAny(params GameplayTag[] queries)
        {
            if (queries == null)
            {
                return false;
            }

            for (int i = 0; i < queries.Length; i++)
            {
                if (Has(queries[i]))
                {
                    return true;
                }
            }

            return false;
        }

        public bool HasAll(params GameplayTag[] queries)
        {
            if (queries == null || queries.Length == 0)
            {
                return true;
            }

            for (int i = 0; i < queries.Length; i++)
            {
                if (!Has(queries[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public bool HasNone(params GameplayTag[] queries) => !HasAny(queries);

        public void OnBeforeSerialize()
        {
        }

        public void OnAfterDeserialize()
        {
            expandedIds = null;
            exactIds = null;
        }

        private void EnsureRuntimeSets()
        {
            if (expandedIds == null || exactIds == null)
            {
                RebuildRuntimeSets();
            }
        }

        private void RebuildRuntimeSets()
        {
            expandedIds = new HashSet<ulong>();
            exactIds = new HashSet<ulong>();

            for (int i = tags.Count - 1; i >= 0; i--)
            {
                if (tags[i] == null || !tags[i].IsValid)
                {
                    tags.RemoveAt(i);
                    continue;
                }

                AddToRuntimeSets(tags[i]);
            }
        }

        private void AddToRuntimeSets(GameplayTag tag)
        {
            EnsureRuntimeSetsWithoutRecursion();
            exactIds.Add(tag.Id);

            GameplayTag current = tag;
            for (int depth = 0; current != null && depth < 1024; depth++, current = current.Parent)
            {
                if (!current.IsValid || !expandedIds.Add(current.Id))
                {
                    break;
                }
            }
        }

        private void EnsureRuntimeSetsWithoutRecursion()
        {
            expandedIds ??= new HashSet<ulong>();
            exactIds ??= new HashSet<ulong>();
        }
    }
}
