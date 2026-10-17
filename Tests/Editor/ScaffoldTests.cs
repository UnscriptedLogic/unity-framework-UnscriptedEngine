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
        public void ExistingPrefabPreventsAnySourceGeneration()
        {
            var plan = Plan();
            File.WriteAllText(plan.PathFor("P", "prefab"), "Existing asset");
            Assert.Throws<IOException>(() => plan.Validate());
            Assert.False(File.Exists(plan.PathFor("C", "cs")));
            Assert.AreEqual("Existing asset", File.ReadAllText(plan.PathFor("P", "prefab")));
        }

        [Test]
        public void EmptyDefinitionCannotBeWired()
        {
            var definition = ScriptableObject.CreateInstance<UPlayerDefinition>();
            try { Assert.Throws<InvalidOperationException>(() => ScaffoldAssets.ValidateDefinition(definition)); }
            finally { UnityEngine.Object.DestroyImmediate(definition); }
        }

        [Test]
        public void SceneNamesAndPlayerNamesUsePrefixDivider()
        {
            var plan = Plan("Arena");
            CollectionAssert.Contains(plan.OutputPaths(), folder + "/PD_Arena.asset");
            CollectionAssert.Contains(plan.OutputPaths(), folder + "/C_Arena.prefab");
            plan.scene = true;
            CollectionAssert.Contains(plan.OutputPaths(), folder + "/Scripts/GM_Arena.cs");
            CollectionAssert.Contains(plan.OutputPaths(), folder + "/Scripts/NM_Arena.cs");
            CollectionAssert.Contains(plan.OutputPaths(), folder + "/Scenes/S_Arena.unity");
        }
    }
}
