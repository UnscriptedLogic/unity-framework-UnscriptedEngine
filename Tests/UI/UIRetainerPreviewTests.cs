using System;
using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnscriptedEngine.UI;
using UnscriptedEngine.UI.Editor;
using Object = UnityEngine.Object;

namespace UnscriptedEngine.Tests
{
    public sealed class UIRetainerPreviewTests
    {
        private sealed class Fixture : IDisposable
        {
            internal readonly GameObject Root;
            internal readonly UIRetainer Retainer;
            internal readonly Image Content;
            internal readonly Material Effect;
            private readonly Scene scene;

            internal Fixture()
            {
                scene = EditorSceneManager.NewPreviewScene();
                Root = new GameObject("Authored UI", typeof(RectTransform), typeof(Canvas));
                SceneManager.MoveGameObjectToScene(Root, scene);
                Root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                ((RectTransform)Root.transform).sizeDelta = new Vector2(128f, 64f);
                GameObject panel = new GameObject("Retainer", typeof(RectTransform), typeof(RawImage), typeof(UIRetainer));
                panel.transform.SetParent(Root.transform, false);
                ((RectTransform)panel.transform).sizeDelta = new Vector2(128f, 64f);
                Retainer = panel.GetComponent<UIRetainer>();
                GameObject content = new GameObject("Content", typeof(RectTransform), typeof(Image));
                content.transform.SetParent(panel.transform, false);
                RectTransform rect = (RectTransform)content.transform;
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                Content = content.GetComponent<Image>();
                Content.color = new Color(0.1f, 0.8f, 0.3f, 0.5f);
                Effect = new Material(Shader.Find("UnscriptedEngine/UI/Retainer Warp"));
                SerializedObject settings = new SerializedObject(Retainer);
                settings.FindProperty("content").objectReferenceValue = rect;
                settings.FindProperty("effectMaterial").objectReferenceValue = Effect;
                settings.ApplyModifiedPropertiesWithoutUndo();
            }

            public void Dispose()
            {
                if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
                if (Effect != null && !EditorUtility.IsPersistent(Effect)) Object.DestroyImmediate(Effect);
            }
        }

        private static Color ReadCenter(RenderTexture texture)
        {
            Texture2D pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false, true);
            RenderTexture previous = RenderTexture.active;
            try
            {
                RenderTexture.active = texture;
                pixel.ReadPixels(new Rect(texture.width / 2, texture.height / 2, 1f, 1f), 0, 0);
                pixel.Apply();
                return pixel.GetPixel(0, 0);
            }
            finally { RenderTexture.active = previous; Object.DestroyImmediate(pixel); }
        }

        [Test]
        public void CaptureLeavesAuthoredObjectsUntouchedAndSuppressesAppBehaviors()
        {
            using (Fixture fixture = new Fixture())
            {
                RetainerPreviewProbe.ResetCount();
                fixture.Content.gameObject.AddComponent<RetainerPreviewProbe>();
                int enables = RetainerPreviewProbe.EnableCount;
                int previewScenes = EditorSceneManager.previewSceneCount;
                RawImage display = fixture.Retainer.GetComponent<RawImage>();
                Texture originalTexture = display.texture;
                Material originalMaterial = display.material;
                string originalContent = EditorJsonUtility.ToJson(fixture.Content);
                Transform originalParent = fixture.Content.transform.parent;
                using (UIRetainerPreviewSession session = new UIRetainerPreviewSession(fixture.Retainer))
                {
                    Assert.That(session.UpdatePreview(new Vector2(128f, 64f)), Is.True);
                    Assert.That(session.Error, Is.Null);
                    Assert.That(EditorSceneManager.previewSceneCount, Is.EqualTo(previewScenes + 1));
                    Color captured = ReadCenter(session.Texture);
                    Assert.That(captured.a, Is.EqualTo(0.5f).Within(0.03f));
                    Assert.That(captured.g, Is.GreaterThan(captured.r));
                    Assert.That(RetainerPreviewProbe.EnableCount, Is.EqualTo(enables), "Copied app scripts must not activate.");
                    Assert.That(fixture.Content.transform.parent, Is.SameAs(originalParent));
                    Assert.That(EditorJsonUtility.ToJson(fixture.Content), Is.EqualTo(originalContent));
                    Assert.That(display.texture, Is.SameAs(originalTexture));
                    Assert.That(display.material, Is.SameAs(originalMaterial));
                    Assert.That(fixture.Retainer.Texture, Is.Null);
                }
                Assert.That(EditorSceneManager.previewSceneCount, Is.EqualTo(previewScenes));
            }
        }

