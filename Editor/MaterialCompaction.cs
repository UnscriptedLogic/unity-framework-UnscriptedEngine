using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnscriptedEngine.Editor
{
    internal static class MaterialCompaction
    {
        internal sealed class Replacement
        {
            internal Material Source;
            internal Material Target;
        }

        private sealed class Reference
        {
            internal string Guid;
            internal long FileId;
            internal int Type;
        }

        // Match complete Unity object references, including local IDs for embedded materials.
        private static readonly Regex ObjectReference = new Regex(
            @"\{\s*fileID:\s*(-?\d+)\s*,\s*guid:\s*([a-fA-F0-9]{32})\s*,\s*type:\s*\d+\s*\}", RegexOptions.Compiled);

        internal static string SettingsKey(Material material)
        {
            // Preserve every serialized setting except the display name. This is deliberately conservative.
            return Regex.Replace(EditorJsonUtility.ToJson(material), @"""m_Name""\s*:\s*""(?:\\.|[^""\\])*""", "\"m_Name\":\"\"");
        }

        private static Reference GetReference(Material material)
        {
            if (material == null || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(material, out string guid, out long fileId))
                throw new InvalidOperationException("Choose saved material assets, not runtime or built-in materials.");
            return new Reference { Guid = guid, FileId = fileId, Type = AssetDatabase.IsNativeAsset(material) ? 2 : 3 };
        }

        private static Dictionary<string, Reference> BuildTargets(IReadOnlyList<Replacement> replacements)
        {
            Dictionary<string, Reference> targets = new Dictionary<string, Reference>(StringComparer.OrdinalIgnoreCase);
            foreach (Replacement replacement in replacements)
            {
                Reference source = GetReference(replacement.Source);
                Reference target = GetReference(replacement.Target);
                string key = source.Guid + ":" + source.FileId;
                if (key == target.Guid + ":" + target.FileId) throw new InvalidOperationException("Source and target must differ.");
                targets.Add(key, target);
            }
            foreach (Reference target in targets.Values)
                if (targets.ContainsKey(target.Guid + ":" + target.FileId))
                    throw new InvalidOperationException("A replacement target cannot also be a source. Scan again.");
            return targets;
        }

        private static string Rewrite(string text, Dictionary<string, Reference> targets) => ObjectReference.Replace(text, match =>
        {
            string key = match.Groups[2].Value + ":" + match.Groups[1].Value;
            return targets.TryGetValue(key, out Reference target)
                ? $"{{fileID: {target.FileId}, guid: {target.Guid}, type: {target.Type}}}" : match.Value;
        });

        internal static string Apply(IReadOnlyList<Replacement> replacements, bool trashUnusedSources)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Wait for Edit Mode and compilation to finish.");
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
                throw new InvalidOperationException("Close Prefab Mode before replacing project references.");
            Dictionary<string, Reference> targets = BuildTargets(replacements);
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return "Cancelled.";
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isDirty || string.IsNullOrEmpty(scene.path))
                    throw new InvalidOperationException("Save or close all untitled and modified scenes first.");
            }
            AssetDatabase.SaveAssets();
            Dictionary<string, byte[]> changes = new Dictionary<string, byte[]>();
            string[] assetPaths = AssetDatabase.GetAllAssetPaths().Where(path => path.StartsWith("Assets/", StringComparison.Ordinal)
                && File.Exists(path)).ToArray();
            try
            {
                for (int i = 0; i < assetPaths.Length; i++)
                {
                    string path = assetPaths[i];
                    if (EditorUtility.DisplayCancelableProgressBar("Compact Materials", path, (float)i / assetPaths.Length)) return "Cancelled before changing files.";
                    foreach (string file in new[] { path, path + ".meta" })
                    {
                        if (!File.Exists(file)) continue;
                        // Only rewrite Unity YAML, never scripts, JSON, textures or binary model data.
                        using (StreamReader reader = new StreamReader(file, Encoding.UTF8, true))
                        {
                            char[] header = new char[18];
                            string prefix = new string(header, 0, reader.ReadBlock(header, 0, header.Length));
                            if (!prefix.StartsWith("%YAML", StringComparison.Ordinal)
                                && !(file.EndsWith(".meta", StringComparison.Ordinal) && prefix.StartsWith("fileFormatVersion:", StringComparison.Ordinal))) continue;
                        }
                        string original = File.ReadAllText(file);
                        string updated = Rewrite(original, targets);
                        if (updated == original) continue;
                        if (!AssetDatabase.IsOpenForEdit(path) || (File.GetAttributes(file) & FileAttributes.ReadOnly) != 0)
                            throw new IOException("Read-only asset needs checkout: " + file);
                        changes.Add(file, new UTF8Encoding(false).GetBytes(updated));
                    }
                }
            }
            finally { EditorUtility.ClearProgressBar(); }

            string backup = "Library/CompactMaterials/" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + "_" + Guid.NewGuid().ToString("N");
            foreach (string file in changes.Keys) Backup(file, backup);
            SceneSetup[] sceneSetup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                // Unload scenes so stale in-memory references cannot overwrite the rewritten scene files.
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                try
                {
                    foreach (KeyValuePair<string, byte[]> change in changes) File.WriteAllBytes(change.Key, change.Value);
                    AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                }
                catch
                {
                    foreach (string file in changes.Keys) File.Copy(backup + "/" + file, file, true);
                    AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                    throw;
                }
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(sceneSetup); }

            int trashed = 0;
            if (trashUnusedSources)
            {
                HashSet<string> referenced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string path in assetPaths)
                    foreach (string dependency in AssetDatabase.GetDependencies(path, false))
                        if (!string.Equals(path, dependency, StringComparison.OrdinalIgnoreCase)) referenced.Add(dependency);
                foreach (Replacement replacement in replacements)
                {
                    string path = AssetDatabase.GetAssetPath(replacement.Source);
                    if (!AssetDatabase.IsMainAsset(replacement.Source) || !path.StartsWith("Assets/", StringComparison.Ordinal)
                        || !path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase) || referenced.Contains(path)) continue;
                    if (!AssetDatabase.IsOpenForEdit(path)) continue;
                    Backup(path, backup);
                    if (File.Exists(path + ".meta")) Backup(path + ".meta", backup);
                    if (AssetDatabase.MoveAssetToTrash(path)) trashed++;
                }
            }
            return $"Updated {changes.Count} serialized files. Moved {trashed} unused .mat assets to Trash.\nBackup: {Path.GetFullPath(backup)}\nEmbedded materials and sources still referenced by binary/imported assets are retained. Name-based Resources/Addressables loads and external references require manual review.";
        }

        private static void Backup(string path, string root)
        {
            string destination = root + "/" + path;
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            if (!File.Exists(destination)) File.Copy(path, destination);
        }
    }
}
