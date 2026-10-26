using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using Unity.Netcode;
using UnityEngine;
using UnscriptedEngine.Item;

namespace UnscriptedEngine.Editor.Scaffolding
{
    [Serializable]
    public sealed class ItemScaffoldPlan
    {
        public string name = "NewItem";
        public string folder = "Assets";
        public string displayName = "New Item";
        public int stackSize = 1;
        public int pickupQuantity = 1;
        public WorldItemInteraction defaultBehaviour = WorldItemInteraction.PickUp;
        public string modelGuid = "";
        public const string GeneratedNamespace = "Game.Items";
        public static readonly string[] ScriptSuffixes = { "Behaviour", "Fragment", "Visual", "WorldItem" };

        public string ScriptFolder => $"{folder}/Scripts/Items/{name}";
        public string PrefabFolder => $"{folder}/Prefabs/Items/{name}";
        public string DataPath => $"{folder}/Data/Items/{name}.asset";
        public string HeldPath => $"{PrefabFolder}/{name}Held.prefab";
        public string WorldPath => $"{PrefabFolder}/{name}World.prefab";
        public string ScriptPath(string suffix) => $"{ScriptFolder}/{name}{suffix}.cs";
        public string TypeName(string suffix) => $"{GeneratedNamespace}.{name}{suffix}";
        public IEnumerable<string> AssetPaths => new[] { DataPath, HeldPath, WorldPath };
        public IEnumerable<string> OutputPaths => ScriptSuffixes.Select(ScriptPath).Concat(AssetPaths);

        public void Validate()
        {
            if (string.IsNullOrEmpty(name) || !Regex.IsMatch(name, @"^[A-Za-z][A-Za-z0-9_]*$")
                || Regex.IsMatch(name, @"^(CON|PRN|AUX|NUL|COM[0-9]|LPT[0-9]|Editor|Resources|StreamingAssets)$", RegexOptions.IgnoreCase))
                throw new ArgumentException("Item name must start with a letter, use only letters/digits/underscores, and not be a reserved filename.");
            if (string.IsNullOrEmpty(folder) || (folder != "Assets" && !folder.StartsWith("Assets/", StringComparison.Ordinal))
                || folder.Contains('\\') || folder.Split('/').Any(p => p == "." || p == ".." || p.Length == 0)
                || !AssetDatabase.IsValidFolder(folder))
                throw new ArgumentException("Choose an existing root folder inside Assets.");
            if (folder.Split('/').Any(p => p.Equals("Editor", StringComparison.OrdinalIgnoreCase)
                || p.Equals("Resources", StringComparison.OrdinalIgnoreCase)
                || p.Equals("StreamingAssets", StringComparison.OrdinalIgnoreCase) || p.EndsWith("~", StringComparison.Ordinal)))
                throw new ArgumentException("Choose a runtime root outside Editor, Resources, StreamingAssets, and ignored folders.");
            if (stackSize < 1 || pickupQuantity < 1)
                throw new ArgumentException("Stack size and pickup quantity must be at least one.");
            if (!Enum.IsDefined(typeof(WorldItemInteraction), defaultBehaviour))
                throw new ArgumentException("Choose a supported default interaction behaviour.");

            ResolveModel();
        }

        public GameObject ResolveModel()
        {
            if (string.IsNullOrEmpty(modelGuid)) return null;

            string path = AssetDatabase.GUIDToAssetPath(modelGuid);
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (model == null || !AssetDatabase.IsMainAsset(model) || !PrefabUtility.IsPartOfPrefabAsset(model))
                throw new ArgumentException("Model override is missing or invalid. Choose a prefab or imported model asset, or clear the override to use a cube.");

            if (model.GetComponentInChildren<NetworkObject>(true) != null
                || model.GetComponentInChildren<NetworkBehaviour>(true) != null)
                throw new ArgumentException("Choose an artwork prefab or model without network components. The creator adds networking to the world item root.");

            return model;
        }

        public void ValidateNew()
        {
            Validate();
            RequireAbsent(OutputPaths);
            foreach (string suffix in ScriptSuffixes)
            {
                string className = name + suffix;
                if (AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetType(TypeName(suffix)) != null)
                    || AssetDatabase.FindAssets(className + " t:MonoScript").Any(guid =>
                        Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid))
                            .Equals(className, StringComparison.OrdinalIgnoreCase)))
                    throw new ArgumentException("An item script already uses this name: " + className);
            }
        }

        public static void RequireAbsent(IEnumerable<string> paths)
        {
            foreach (string path in paths)
                if (File.Exists(path) || File.Exists(path + ".meta") || Directory.Exists(path))
                    throw new IOException("Output already exists; choose another item name or root: " + path);
        }

        public void EnsureFolders()
        {
            Validate();
            foreach (string path in OutputPaths.Select(Path.GetDirectoryName).Distinct())
                EnsureFolder(path.Replace('\\', '/'));
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            if (File.Exists(path) || File.Exists(path + ".meta") || Directory.Exists(path))
                throw new IOException("Output folder is occupied or not imported: " + path);
            int split = path.LastIndexOf('/');
            if (split < 0) throw new IOException("Invalid asset folder: " + path);
            string parent = path.Substring(0, split);
            EnsureFolder(parent);
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, path.Substring(split + 1))))
                throw new IOException("Cannot create folder: " + path);
        }
    }
}
