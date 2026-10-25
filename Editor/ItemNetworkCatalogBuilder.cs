using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnscriptedEngine.Item;

namespace UnscriptedEngine.Editor
{
    [InitializeOnLoad]
    public sealed class ItemNetworkCatalogBuilder : AssetPostprocessor, IPreprocessBuildWithReport
    {
        private const string CatalogPath = "Assets/Resources/UnscriptedItemCatalog.asset";
        private static bool queued;
        static ItemNetworkCatalogBuilder()
        {
            Queue();
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingEditMode) Rebuild();
            };
        }
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] previous)
        {
            if (imported.Concat(deleted).Concat(moved).Any(path => path.EndsWith(".asset") && path != CatalogPath)) Queue();
        }
        private static void Queue()
        {
            if (queued) return;
            queued = true;
            EditorApplication.delayCall += () => { queued = false; if (!EditorApplication.isPlaying) Rebuild(); };
        }
        public int callbackOrder => 0;
        public void OnPreprocessBuild(BuildReport report) => Rebuild();
        [MenuItem("Unscripted Engine/Items/Rebuild Network Catalog")]
        public static void Rebuild()
        {
            var items = AssetDatabase.FindAssets("t:ItemDataSO").OrderBy(id => id)
                .Select(id => AssetDatabase.LoadAssetAtPath<ItemDataSO>(AssetDatabase.GUIDToAssetPath(id)))
                .Where(item => item != null).ToArray();
            foreach (var item in items)
            {
                var serialized = new SerializedObject(item);
                var id = serialized.FindProperty("networkId");
                string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(item));
                if (id.stringValue != guid) { id.stringValue = guid; serialized.ApplyModifiedPropertiesWithoutUndo(); }
            }
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            var catalog = AssetDatabase.LoadAssetAtPath<ItemNetworkCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<ItemNetworkCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            var data = new SerializedObject(catalog);
            var list = data.FindProperty("items");
            bool changed = list.arraySize != items.Length;
            if (!changed)
                for (int i = 0; i < items.Length; i++)
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue != items[i]) { changed = true; break; }
            if (changed)
            {
                list.arraySize = items.Length;
                for (int i = 0; i < items.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            AssetDatabase.SaveAssetIfDirty(catalog);
            foreach (var item in items) AssetDatabase.SaveAssetIfDirty(item);
        }
    }
}