        [Test]
        public void SliderDrivenFillIsVisibleWithoutInvokingCallbacks()
        {
            using (Fixture fixture = new Fixture())
            {
                fixture.Content.color = Color.black;
                GameObject fillObject = new GameObject("Driven fill", typeof(RectTransform), typeof(Image));
                fillObject.transform.SetParent(fixture.Content.transform, false);
                Image fill = fillObject.GetComponent<Image>();
                fill.color = Color.green;
                ((RectTransform)fill.transform).sizeDelta = Vector2.zero;
                Slider slider = fixture.Content.gameObject.AddComponent<Slider>();
                slider.transition = Selectable.Transition.None;
                slider.fillRect = (RectTransform)fill.transform;
                slider.SetValueWithoutNotify(1f);
                Canvas.ForceUpdateCanvases();
                int changes = 0;
                slider.onValueChanged.AddListener(value => changes++);
                Vector2 originalAnchor = slider.fillRect.anchorMax;
                using (UIRetainerPreviewSession session = new UIRetainerPreviewSession(fixture.Retainer))
                {
                    session.UpdatePreview(new Vector2(128f, 64f));
                    Assert.That(session.Error, Is.Null);
                    Assert.That(ReadCenter(session.Texture).g, Is.GreaterThan(0.9f));
                    Assert.That(slider.fillRect.anchorMax, Is.EqualTo(originalAnchor));
                    Assert.That(changes, Is.EqualTo(0));
                }
            }
        }

        [Test]
        public void PropertyChangesUpdateWarpWithoutReplacingCaptureAndContentChangesRecapture()
        {
            using (Fixture fixture = new Fixture())
            using (UIRetainerPreviewSession session = new UIRetainerPreviewSession(fixture.Retainer))
            {
                Vector2 size = new Vector2(128f, 64f);
                session.UpdatePreview(size);
                Assert.That(session.Error, Is.Null);
                RenderTexture original = session.Texture;
                Assert.That(session.UpdatePreview(size), Is.False, "Unchanged preview should reuse its capture.");
                fixture.Retainer.Perspective = new Vector2(0.3f, -0.1f);
                fixture.Retainer.Curvature = -0.2f;
                Assert.That(session.UpdatePreview(size), Is.True);
                Assert.That(session.Texture, Is.SameAs(original));
                Assert.That(session.Material.GetFloat("_Curvature"), Is.EqualTo(-0.2f));
                Assert.That(session.Material.GetVector("_Perspective").x, Is.EqualTo(0.3f));
                fixture.Retainer.PerspectiveFOV = 110f;
                Assert.That(session.UpdatePreview(size), Is.True);
                Assert.That(session.Material.GetFloat("_PerspectiveFOV"), Is.EqualTo(110f));
                fixture.Retainer.FitContent = false;
                Assert.That(session.UpdatePreview(size), Is.True);
                Assert.That(session.Material.GetFloat("_FitContent"), Is.Zero);
                Assert.That(session.Texture, Is.SameAs(original));
                fixture.Content.color = Color.red;
                EditorUtility.SetDirty(fixture.Content);
                Assert.That(session.UpdatePreview(size), Is.True);
                Color changed = ReadCenter(session.Texture);
                Assert.That(changed.r, Is.GreaterThan(0.9f));
                Assert.That(changed.g, Is.LessThan(0.05f));
                session.UpdatePreview(new Vector2(256f, 128f));
                Assert.That(session.Texture.width, Is.EqualTo(256));
                Assert.That(session.Texture.height, Is.EqualTo(128));
            }
        }

        [Test]
        public void InvalidConfigurationReleasesResourcesAndRecovers()
        {
            using (Fixture fixture = new Fixture())
            using (UIRetainerPreviewSession session = new UIRetainerPreviewSession(fixture.Retainer))
            {
                int scenes = EditorSceneManager.previewSceneCount;
                session.UpdatePreview(new Vector2(128f, 64f));
                SerializedObject settings = new SerializedObject(fixture.Retainer);
                settings.FindProperty("effectMaterial").objectReferenceValue = null;
                settings.ApplyModifiedPropertiesWithoutUndo();
                session.UpdatePreview(new Vector2(128f, 64f));
                Assert.That(session.Error, Does.Contain("Effect Material"));
                Assert.That(session.Texture, Is.Null);
                Assert.That(EditorSceneManager.previewSceneCount, Is.EqualTo(scenes));
                settings.FindProperty("effectMaterial").objectReferenceValue = fixture.Effect;
                settings.ApplyModifiedPropertiesWithoutUndo();
                session.UpdatePreview(new Vector2(128f, 64f));
                Assert.That(session.Error, Is.Null);
                Assert.That(session.Texture, Is.Not.Null);
                session.Dispose(); session.Dispose();
                Assert.That(EditorSceneManager.previewSceneCount, Is.EqualTo(scenes));
            }
        }

