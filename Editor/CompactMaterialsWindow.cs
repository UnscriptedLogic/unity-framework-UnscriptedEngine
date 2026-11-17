using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UnscriptedEngine.Editor
{
    public sealed class CompactMaterialsWindow : EditorWindow
    {
        private sealed class Group
        {
            internal bool Enabled = true;
            internal Material[] Materials;
            internal string[] Labels;
            internal int Keep;
        }

        [SerializeField]
        private DefaultAsset folder;
        [SerializeField]
        private Material replacement;
        [SerializeField]
        private bool manualReplacement;
        [SerializeField]
        private bool matchName = true;
        [SerializeField]
        private bool trashUnusedSources;
        private readonly List<Group> groups = new List<Group>();
        private Material[] scannedMaterials = Array.Empty<Material>();
        private Vector2 scroll;
        private string report;

        [MenuItem("Tools/UnscriptedEngine/Materials/Compact Materials")]
        [MenuItem("Assets/Compact Materials...")]
        public static void Open() => GetWindow<CompactMaterialsWindow>("Compact Materials");

        private void OnEnable() => minSize = new Vector2(560f, 420f);

        private void OnGUI()
        {
            EditorGUI.BeginChangeCheck();
            manualReplacement = EditorGUILayout.Toggle("Replace with chosen material", manualReplacement);
            if (!manualReplacement) matchName = EditorGUILayout.Toggle("Require same name", matchName);
            if (EditorGUI.EndChangeCheck()) { groups.Clear(); scannedMaterials = Array.Empty<Material>(); report = null; }
            if (manualReplacement)
                replacement = (Material)EditorGUILayout.ObjectField("Replacement", replacement, typeof(Material), false);
            folder = (DefaultAsset)EditorGUILayout.ObjectField(new GUIContent("Scan folder (blank = Assets)", "Folder limits the material candidates, not the project reference replacement scope."), folder, typeof(DefaultAsset), false);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Scan Folder")) Scan(false);
                if (GUILayout.Button("Use Selected Materials / Folders")) Scan(true);
            }
            EditorGUILayout.HelpBox(manualReplacement
                ? "Selected source materials will be replaced by the chosen material, regardless of appearance."
                : "Duplicate groups compare all serialized material settings, including shader, colors, textures, keywords and render settings. Choose one material to keep per group. Similar-looking materials with different settings stay separate.", MessageType.Info);
            EditorGUILayout.HelpBox("Replaces saved Unity YAML references throughout Assets, including scenes, prefabs and importer remaps. Open scenes must be saved and will reload. Binary assets and package contents are not rewritten. File backups go under Library/CompactMaterials. No Unity Undo.", MessageType.None);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            if (manualReplacement)
            {
                foreach (Material material in scannedMaterials)
                    using (new EditorGUI.DisabledScope(true)) EditorGUILayout.ObjectField(material, typeof(Material), false);
            }
            else
            {
                foreach (Group group in groups)
                {
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        group.Enabled = EditorGUILayout.ToggleLeft($"{group.Materials[0]?.name} ({group.Materials.Length} materials)", group.Enabled);
                        group.Keep = EditorGUILayout.Popup("Keep", group.Keep, group.Labels);
                        using (new EditorGUI.DisabledScope(true))
                            foreach (Material material in group.Materials) EditorGUILayout.ObjectField(material, typeof(Material), false);
                    }
                }
            }
            EditorGUILayout.EndScrollView();
            trashUnusedSources = EditorGUILayout.Toggle(new GUIContent("Trash unused source .mat assets", "Off by default. Only standalone .mat files with no remaining asset dependencies can be removed. Embedded model materials are retained."), trashUnusedSources);
            if (trashUnusedSources)
                EditorGUILayout.HelpBox("Cleanup cannot detect name-based Resources/Addressables loads or references outside Assets. Leave off if materials are loaded by name or used by packages.", MessageType.Warning);
            if (!string.IsNullOrEmpty(report)) EditorGUILayout.HelpBox(report, MessageType.Info);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling
                || (manualReplacement ? replacement == null || scannedMaterials.Length == 0 : groups.All(group => !group.Enabled))))
            {
                if (GUILayout.Button("Apply Material Replacements…")) Apply();
            }
        }

        private void Scan(bool selectionOnly)
        {
            try
            {
                HashSet<Material> materials = new HashSet<Material>();
                List<string> roots = new List<string>();
                if (selectionOnly)
                {
                    foreach (UnityEngine.Object selected in Selection.objects)
                    {
                        if (selected is Material material && AssetDatabase.Contains(material)) materials.Add(material);
                        string path = AssetDatabase.GetAssetPath(selected);
                        if (AssetDatabase.IsValidFolder(path)) roots.Add(path);
                        else if (selected is GameObject && !string.IsNullOrEmpty(path))
                            foreach (Material embedded in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>()) materials.Add(embedded);
                    }
                }
                else
                {
                    string root = folder == null ? "Assets" : AssetDatabase.GetAssetPath(folder);
                    if (!AssetDatabase.IsValidFolder(root)) throw new InvalidOperationException("Choose a folder to scan.");
                    roots.Add(root);
                }
                if (roots.Count > 0)
                {
                    string[] paths = AssetDatabase.FindAssets("t:Material", roots.ToArray())
                        .Concat(AssetDatabase.FindAssets("t:Model", roots.ToArray()))
                        .Select(AssetDatabase.GUIDToAssetPath).Distinct().ToArray();
                    for (int i = 0; i < paths.Length; i++)
                    {
                        if (EditorUtility.DisplayCancelableProgressBar("Scan Materials", paths[i], (float)i / paths.Length)) return;
                        foreach (Material material in AssetDatabase.LoadAllAssetsAtPath(paths[i]).OfType<Material>()) materials.Add(material);
                    }
                }
                scannedMaterials = materials.Where(material => material != null && material.shader != null)
                    .OrderBy(material => AssetDatabase.IsMainAsset(material) ? 0 : 1)
                    .ThenBy(AssetDatabase.GetAssetPath).ThenBy(material => material.name).ToArray();
                groups.Clear();
                if (!manualReplacement)
                {
                    foreach (IGrouping<string, Material> group in scannedMaterials.GroupBy(material =>
                        (matchName ? material.name : "") + "\n" + MaterialCompaction.SettingsKey(material)))
                    {
                        Material[] duplicates = group.ToArray();
                        if (duplicates.Length < 2) continue;
                        groups.Add(new Group
                        {
                            Materials = duplicates,
                            Labels = duplicates.Select(material => AssetDatabase.GetAssetPath(material) + " / " + material.name
                                + " [" + Array.IndexOf(duplicates, material) + "]").ToArray()
                        });
                    }
                }
                report = $"Scanned {scannedMaterials.Length} materials. Found {groups.Count} duplicate groups.";
            }
            catch (Exception exception) { report = exception.Message; }
            finally { EditorUtility.ClearProgressBar(); }
        }

        private void Apply()
        {
            try
            {
                List<MaterialCompaction.Replacement> replacements = new List<MaterialCompaction.Replacement>();
                if (manualReplacement)
                {
                    foreach (Material material in scannedMaterials)
                        if (material != replacement) replacements.Add(new MaterialCompaction.Replacement { Source = material, Target = replacement });
                }
                else
                {
                    foreach (Group group in groups.Where(group => group.Enabled))
                    {
                        Material target = group.Materials[group.Keep];
                        if (target == null) throw new InvalidOperationException("A material was deleted. Scan again.");
                        string key = MaterialCompaction.SettingsKey(target);
                        foreach (Material material in group.Materials)
                        {
                            if (material == null || MaterialCompaction.SettingsKey(material) != key
                                || (matchName && material.name != target.name))
                                throw new InvalidOperationException("Material settings changed after scanning. Scan again.");
                            if (material != target) replacements.Add(new MaterialCompaction.Replacement { Source = material, Target = target });
                        }
                    }
                }
                if (replacements.Count == 0) { report = "No replacements to apply."; return; }
                if (!EditorUtility.DisplayDialog("Compact Materials", $"Replace references to {replacements.Count} materials throughout Assets? Open scenes will reload."
                    + (trashUnusedSources ? " Unused standalone source materials will move to Trash." : " Source material assets will be kept.")
                    + " File backups will be created. This operation has no Unity Undo.", "Apply", "Cancel")) return;
                report = MaterialCompaction.Apply(replacements, trashUnusedSources);
                groups.Clear();
                scannedMaterials = Array.Empty<Material>();
            }
            catch (Exception exception) { report = exception.Message; Debug.LogException(exception); }
        }
    }
}
