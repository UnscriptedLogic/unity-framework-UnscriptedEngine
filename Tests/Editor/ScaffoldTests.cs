using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnscriptedEngine.Editor.Scaffolding;

namespace UnscriptedEngine.Tests
{
    public class ScaffoldTests
    {
        private string folder;

        [SetUp]
        public void SetUp()
        {
            folder = "Assets/ScaffoldTests_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
        }

        [TearDown]
        public void TearDown() { AssetDatabase.DeleteAsset(folder); }

        private ScaffoldPlan Plan(string name = "TestPlayer") => new ScaffoldPlan
        {
            name = name,
            folder = folder,
            templateType = typeof(BlankPlayerScaffoldTemplate).AssemblyQualifiedName
        };

        [TestCase("")]
        [TestCase("123Player")]
        [TestCase("../Player")]
        [TestCase("Player Name")]
        [TestCase("Player;class")]
        public void InvalidNamesCannotWriteSource(string name)
        {
            Assert.Throws<ArgumentException>(() => Plan(name).Validate());
        }

        [TestCase("Packages")]
        [TestCase("Assets/../Packages")]
        [TestCase("Assets\\Other")]
        public void InvalidFoldersAreRejected(string path)
        {
            var plan = Plan();
            plan.folder = path;
            Assert.Throws<ArgumentException>(() => plan.Validate());
        }

        [TestCase("Editor")]
        [TestCase("Resources")]
        public void RuntimeOutputRejectsSpecialFolders(string specialFolder)
        {
            AssetDatabase.CreateFolder(folder, specialFolder);
            var plan = Plan();
            plan.folder += "/" + specialFolder;
            Assert.Throws<ArgumentException>(() => plan.Validate());
        }

        [Test]
        public void ExistingPrefabIsAllowedForConflictResolutionWithoutChangingContent()
        {
            var plan = Plan();
            plan.EnsureOutputFolders();
            File.WriteAllText(plan.PathFor("P", "prefab"), "Existing asset");
            Assert.DoesNotThrow(() => plan.Validate());
            Assert.False(File.Exists(plan.PathFor("C", "cs")));
            Assert.AreEqual("Existing asset", File.ReadAllText(plan.PathFor("P", "prefab")));
        }

        [Test]
        public void ConflictDecisionsSurviveCompilationHandoff()
        {
            var plan = Plan();
            plan.skippedPaths = new[] { plan.PathFor("C", "cs") };
            plan.replacedPaths = new[] { plan.PathFor("P", "prefab") };
            var restored = JsonUtility.FromJson<ScaffoldPlan>(JsonUtility.ToJson(plan));
            Assert.True(restored.ShouldSkip(plan.PathFor("C", "cs")));
            Assert.True(restored.ShouldReplace(plan.PathFor("P", "prefab")));
            Assert.False(restored.ShouldReplace(plan.PathFor("PD", "asset")));
        }

        [Test]
        public void DirectoryAtOutputFilePathCannotBeReplaced()
        {
            var plan = Plan();
            Directory.CreateDirectory(plan.PathFor("P", "prefab"));
            Assert.Throws<IOException>(() => plan.Validate());
        }

        [Test]
        public void EmptyDefinitionCannotBeWired()
        {
            var definition = ScriptableObject.CreateInstance<UPlayerDefinition>();
            try { Assert.Throws<InvalidOperationException>(() => ScaffoldAssets.ValidateDefinition(definition)); }
            finally { UnityEngine.Object.DestroyImmediate(definition); }
        }

        [TestCase(UIScaffoldKind.Canvas, "Scripts/UI", "Prefabs/UI", "UC")]
        [TestCase(UIScaffoldKind.Widget, "Scripts/UI/Widgets", "Prefabs/UI/Components", "UW")]
        public void UIFoldersAreCreatedRecursivelyAndReused(UIScaffoldKind kind, string scripts, string prefabs, string prefix)
        {
            var plan = Plan("Menu");
            plan.ui = kind;
            plan.EnsureOutputFolders();
            Assert.True(AssetDatabase.IsValidFolder(folder + "/" + scripts));
            Assert.True(AssetDatabase.IsValidFolder(folder + "/" + prefabs));
            string guid = AssetDatabase.AssetPathToGUID(folder + "/" + prefabs);
            plan.EnsureOutputFolders();
            Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(folder + "/" + prefabs));
            CollectionAssert.AreEquivalent(new[] { folder + "/" + scripts + "/" + prefix + "_Menu.cs",
                folder + "/" + prefabs + "/" + prefix + "_Menu.prefab" }, plan.OutputPaths());
        }

        [Test]
        public void SceneNamesAndPlayerNamesUsePrefixDivider()
        {
            var plan = Plan("Arena");
            CollectionAssert.Contains(plan.OutputPaths(), folder + "/PD_Arena.asset");
            CollectionAssert.Contains(plan.OutputPaths(), folder + "/Prefabs/C_Arena.prefab");
            plan.scene = true;
            CollectionAssert.Contains(plan.OutputPaths(), folder + "/Scripts/GM_Arena.cs");
            CollectionAssert.Contains(plan.OutputPaths(), folder + "/Scripts/NM_Arena.cs");
            CollectionAssert.Contains(plan.OutputPaths(), folder + "/Scenes/S_Arena.unity");
        }
    }
}
