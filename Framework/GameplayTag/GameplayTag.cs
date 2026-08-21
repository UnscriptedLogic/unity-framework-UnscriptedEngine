using System;
using UnityEngine;

namespace Framework.GameplayTag
{
    [CreateAssetMenu(fileName = "GameplayTag", menuName = "Unscripted Engine/Gameplay Tag")]
    public sealed class GameplayTag : ScriptableObject
    {
        [SerializeField] private string path;
        [SerializeField, TextArea] private string description;
        [SerializeField] private GameplayTag parent;

        public string Path => path;
        public string Description => description;
        public GameplayTag Parent => parent;
        public ulong Id => GameplayTagId.FromPath(path);

        public bool IsValid => !string.IsNullOrEmpty(path) && Id != 0;

        public bool IsChildOf(GameplayTag possibleParent)
        {
            if (possibleParent == null || possibleParent == this)
            {
                return false;
            }

            // The database validator catches cycles in the editor. The guard keeps
            // malformed assets safe if they are loaded from an external bundle.
            GameplayTag current = parent;
            for (int depth = 0; current != null && depth < 1024; depth++, current = current.parent)
            {
                if (current == possibleParent)
                {
                    return true;
                }
            }

            return false;
        }

        public bool Matches(GameplayTag query, bool includeChildren = true)
        {
            if (query == null || !IsValid || !query.IsValid)
            {
                return false;
            }

            return this == query || (includeChildren && IsChildOf(query));
        }

        private void OnValidate()
        {
            path = GameplayTagPath.Normalize(path);
        }

        public override string ToString() => path ?? string.Empty;
    }

    internal static class GameplayTagPath
    {
        public static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string[] segments = value.Trim().Split('.');
            for (int i = 0; i < segments.Length; i++)
            {
                segments[i] = segments[i].Trim();
            }

            return string.Join(".", segments);
        }
    }

    internal static class GameplayTagId
    {
        // Stable FNV-1a hash. Unlike string.GetHashCode(), this is consistent across runs.
        public static ulong FromPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return 0;
            }

            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            ulong hash = offset;

            for (int i = 0; i < path.Length; i++)
            {
                hash ^= path[i];
                hash *= prime;
            }

            return hash;
        }
    }
}
