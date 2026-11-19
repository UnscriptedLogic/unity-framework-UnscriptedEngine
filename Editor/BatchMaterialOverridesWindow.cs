using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UnscriptedEngine.Editor
{
    public sealed class BatchMaterialOverridesWindow : EditorWindow
    {
        private sealed class Target
        {
            public string Path;
            public bool IsModel;
            public readonly List<(string Name, Material Material)> Slots =
                new List<(string Name, Material Material)>();

            public bool NeedsReplacement(string sourceName, Material replacement)
            {
                return Slots.Any(slot => slot.Name == sourceName &&
                    (replacement == null || slot.Material != replacement));
            }
        }

        [SerializeField] private GameObject[] selectedAssets = Array.Empty<GameObject>();
        [SerializeField] private string sourceName;
        [SerializeField] private Material replacement;
        private Target[] targets = Array.Empty<Target>();
        private string[] sourceNames = Array.Empty<string>();
        private bool refreshTargets = true;
        private Vector2 scroll;
        private string report;
        private MessageType reportType;

        [MenuItem("Tools/UnscriptedEngine/Materials/Batch Material Overrides")]
        private static void Open()
        {
            GetWindow<BatchMaterialOverridesWindow>("Batch Material Overrides").CaptureSelection();
        }

        private void OnEnable()
        {
            minSize = new Vector2(420f, 270f);
            refreshTargets = true;
        }

        private void CaptureSelection()
        {
            selectedAssets = Selection.GetFiltered<GameObject>(SelectionMode.Assets)
                .Where(AssetDatabase.IsMainAsset)
                .ToArray();
            refreshTargets = true;
            Repaint();
        }

        private void OnProjectChange()
        {
            refreshTargets = true;
            Repaint();
        }

        private void OnGUI()
        {
            if (refreshTargets && Event.current.type == EventType.Layout)
                ReadTargets();

            EditorGUILayout.HelpBox(
                "Models match the imported material name. Prefabs match the assigned material name.",
                MessageType.Info);
            if (GUILayout.Button("Use Project Selection"))
                CaptureSelection();

            using (new EditorGUI.DisabledScope(sourceNames.Length == 0))
            {
                int index = Array.IndexOf(sourceNames, sourceName);
                int selected = EditorGUILayout.Popup("Material to replace", index, sourceNames);
                if (selected >= 0)
                    sourceName = sourceNames[selected];
            }

            replacement = (Material)EditorGUILayout.ObjectField("Replacement", replacement, typeof(Material), false);
            string replacementPath = AssetDatabase.GetAssetPath(replacement);
            Target[] preview = targets.Where(target =>
                target.Path != replacementPath && target.NeedsReplacement(sourceName, replacement)).ToArray();

            EditorGUILayout.LabelField(replacement == null
                ? $"{preview.Length} of {targets.Length} assets match"
                : $"{preview.Length} of {targets.Length} assets will change");
            using (var scrollView = new EditorGUILayout.ScrollViewScope(scroll))
            {
                scroll = scrollView.scrollPosition;
                foreach (Target target in preview)
                    EditorGUILayout.LabelField(new GUIContent(target.Path, target.Path));
            }

            if (!string.IsNullOrEmpty(report))
                EditorGUILayout.HelpBox(report, reportType);

            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(replacementPath) || preview.Length == 0 ||
                EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling))
            {
                if (GUILayout.Button("Apply Material Overrides…"))
                    Apply(preview);
            }
        }

        private void ReadTargets()
        {
            refreshTargets = false;
            targets = selectedAssets
                .Where(asset => asset != null)
                .Select(ReadTarget)
                .Where(target => target != null)
                .OrderBy(target => target.Path, StringComparer.Ordinal)
                .ToArray();

            sourceNames = targets.SelectMany(target => target.Slots)
                .Select(slot => slot.Name)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            if (!sourceNames.Contains(sourceName))
                sourceName = sourceNames.FirstOrDefault();
        }

        private static Target ReadTarget(GameObject asset)
        {
            string path = AssetDatabase.GetAssetPath(asset);
            if (!path.StartsWith("Assets/", StringComparison.Ordinal))
                return null;

            if (AssetImporter.GetAtPath(path) is ModelImporter importer)
            {
                var model = new Target { Path = path, IsModel = true };
                var remaps = importer.GetExternalObjectMap();
                foreach (string name in ReadModelMaterialNames(importer))
                {
                    remaps.TryGetValue(new AssetImporter.SourceAssetIdentifier(typeof(Material), name), out var remap);
                    model.Slots.Add((name, remap as Material));
                }
                return model;
            }

            if (!path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                return null;

            var prefab = new Target { Path = path };
            foreach (Renderer renderer in asset.GetComponentsInChildren<Renderer>(true))
                foreach (Material material in renderer.sharedMaterials)
                    if (material != null)
                        prefab.Slots.Add((material.name, material));
            return prefab;
        }

        private static IEnumerable<string> ReadModelMaterialNames(ModelImporter importer)
        {
            if (importer.materialImportMode == ModelImporterMaterialImportMode.None ||
                importer.materialLocation != ModelImporterMaterialLocation.InPrefab)
                yield break;

            // Unity's Materials inspector reads source identifiers here, even for slots with no remap.
            using var serialized = new SerializedObject(importer);
            SerializedProperty materials = serialized.FindProperty("m_Materials");
            if (materials == null)
                yield break;

            for (int i = 0; i < materials.arraySize; i++)
            {
                string name = materials.GetArrayElementAtIndex(i).FindPropertyRelative("name")?.stringValue;
                if (!string.IsNullOrEmpty(name))
                    yield return name;
            }
        }

        private void Apply(Target[] preview)
        {
            if (!EditorUtility.DisplayDialog("Apply Material Overrides",
                $"Replace \"{sourceName}\" with \"{replacement.name}\" in {preview.Length} assets? " +
                "This saves prefab assets and reimports models. Unity Undo is unavailable.",
                "Apply", "Cancel"))
                return;

            // Edit dependent prefabs before their bases and nested sources change the matched slots.
            var selectedPrefabs = new HashSet<string>(preview.Where(target => !target.IsModel)
                .Select(target => target.Path), StringComparer.Ordinal);
            Target[] ordered = preview.OrderBy(target => target.IsModel)
                .ThenByDescending(target => target.IsModel ? 0 :
                    AssetDatabase.GetDependencies(target.Path).Count(selectedPrefabs.Contains))
                .ToArray();

            int changed = 0;
            var failures = new List<string>();
            try
            {
                for (int i = 0; i < ordered.Length; i++)
                {
                    Target target = ordered[i];
                    EditorUtility.DisplayProgressBar("Applying Material Overrides", target.Path, (float)i / ordered.Length);
                    try
                    {
                        string writePath = target.IsModel ? target.Path + ".meta" : target.Path;
                        if (!AssetDatabase.IsOpenForEdit(writePath) || new FileInfo(writePath).IsReadOnly)
                            throw new InvalidOperationException("Asset needs checkout or is read-only.");
                        if (PrefabStageUtility.GetCurrentPrefabStage()?.assetPath == target.Path)
                            throw new InvalidOperationException("Close Prefab Mode for this asset first.");
                        if (replacement == null)
                            throw new InvalidOperationException("Replacement material is no longer available.");

                        bool saved = target.IsModel
                            ? ApplyModel(target.Path, sourceName, replacement)
                            : ApplyPrefab(target.Path, sourceName, replacement);
                        if (saved)
                            changed++;
                    }
                    catch (Exception exception)
                    {
                        failures.Add($"{target.Path}: {exception.Message}");
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                refreshTargets = true;
            }

            report = $"Last batch updated {changed} of {ordered.Length} assets.";
            reportType = failures.Count == 0 ? MessageType.Info : MessageType.Warning;
            if (failures.Count > 0)
            {
                report += $" {failures.Count} could not be updated. See the Console for details.";
                Debug.LogWarning($"{report}\n{string.Join("\n", failures)}");
            }
        }

        private static bool ApplyModel(string path, string name, Material material)
        {
            if (AssetImporter.GetAtPath(path) is not ModelImporter importer ||
                !ReadModelMaterialNames(importer).Contains(name))
                return false;

            var identifier = new AssetImporter.SourceAssetIdentifier(typeof(Material), name);
            if (importer.GetExternalObjectMap().TryGetValue(identifier, out var current) && current == material)
                return false;

            importer.AddRemap(identifier, material);
            importer.SaveAndReimport();
            if (AssetImporter.GetAtPath(path) is not ModelImporter savedImporter ||
                !savedImporter.GetExternalObjectMap().TryGetValue(identifier, out var saved) || saved != material)
                throw new InvalidOperationException("Unity could not save the model material remap.");
            return true;
        }

        private static bool ApplyPrefab(string path, string name, Material replacement)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                bool changed = false;
                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] materials = renderer.sharedMaterials;
                    bool rendererChanged = false;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        Material current = materials[i];
                        if (current == null || current == replacement || current.name != name)
                            continue;

                        materials[i] = replacement;
                        rendererChanged = true;
                    }

                    if (!rendererChanged)
                        continue;

                    renderer.sharedMaterials = materials;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                    changed = true;
                }

                if (!changed)
                    return false;

                PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
                if (!success)
                    throw new InvalidOperationException("Unity could not save the prefab.");
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
