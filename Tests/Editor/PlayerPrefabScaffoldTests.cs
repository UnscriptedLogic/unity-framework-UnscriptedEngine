using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnscriptedEngine.Editor.Scaffolding;

namespace UnscriptedEngine.Tests
{
    public class PlayerPrefabScaffoldTests
    {
        [Test]
        public void ClonePreservesInheritedFieldsAndRemapsHierarchyReferences()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var source = new GameObject("Source");
            source.SetActive(false);
            var original = source.AddComponent<TestScaffoldPawn>();
            var camera = new GameObject("Camera target");
            camera.transform.SetParent(source.transform);
            original.cameraTarget = camera.transform;
            original.pawnReference = original;
            GameObject clone = null;
            try
            {
                clone = PlayerPrefabScaffold.Clone(source, typeof(TestScaffoldPawn), typeof(TestScaffoldPawnDerived), "P_Test", scene);
                var pawn = clone.GetComponent<TestScaffoldPawnDerived>();
                Assert.NotNull(pawn);
                Assert.AreEqual(1, clone.GetComponents<UPawn>().Length);
                Assert.AreEqual(7.5f, pawn.Speed);
                Assert.AreSame(pawn, pawn.pawnReference);
                Assert.AreEqual(clone.transform, pawn.cameraTarget.parent);
                Assert.AreNotSame(camera.transform, pawn.cameraTarget);
                Assert.AreSame(original, original.pawnReference);
                Assert.AreEqual("Source", source.name);
            }
            finally
            {
                if (clone != null) Object.DestroyImmediate(clone);
                Object.DestroyImmediate(source);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
