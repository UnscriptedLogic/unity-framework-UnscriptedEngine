using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UnscriptedEngine.Editor
{
    [FilePath("UserSettings/UnscriptedEngineFavourites.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class FavouritesStore : ScriptableSingleton<FavouritesStore>
    {
        [SerializeField]
        private List<string> assetGuids = new List<string>();

        public IReadOnlyList<string> AssetGuids => assetGuids;

        public void Add(IEnumerable<string> guids)
        {
            bool changed = false;
            foreach (string guid in guids)
            {
                if (string.IsNullOrEmpty(guid) || assetGuids.Contains(guid)) continue;
                assetGuids.Add(guid);
                changed = true;
            }
            if (changed) Save(true);
        }

        public void Remove(string guid)
        {
            if (assetGuids.Remove(guid)) Save(true);
        }
    }
}
