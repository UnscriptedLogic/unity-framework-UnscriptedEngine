using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnscriptedEngine.UI;

namespace UnscriptedEngine.Tests
{
    public sealed class UIRetainerTests
    {
        [Test]
        public void IdentityMappingPreservesCoordinates()
        {
            Vector2 point = new Vector2(0.17f, 0.81f);
            Assert.That(Vector2.Distance(UIRetainerWarp.MapToSource(point, 0f, Vector2.zero), point), Is.LessThan(0.00001f));
        }

        [Test]
        public void CurvatureKeepsCenterAndClipsCorners()
        {
            Assert.That(UIRetainerWarp.MapToSource(Vector2.one * 0.5f, 0.3f, new Vector2(0.2f, -0.1f)), Is.EqualTo(Vector2.one * 0.5f));
            Assert.That(UIRetainerWarp.Contains(UIRetainerWarp.MapToSource(Vector2.one, 0.2f, Vector2.zero)), Is.False);
        }

        [Test]
        public void PerspectiveMatchesKnownProjection()
        {
            Vector2 source = UIRetainerWarp.MapToSource(new Vector2(1f, 0.5f), 0f, new Vector2(0.25f, 0f));
            Assert.That(source.x, Is.EqualTo(0.9f).Within(0.00001f));
            Assert.That(source.y, Is.EqualTo(0.5f).Within(0.00001f));
        }

        [Test]
        public void ShaderImportsWithoutErrors()
        {
            Shader shader = Shader.Find("UnscriptedEngine/UI/Retainer Warp");
            Assert.That(shader, Is.Not.Null);
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False);
        }