        [Test]
        public void PrefabAssetsAndLoadedPrefabContentsKeepTheirSerializedReferences()
        {
            string folder = "Assets/RetainerPreviewTest-" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", folder.Substring("Assets/".Length));
            try
            {
                using (Fixture fixture = new Fixture())
                {
                    AssetDatabase.CreateAsset(fixture.Effect, folder + "/Effect.mat");
                    GameObject asset = PrefabUtility.SaveAsPrefabAsset(fixture.Root, folder + "/UI.prefab");
                    UIRetainer retainer = asset.GetComponentInChildren<UIRetainer>();
                    string before = FileText(folder + "/UI.prefab");
                    using (UIRetainerPreviewSession session = new UIRetainerPreviewSession(retainer))
                    {
                        session.UpdatePreview(new Vector2(128f, 64f));
                        Assert.That(session.Error, Is.Null);
                        Assert.That(ReadCenter(session.Texture).a, Is.GreaterThan(0.45f));
                    }
                    GameObject loaded = PrefabUtility.LoadPrefabContents(folder + "/UI.prefab");
                    try
                    {
                        UIRetainer loadedRetainer = loaded.GetComponentInChildren<UIRetainer>();
                        Transform parent = loadedRetainer.Content.parent;
                        using (UIRetainerPreviewSession session = new UIRetainerPreviewSession(loadedRetainer))
                        { session.UpdatePreview(new Vector2(128f, 64f)); Assert.That(session.Error, Is.Null); }
                        Assert.That(loadedRetainer.Content.parent, Is.SameAs(parent));
                    }
                    finally { PrefabUtility.UnloadPrefabContents(loaded); }
                    Assert.That(FileText(folder + "/UI.prefab"), Is.EqualTo(before));
                }
            }
            finally { AssetDatabase.DeleteAsset(folder); }
        }

        [UnityTest]
        public IEnumerator PreviewWindowSuspendsForPlayModeAndResumesAfterExit()
        {
            string folder = "Assets/RetainerPreviewLifecycle-" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", folder.Substring("Assets/".Length));
            UIRetainerPreviewWindow window = null;
            try
            {
                UIRetainer prefab;
                using (Fixture fixture = new Fixture())
                {
                    AssetDatabase.CreateAsset(fixture.Effect, folder + "/Effect.mat");
                    prefab = PrefabUtility.SaveAsPrefabAsset(fixture.Root, folder + "/UI.prefab").GetComponentInChildren<UIRetainer>();
                }
                int scenes = EditorSceneManager.previewSceneCount;
                window = UIRetainerPreviewWindow.Open(prefab);
                double deadline = EditorApplication.timeSinceStartup + 5d;
                while (EditorSceneManager.previewSceneCount == scenes && EditorApplication.timeSinceStartup < deadline) yield return null;
                Assert.That(EditorSceneManager.previewSceneCount, Is.EqualTo(scenes + 1));
                bool expectsReload = !EditorSettings.enterPlayModeOptionsEnabled ||
                    (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableDomainReload) == 0;
                yield return new EnterPlayMode(expectsReload);
                Assert.That(EditorSceneManager.previewSceneCount, Is.EqualTo(scenes), "Entering Play Mode must release the editor capture.");
                yield return new ExitPlayMode();
                deadline = EditorApplication.timeSinceStartup + 5d;
                while (EditorSceneManager.previewSceneCount == scenes && EditorApplication.timeSinceStartup < deadline) yield return null;
                Assert.That(EditorSceneManager.previewSceneCount, Is.EqualTo(scenes + 1), "Edit Mode preview must resume afterward.");
            }
            finally
            {
                if (window != null) window.Close();
                AssetDatabase.DeleteAsset(folder);
            }
        }

        [UnityTearDown]
        public IEnumerator RestoreEditMode()
        {
            if (EditorApplication.isPlaying) yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator ClosingWindowReleasesItsPreviewScene()
        {
            using (Fixture fixture = new Fixture())
            {
                int scenes = EditorSceneManager.previewSceneCount;
                UIRetainerPreviewWindow window = UIRetainerPreviewWindow.Open(fixture.Retainer);
                try
                {
                    double deadline = EditorApplication.timeSinceStartup + 5d;
                    while (EditorSceneManager.previewSceneCount == scenes && EditorApplication.timeSinceStartup < deadline) yield return null;
                    Assert.That(EditorSceneManager.previewSceneCount, Is.EqualTo(scenes + 1));
                }
                finally { window.Close(); }
                Assert.That(EditorSceneManager.previewSceneCount, Is.EqualTo(scenes));
            }
        }

        private static string FileText(string path) => System.IO.File.ReadAllText(path);
    }
}
