using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;

namespace UnscriptedEngine.Editor.Scaffolding
{
    public enum UIScaffoldKind { None, Canvas, Widget }

    [Serializable]
    public sealed class ScaffoldPlan
    {
        public string name;
        public string folder;
        public bool scene;
        public UIScaffoldKind ui;
        public string templateType;
        public string playerDefinitionPath;
        public string targetScenePath;
        public string[] skippedPaths = Array.Empty<string>();
        public string[] replacedPaths = Array.Empty<string>();

        public bool ShouldSkip(string path) => skippedPaths != null && skippedPaths.Contains(path);
        public bool ShouldReplace(string path) => replacedPaths != null && replacedPaths.Contains(path);

        public bool ConfirmConflicts(IEnumerable<string> paths)
        {
            string[] conflicts = paths.Where(path => !ShouldSkip(path) && !ShouldReplace(path)
                && (File.Exists(path) || File.Exists(path + ".meta"))).ToArray();
            if (conflicts.Length == 0) return true;
            int choice = EditorUtility.DisplayDialogComplex("Existing scaffold files",
                "These output files already exist:\n\n" + string.Join("\n", conflicts)
                + "\n\nSkip keeps existing files and reuses them where needed. Replace overwrites these files, including custom code and scene contents. Missing files are generated in either case.",
                "Skip Existing", "Cancel", "Replace Existing");
            if (choice == 1) return false;
            if (choice == 0) skippedPaths = (skippedPaths ?? Array.Empty<string>()).Concat(conflicts).ToArray();
            else replacedPaths = (replacedPaths ?? Array.Empty<string>()).Concat(conflicts).ToArray();
            return true;
        }

        public string PathFor(string prefix, string extension)
        {
            if (ui != UIScaffoldKind.None)
            {
                string uiFolder = extension == "cs"
                    ? "/Scripts/UI" + (ui == UIScaffoldKind.Widget ? "/Widgets" : "")
                    : "/Prefabs/UI" + (ui == UIScaffoldKind.Widget ? "/Components" : "");
                return $"{folder}{uiFolder}/{prefix}_{name}.{extension}";
            }
            string outputFolder = extension == "cs" ? folder + "/Scripts"
                : extension == "unity" ? folder + "/Scenes"
                : extension == "prefab" ? folder + "/Prefabs" : folder;
            return $"{outputFolder}/{prefix}_{name}.{extension}";
        }

        public void EnsureOutputFolders()
        {
            foreach (string outputFolder in OutputPaths().Select(path => path.Substring(0, path.LastIndexOf('/'))).Distinct())
            {
                EnsureFolder(outputFolder);
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            if (File.Exists(path) || Directory.Exists(path)) throw new IOException("Cannot create output folder: " + path);
            int divider = path.LastIndexOf('/');
            if (divider < 0) throw new IOException("Invalid output folder: " + path);
            string parent = path.Substring(0, divider);
            EnsureFolder(parent);
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, path.Substring(divider + 1))))
                throw new IOException("Cannot create output folder: " + path);
        }

        public string[] Prefixes => ui != UIScaffoldKind.None ? new[] { ui == UIScaffoldKind.Canvas ? "UC" : "UW" }
            : scene ? new[] { "GM", "GI", "GS", "NM" } : new[] { "C", "P" };

        public IEnumerable<string> OutputPaths()
        {
            foreach (string prefix in Prefixes) yield return PathFor(prefix, "cs");
            if (ui != UIScaffoldKind.None)
            {
                yield return PathFor(Prefixes[0], "prefab");
                yield break;
            }
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
                if (Directory.Exists(path))
                    throw new IOException("A folder occupies an output file path: " + path);
            foreach (string prefix in Prefixes)
            {
                string className = prefix + "_" + name;
                string expectedPath = PathFor(prefix, "cs");
                var existingScript = AssetDatabase.LoadAssetAtPath<MonoScript>(expectedPath);
                if (AppDomain.CurrentDomain.GetAssemblies().Any(assembly => assembly.GetType(className) != null
                        && (existingScript == null || assembly.GetType(className) != existingScript.GetClass()))
                    || AssetDatabase.FindAssets(className + " t:MonoScript").Any(guid =>
                        System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid)) == className
                        && !string.Equals(AssetDatabase.GUIDToAssetPath(guid), expectedPath, StringComparison.OrdinalIgnoreCase)))
                    throw new ArgumentException("Script name already exists outside the target output: " + className);
            }
        }
    }
}
