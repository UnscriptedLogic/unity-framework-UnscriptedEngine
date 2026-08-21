using System.Collections.Generic;
using UnityEngine;

namespace Framework.GameplayTag
{
    [CreateAssetMenu(fileName = "GameplayTagDatabase", menuName = "Unscripted Engine/Gameplay Tag Database")]
    public sealed class GameplayTagDatabase : ScriptableObject
    {
        [SerializeField] private List<GameplayTag> tags = new();

        private Dictionary<string, GameplayTag> lookup;

        public IReadOnlyList<GameplayTag> Tags => tags;

        public bool TryGet(string path, out GameplayTag tag)
        {
            BuildLookup();
            return lookup.TryGetValue(GameplayTagPath.Normalize(path), out tag);
        }

        private void BuildLookup()
        {
            if (lookup != null)
            {
                return;
            }

            lookup = new Dictionary<string, GameplayTag>(System.StringComparer.Ordinal);

            for (int i = 0; i < tags.Count; i++)
            {
                GameplayTag tag = tags[i];
                if (tag != null && tag.IsValid)
                {
                    lookup[tag.Path] = tag;
                }
            }
        }

        private void OnValidate()
        {
            lookup = null;
            HashSet<string> paths = new(System.StringComparer.Ordinal);
            HashSet<ulong> ids = new();

            for (int i = 0; i < tags.Count; i++)
            {
                GameplayTag tag = tags[i];
                if (tag == null || !tag.IsValid)
                {
                    continue;
                }

                if (!paths.Add(tag.Path))
                {
                    Debug.LogError($"Duplicate GameplayTag path '{tag.Path}' in {name}.", this);
                }

                if (!ids.Add(tag.Id))
                {
                    Debug.LogError($"GameplayTag ID collision for '{tag.Path}' in {name}.", this);
                }

                HashSet<GameplayTag> visited = new();
                for (GameplayTag parent = tag.Parent; parent != null; parent = parent.Parent)
                {
                    if (!visited.Add(parent))
                    {
                        Debug.LogError($"GameplayTag '{tag.Path}' has a cyclic parent hierarchy.", tag);
                        break;
                    }
                }
            }
        }
    }
}
