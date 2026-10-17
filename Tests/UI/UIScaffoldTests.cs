using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnscriptedEngine.Editor.Scaffolding;
using UnscriptedEngine.UI;
using UnscriptedEngine.UI.Editor;

namespace UnscriptedEngine.Tests
{
    public class ScaffoldCanvasSubclass : UCanvasComponent { }
    public class ScaffoldWidgetSubclass : UWidgetComponent { }

    public class UIScaffoldTests
    {
        private GameObject root;

        [SetUp]
        public void Setup()
        {
            root = new GameObject("UI scaffold", typeof(RectTransform));
            root.SetActive(false);
        }

        [TearDown]
        public void Cleanup() { Object.DestroyImmediate(root); }

        [Test]
        public void CanvasPrefabHasScalingAndRaycastingWithDerivedComponent()
        {
            UIScaffoldBuilder.ConfigureRoot(root, UIScaffoldKind.Canvas, typeof(ScaffoldCanvasSubclass));
            Assert.NotNull(root.GetComponent<ScaffoldCanvasSubclass>());
            Assert.NotNull(root.GetComponent<GraphicRaycaster>());
            Assert.AreEqual(RenderMode.ScreenSpaceOverlay, root.GetComponent<Canvas>().renderMode);
            var scaler = root.GetComponent<CanvasScaler>();
            Assert.AreEqual(CanvasScaler.ScaleMode.ScaleWithScreenSize, scaler.uiScaleMode);
            Assert.AreEqual(new Vector2(1920, 1080), scaler.referenceResolution);
            Assert.AreEqual(0.5f, scaler.matchWidthOrHeight);
            Assert.AreEqual(LayerMask.NameToLayer("UI"), root.layer);
        }

        [Test]
        public void WidgetStretchesUnderOwningCanvasWithoutItsOwnCanvas()
        {
            UIScaffoldBuilder.ConfigureRoot(root, UIScaffoldKind.Widget, typeof(ScaffoldWidgetSubclass));
            Assert.NotNull(root.GetComponent<ScaffoldWidgetSubclass>());
            Assert.NotNull(root.GetComponent<CanvasGroup>());
            Assert.IsNull(root.GetComponent<Canvas>());
            var rect = root.GetComponent<RectTransform>();
            Assert.AreEqual(Vector2.zero, rect.anchorMin);
            Assert.AreEqual(Vector2.one, rect.anchorMax);
            Assert.AreEqual(Vector2.zero, rect.offsetMin);
            Assert.AreEqual(Vector2.zero, rect.offsetMax);
        }

        [Test]
        public void WrongBaseTypeIsRejectedBeforeAddingComponents()
        {
            Assert.Throws<System.ArgumentException>(() =>
                UIScaffoldBuilder.ConfigureRoot(root, UIScaffoldKind.Canvas, typeof(ScaffoldWidgetSubclass)));
            Assert.IsNull(root.GetComponent<Canvas>());
        }
    }
}
