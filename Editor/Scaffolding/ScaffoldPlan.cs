using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;

namespace UnscriptedEngine.Editor.Scaffolding
{
    [Serializable]
    public sealed class ScaffoldPlan
    {
        public string name;
        public string folder;
        public bool scene;
        public string templateType;
        public string playerDefinitionPath;
        public string targetScenePath;

        public string PathFor(string prefix, string extension)
        {
            string outputFolder = extension == "cs" ? folder + "/Scripts"
                : extension == "unity" ? folder + "/Scenes" : folder;
            return $"{outputFolder}/{prefix}_{name}.{extension}";
        }

        public void EnsureOutputFolders()
        {
            foreach (string outputFolder in OutputPaths().Select(path => path.Substring(0, path.LastIndexOf('/'))).Distinct())
            {
                if (AssetDatabase.IsValidFolder(outputFolder)) continue;
                if (File.Exists(outputFolder) || Directory.Exists(outputFolder)
                    || string.IsNullOrEmpty(AssetDatabase.CreateFolder(folder, System.IO.Path.GetFileName(outputFolder))))
                    throw new IOException("Cannot create output folder: " + outputFolder);
            }
        }
        public string[] Prefixes => scene ? new[] { "GM", "GI", "GS", "NM" } : new[] { "C", "P" };

        public IEnumerable<string> OutputPaths()
        {
            foreach (string prefix in Prefixes) yield return PathFor(prefix, "cs");
            if (scene)
            {
                yield return PathFor("S", "unity");
                yield return PathFor("NP", "asset");
            }
            else
            {
                yield return PathFor("C", "prefab");
                yield return PathFor("P", "prefab");
                yield return PathFor("PD", "asset");
                if (!string.IsNullOrEmpty(targetScenePath)) yield return PathFor("NP", "asset");
            }
        }

        public void Validate()
        {
            if (string.IsNullOrEmpty(name) || !Regex.IsMatch(name, @"^[A-Za-z][A-Za-z0-9_]*$"))
                throw new ArgumentException("Name must start with a letter and contain only letters, digits, and underscores.");
            if (string.IsNullOrEmpty(folder) || (folder != "Assets" && !folder.StartsWith("Assets/", StringComparison.Ordinal))
                || folder.Split('/').Any(part => part == ".." || part == "." || part.Length == 0)
                || folder.Contains('\\') || !AssetDatabase.IsValidFolder(folder))
                throw new ArgumentException("Choose an existing folder inside Assets.");
            if (folder.Split('/').Any(part => part.Equals("Editor", StringComparison.OrdinalIgnoreCase)
                || part.Equals("Resources", StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("Choose a runtime folder outside Editor and Resources.");
            foreach (string path in OutputPaths())
                if (File.Exists(path) || File.Exists(path + ".meta") || Directory.Exists(path))
                    throw new IOException("Output already exists: " + path);
            foreach (string prefix in Prefixes)
            {
                string className = prefix + "_" + name;
                if (AppDomain.CurrentDomain.GetAssemblies().Any(assembly => assembly.GetType(className) != null)
                    || AssetDatabase.FindAssets(className + " t:MonoScript").Any(guid =>
                        System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid)) == className))
                    throw new ArgumentException("Script name already exists: " + className);
            }
        }
    }
}
