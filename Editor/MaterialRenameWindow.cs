using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UnscriptedEngine.Editor
{
    public sealed class MaterialRenameWindow : EditorWindow
    {
        private const string ContextMenuPath = "Assets/Rename Selected Materials...";

        [SerializeField]
        private Material[] materials = Array.Empty<Material>();
        [SerializeField]
        private string prefix = "";
        [SerializeField]
        private string suffix = "";
        [SerializeField]
        private string find = "";
        [SerializeField]
        private string replacement = "";
        [SerializeField]
        private bool capitalizeFirst;
        private Vector2 scroll;
        private string result;

        [MenuItem(ContextMenuPath)]
        [MenuItem("Tools/UnscriptedEngine/Materials/Rename Selected Materials...")]
        private static void Open()
        {
            MaterialRenameWindow window = GetWindow<MaterialRenameWindow>("Rename Materials");
            window.minSize = new Vector2(520f, 360f);
            window.CaptureSelection();
            window.Show();
        }

        [MenuItem(ContextMenuPath, true)]
        private static bool CanOpen() => GetSelectedMaterials().Length > 0;

        private static Material[] GetSelectedMaterials() => Selection.objects.OfType<Material>()
            .Where(material => AssetDatabase.IsMainAsset(material)
                && AssetDatabase.GetAssetPath(material).StartsWith("Assets/", StringComparison.Ordinal)
                && Path.GetExtension(AssetDatabase.GetAssetPath(material)).Equals(".mat", StringComparison.OrdinalIgnoreCase))
            .Distinct().OrderBy(material => AssetDatabase.GetAssetPath(material)).ToArray();

        private void CaptureSelection()
        {
            materials = GetSelectedMaterials();
            result = null;
        }

        private string GetNewName(string current)
        {
            string renamed = string.IsNullOrEmpty(find) ? current : current.Replace(find, replacement);
            if (capitalizeFirst && renamed.Length > 0)
                renamed = char.ToUpperInvariant(renamed[0]) + renamed.Substring(1);
            return prefix + renamed + suffix;
        }

        private void OnGUI()
        {
            if (GUILayout.Button("Use Selected Materials")) CaptureSelection();
            EditorGUILayout.LabelField($"{materials.Length} material(s)", EditorStyles.boldLabel);
            prefix = EditorGUILayout.TextField("Prefix", prefix);
            suffix = EditorGUILayout.TextField("Suffix", suffix);
            find = EditorGUILayout.TextField("Find (case sensitive)", find);
            replacement = EditorGUILayout.TextField("Replace with", replacement);
            capitalizeFirst = EditorGUILayout.Toggle("Capitalize first letter", capitalizeFirst);
            EditorGUILayout.HelpBox("Order: replace, capitalize first letter, then prefix/suffix. Preview uses filenames without .mat. Embedded and package materials are excluded. Asset references are preserved. Renaming does not support Unity Undo.", MessageType.Info);

            string error = ValidateNames(out int changes);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (Material material in materials)
            {
                if (material == null) continue;
                string path = AssetDatabase.GetAssetPath(material);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(new GUIContent(Path.GetFileNameWithoutExtension(path), path));
                    EditorGUILayout.LabelField(GetNewName(Path.GetFileNameWithoutExtension(path)));
                }
            }
            EditorGUILayout.EndScrollView();
            if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Warning);
            if (!string.IsNullOrEmpty(result)) EditorGUILayout.HelpBox(result, MessageType.Info);
            using (new EditorGUI.DisabledScope(error != null || changes == 0 || EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button($"Rename {changes} Material(s)")) RenameMaterials();
            }
        }

        private string ValidateNames(out int changes)
        {
            changes = 0;
            HashSet<string> destinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Material material in materials)
            {
                if (material == null) return "A captured material is missing. Select materials again.";
                string path = AssetDatabase.GetAssetPath(material);
                if (!path.StartsWith("Assets/", StringComparison.Ordinal) || !AssetDatabase.IsMainAsset(material))
                    return "Only standalone material assets inside Assets can be renamed.";
                string current = Path.GetFileNameWithoutExtension(path);
                string renamed = GetNewName(current);
                if (string.IsNullOrWhiteSpace(renamed) || renamed != renamed.Trim()
                    || renamed.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                    || renamed.EndsWith(".", StringComparison.Ordinal)
                    || renamed.EndsWith(".mat", StringComparison.OrdinalIgnoreCase)
                    || System.Text.RegularExpressions.Regex.IsMatch(renamed,
                        @"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(\.|$)", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                    return "Invalid filename: " + renamed + ". Use a valid name without the .mat extension.";
                string destination = Path.GetDirectoryName(path).Replace('\\', '/') + "/" + renamed + Path.GetExtension(path);
                if (!destinations.Add(destination)) return "Multiple materials would have the same path: " + destination;
                bool samePath = string.Equals(path, destination, StringComparison.OrdinalIgnoreCase);
                if (!samePath && (File.Exists(destination) || Directory.Exists(destination) || File.Exists(destination + ".meta")
                    || !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(destination))))
                    return "Destination already exists: " + destination;
                if (current == renamed) continue;
                if (!AssetDatabase.IsOpenForEdit(path)) return "Material is read-only or needs checkout: " + path;
                changes++;
            }
            return null;
        }

        private void RenameMaterials()
        {
            string error = ValidateNames(out int changes);
            if (error != null) { result = error; return; }
            int renamedCount = 0;
            foreach (Material material in materials)
            {
                string path = AssetDatabase.GetAssetPath(material);
                string current = Path.GetFileNameWithoutExtension(path);
                string renamed = GetNewName(current);
                if (current == renamed) continue;
                // A temporary name makes case-only changes reliable on case-insensitive filesystems.
                if (current.Equals(renamed, StringComparison.OrdinalIgnoreCase))
                {
                    string temporaryPath = AssetDatabase.GenerateUniqueAssetPath(Path.GetDirectoryName(path).Replace('\\', '/')
                        + "/MaterialRename_" + Guid.NewGuid().ToString("N") + ".mat");
                    error = AssetDatabase.RenameAsset(path, Path.GetFileNameWithoutExtension(temporaryPath));
                    if (string.IsNullOrEmpty(error))
                    {
                        error = AssetDatabase.RenameAsset(temporaryPath, renamed);
                        if (!string.IsNullOrEmpty(error))
                            error += " Restore: " + AssetDatabase.RenameAsset(temporaryPath, current);
                    }
                }
                else error = AssetDatabase.RenameAsset(path, renamed);
                if (!string.IsNullOrEmpty(error))
                {
                    result = $"Renamed {renamedCount} of {changes}. Stopped: {error}";
                    return;
                }
                renamedCount++;
            }
            AssetDatabase.SaveAssets();
            result = $"Renamed {renamedCount} material(s).";
            // Clear operations after success so clicking again cannot accidentally repeat affixes.
            prefix = suffix = find = replacement = "";
            capitalizeFirst = false;
        }
    }
}