        [Test]
        public void ShaderSamplesSameCoordinatesAsPointerMapping()
        {
            Texture2D source = new Texture2D(64, 64, TextureFormat.RGBA32, false, true);
            RenderTexture target = new RenderTexture(64, 64, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            Texture2D readback = new Texture2D(64, 64, TextureFormat.RGBA32, false, true);
            Material material = new Material(Shader.Find("UnscriptedEngine/UI/Retainer Warp"));
            RenderTexture previous = RenderTexture.active;
            try
            {
                for (int y = 0; y < 64; y++)
                    for (int x = 0; x < 64; x++)
                        source.SetPixel(x, y, new Color((x + 0.5f) / 64f, (y + 0.5f) / 64f, 0f, 1f));
                source.Apply();
                source.wrapMode = TextureWrapMode.Clamp;
                material.SetFloat("_Curvature", 0.12f);
                material.SetVector("_Perspective", new Vector4(0.2f, -0.1f, 0f, 0f));
                target.Create();
                RenderTexture.active = target;
                GL.Clear(true, true, Color.clear);
                Graphics.Blit(source, target, material);
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, 64, 64), 0, 0);
                readback.Apply();
                Vector2 expected = UIRetainerWarp.MapToSource(new Vector2(48.5f / 64f, 32.5f / 64f), 0.12f, new Vector2(0.2f, -0.1f));
                Color actual = readback.GetPixel(48, 32);
                Assert.That(actual.r, Is.EqualTo(expected.x).Within(0.025f));
                Assert.That(actual.g, Is.EqualTo(expected.y).Within(0.025f));
                Assert.That(readback.GetPixel(0, 0).a, Is.EqualTo(0f).Within(0.01f));
            }
            finally
            {
                RenderTexture.active = previous;
                target.Release();
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(readback);
                Object.DestroyImmediate(material);
            }
        }

        [UnityTest]
        public IEnumerator CaptureRoutesWarpedClicksAndRestoresContent()
        {
            bool expectsReload = !EditorSettings.enterPlayModeOptionsEnabled ||
                (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableDomainReload) == 0;
            yield return new EnterPlayMode(expectsReload);
            GameObject canvasObject = new GameObject("Retainer Test Canvas", typeof(Canvas), typeof(GraphicRaycaster));
            GameObject events = new GameObject("Retainer Test Events", typeof(EventSystem));
            Material material = new Material(Shader.Find("UnscriptedEngine/UI/Retainer Warp"));
            try
            {
                canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(RawImage));
                panel.SetActive(false);
                panel.transform.SetParent(canvasObject.transform, false);
                ((RectTransform)panel.transform).sizeDelta = new Vector2(400f, 200f);
                GameObject contentObject = new GameObject("Content", typeof(RectTransform), typeof(Image));
                contentObject.GetComponent<Image>().color = new Color(0f, 0f, 1f, 0.5f);
                contentObject.transform.SetParent(panel.transform, false);
                RectTransform content = (RectTransform)contentObject.transform;
                content.anchorMin = Vector2.zero;
                content.anchorMax = Vector2.one;
                content.offsetMin = content.offsetMax = Vector2.zero;
                GameObject buttonObject = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
                buttonObject.transform.SetParent(content, false);
                RectTransform buttonRect = (RectTransform)buttonObject.transform;
                buttonRect.sizeDelta = new Vector2(70f, 60f);
                // With perspective X=.25, display UV .75 maps to source UV .722222.
                buttonRect.anchoredPosition = new Vector2(88.8889f, 0f);
                buttonObject.GetComponent<Image>().color = Color.green;
                int clicks = 0;
                buttonObject.GetComponent<Button>().onClick.AddListener(() => clicks++);
                GameObject sliderObject = new GameObject("Slider", typeof(RectTransform), typeof(Image), typeof(Slider));
                sliderObject.transform.SetParent(content, false);
                RectTransform sliderRect = (RectTransform)sliderObject.transform;
                sliderRect.sizeDelta = new Vector2(120f, 30f);
                sliderRect.anchoredPosition = new Vector2(-80f, 50f);
                GameObject handleObject = new GameObject("Handle", typeof(RectTransform), typeof(Image));
                handleObject.transform.SetParent(sliderObject.transform, false);
                Slider slider = sliderObject.GetComponent<Slider>();
                slider.handleRect = (RectTransform)handleObject.transform;
                UIRetainer retainer = panel.AddComponent<UIRetainer>();
                SerializedObject settings = new SerializedObject(retainer);
                settings.FindProperty("content").objectReferenceValue = content;
                settings.FindProperty("effectMaterial").objectReferenceValue = material;
                settings.FindProperty("curvature").floatValue = 0f;
                settings.FindProperty("refreshRate").floatValue = 0f;
                settings.FindProperty("perspective").vector2Value = new Vector2(0.25f, 0f);
                settings.ApplyModifiedPropertiesWithoutUndo();
                panel.SetActive(true);
                yield return null;
                yield return null;
                yield return null;
                Canvas.ForceUpdateCanvases();
                Assert.That(retainer.Texture, Is.Not.Null);
                Assert.That(retainer.Texture.IsCreated(), Is.True);
                Texture2D sample = new Texture2D(1, 1, TextureFormat.RGBA32, false, true);
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = retainer.Texture;
                sample.ReadPixels(new Rect(10, 10, 1, 1), 0, 0);
                sample.Apply();
                RenderTexture.active = previous;
                float alpha = sample.GetPixel(0, 0).a;
                Object.Destroy(sample);
                Assert.That(alpha, Is.EqualTo(0.5f).Within(0.03f), "Capture must preserve translucent UI alpha.");
                Assert.That(content.parent, Is.Not.EqualTo(panel.transform));
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, panel.transform.TransformPoint(new Vector3(100f, 0f)));
                PointerEventData pointer = new PointerEventData(events.GetComponent<EventSystem>())
                { position = screen, button = PointerEventData.InputButton.Left, pointerId = -1, eligibleForClick = true };
                UIRetainerInput input = panel.GetComponent<UIRetainerInput>();
                input.OnPointerDown(pointer);
                input.OnPointerUp(pointer);
                input.OnPointerClick(pointer);
                Assert.That(clicks, Is.EqualTo(1), "Warped pointer must reach the captured button.");
                pointer.position = RectTransformUtility.WorldToScreenPoint(null,
                    panel.transform.TransformPoint(new Vector3(-72.7273f, 45.4545f)));
                input.OnPointerDown(pointer);
                input.OnInitializePotentialDrag(pointer);
                input.OnBeginDrag(pointer);
                pointer.position = RectTransformUtility.WorldToScreenPoint(null,
                    panel.transform.TransformPoint(new Vector3(-38.0952f, 47.619f)));
                input.OnDrag(pointer);
                Assert.That(slider.value, Is.EqualTo(0.83333f).Within(0.03f), "Slider drag must use capture-camera coordinates.");
                input.OnPointerUp(pointer);
                input.OnEndDrag(pointer);
                GameObject added = new GameObject("Dynamic content", typeof(RectTransform));
                added.layer = 2;
                added.transform.SetParent(content, false);
                retainer.RefreshContent();
                Assert.That(added.layer, Is.EqualTo(31));
                added.transform.SetParent(panel.transform, false);
                retainer.RefreshContent();
                Assert.That(added.layer, Is.EqualTo(2), "Objects moved out of capture must regain their original layer.");
                retainer.enabled = false;
                Assert.That(content.parent, Is.EqualTo(panel.transform));
                Assert.That(retainer.Texture, Is.Null);
                Assert.That(buttonObject.layer, Is.EqualTo(0));
                // Re-entry covers repeated play-time setup without any static state.
                retainer.enabled = true;
                yield return null;
                Assert.That(retainer.Texture, Is.Not.Null);
                retainer.enabled = false;
            }
            finally
            {
                Object.Destroy(canvasObject);
                Object.Destroy(events);
                Object.Destroy(material);
            }
            yield return null;
            yield return new ExitPlayMode();
        }
    }
}
