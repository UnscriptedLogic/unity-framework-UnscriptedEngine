using System.Collections;
using Framework.GameplayTag;
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
            Assert.That(UIRetainerWarp.MapToSource(Vector2.one * 0.5f, 0.3f, new Vector2(0.2f, -0.1f), 60f, false), Is.EqualTo(Vector2.one * 0.5f));
            Assert.That(UIRetainerWarp.Contains(UIRetainerWarp.MapToSource(Vector2.one, 0.2f, Vector2.zero)), Is.False);
        }

        [Test]
        public void PerspectiveMatchesKnownProjection()
        {
            Vector2 source = UIRetainerWarp.MapToSource(new Vector2(1f, 0.5f), 0f, new Vector2(0.25f, 0f), 60f, false);
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

        [TestCase(-0.5f, 150f, 0.4f, -0.4f)]
        [TestCase(-0.5f, 60f, -0.8f, 0f)]
        [TestCase(0f, 120f, 0.3f, 0.1f)]
        [TestCase(0.5f, 30f, -0.2f, 0.5f)]
        public void EntireSourceFitsAndPointerMappingReachesEveryEdge(float curvature, float fieldOfView, float horizontal, float vertical)
        {
            Vector2 perspective = new Vector2(horizontal, vertical);
            Vector2 effective = UIRetainerWarp.EffectivePerspective(perspective, fieldOfView);
            Vector3 fit = UIRetainerWarp.FitBounds(curvature, effective);
            float bound = UIRetainerWarp.RadialBounds(curvature);
            for (int side = 0; side < 4; side++)
                for (int i = 0; i <= 32; i++)
                {
                    float along = i / 16f - 1f;
                    Vector2 source = side == 0 ? new Vector2(-1f, along) : side == 1 ? new Vector2(1f, along) :
                        side == 2 ? new Vector2(along, -1f) : new Vector2(along, 1f);
                    // Independently invert radial distance to project the original source outline.
                    float low = 0f;
                    float high = 8f;
                    for (int iteration = 0; iteration < 32; iteration++)
                    {
                        float radius = (low + high) * 0.5f;
                        float mapped = radius * (curvature < 0f ? Mathf.Pow(1f - 4f * curvature * radius * radius, -0.25f) : 1f + curvature * radius * radius);
                        if (mapped < source.magnitude) low = radius;
                        else high = radius;
                    }
                    Vector2 radial = source.normalized * ((low + high) * 0.5f);
                    Vector2 projected = radial / (1f - Vector2.Dot(effective, radial) / bound);
                    Vector2 uv = ((projected - new Vector2(fit.x, fit.y)) / fit.z + Vector2.one) * 0.5f;
                    Assert.That(UIRetainerWarp.Contains(uv), Is.True, $"Clipped source edge {source}");
                    Vector2 sampled = UIRetainerWarp.MapToSource(uv, curvature, perspective, fieldOfView);
                    Assert.That(Vector2.Distance(sampled, (source + Vector2.one) * 0.5f), Is.LessThan(0.001f));
                    Vector2 projectedByApi = UIRetainerWarp.MapFromSource((source + Vector2.one) * 0.5f, curvature, perspective, fieldOfView);
                    Assert.That(Vector2.Distance(projectedByApi, uv), Is.LessThan(0.001f));
                }
        }

        [Test]
        public void WiderFovExaggeratesPerspectiveWhileStayingBeforeHorizon()
        {
            Vector2 perspective = new Vector2(0.2f, 0.1f);
            Vector2 normal = UIRetainerWarp.EffectivePerspective(perspective, 60f);
            Vector2 wide = UIRetainerWarp.EffectivePerspective(perspective, 120f);
            Assert.That(wide.magnitude, Is.GreaterThan(normal.magnitude * 2f));
            Vector2 extreme = UIRetainerWarp.EffectivePerspective(Vector2.one, 150f);
            Assert.That(Mathf.Abs(extreme.x) + Mathf.Abs(extreme.y), Is.LessThan(1f));
        }

        [TestCase(-0.5f, 0.8f, 0f)]
        [TestCase(-0.5f, -0.4f, 0.4f)]
        [TestCase(-0.25f, 0f, -0.8f)]
        public void NegativeCurvatureDoesNotFold(float curvature, float horizontal, float vertical)
        {
            Vector2 perspective = new Vector2(horizontal, vertical);
            const float step = 0.0001f;
            for (int y = 0; y <= 20; y++)
                for (int x = 0; x <= 20; x++)
                {
                    Vector2 uv = new Vector2(x / 20f, y / 20f);
                    Vector2 point = UIRetainerWarp.MapToSource(uv, curvature, perspective, 60f, false);
                    Vector2 dx = UIRetainerWarp.MapToSource(uv + Vector2.right * step, curvature, perspective, 60f, false) - point;
                    Vector2 dy = UIRetainerWarp.MapToSource(uv + Vector2.up * step, curvature, perspective, 60f, false) - point;
                    Assert.That(dx.x * dy.y - dx.y * dy.x, Is.GreaterThan(0f), $"Fold at {uv}");
                }
        }

        [TestCase(0.12f, 60f)]
        [TestCase(-0.12f, 60f)]
        [TestCase(-0.5f, 60f)]
        [TestCase(-0.5f, 120f)]
        [TestCase(0.5f, 150f)]
        public void ShaderSamplesSameCoordinatesAsPointerMapping(float curvature, float fieldOfView)
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
                material.SetFloat("_Curvature", curvature);
                material.SetFloat("_PerspectiveFOV", fieldOfView);
                material.SetVector("_Perspective", new Vector4(0.2f, -0.1f, 0f, 0f));
                target.Create();
                RenderTexture.active = target;
                GL.Clear(true, true, Color.clear);
                Graphics.Blit(source, target, material);
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, 64, 64), 0, 0);
                readback.Apply();
                int visibleSamples = 0;
                for (int y = 0; y < 64; y += 3)
                    for (int x = 0; x < 64; x += 3)
                    {
                        Vector2 expected = UIRetainerWarp.MapToSource(new Vector2((x + 0.5f) / 64f, (y + 0.5f) / 64f), curvature, new Vector2(0.2f, -0.1f), fieldOfView);
                        Color actual = readback.GetPixel(x, y);
                        bool visible = UIRetainerWarp.Contains(expected);
                        Assert.That(actual.a, Is.EqualTo(visible ? 1f : 0f).Within(0.01f), $"Alpha at {x}, {y}");
                        if (!visible) continue;
                        visibleSamples++;
                        Assert.That(actual.r, Is.EqualTo(expected.x).Within(0.025f));
                        Assert.That(actual.g, Is.EqualTo(expected.y).Within(0.025f));
                    }
                Assert.That(visibleSamples, Is.GreaterThan(0));
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
            GameObject actor = new GameObject("Tagged actor");
            GameplayTag health = ScriptableObject.CreateInstance<GameplayTag>();
            JsonUtility.FromJsonOverwrite("{\"path\":\"Stats.Health\"}", health);
            TaggedFloatValue value = actor.AddComponent<TaggedFloatValue>();
            value.Tags.Add(health);
            try
            {
                canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObject.AddComponent<TaggedValueContext>().SetSource(actor);
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
                TaggedFloatBinding binding = sliderObject.AddComponent<TaggedFloatBinding>();
                binding.ValueTag = health;
                TaggedFloatDisplay taggedDisplay = sliderObject.AddComponent<TaggedFloatDisplay>();
                SerializedObject displaySettings = new SerializedObject(taggedDisplay);
                displaySettings.FindProperty("slider").objectReferenceValue = slider;
                displaySettings.ApplyModifiedPropertiesWithoutUndo();
                UIRetainer retainer = panel.AddComponent<UIRetainer>();
                retainer.FitContent = false; // This fixture uses known, unfitted pointer coordinates.
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
                Assert.That(binding.Source, Is.SameAs(value), "Capture must preserve inherited tagged-value context.");
                value.SetValue(25f);
                Assert.That(slider.value, Is.EqualTo(0.25f).Within(0.001f));
                value.SetValue(0f); // Keep the drag fixture's initial handle position unchanged.
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
                value.SetValue(60f);
                Assert.That(slider.value, Is.EqualTo(0.6f).Within(0.001f));
                // Reproduce closing the source UI from a child's capture-root deactivation callback.
                RetainerDeactivateProbe probe = contentObject.AddComponent<RetainerDeactivateProbe>();
                probe.Target = panel;
                retainer.CaptureRaycaster.gameObject.SetActive(false);
                yield return null;
                yield return null;
                Assert.That(content.parent, Is.EqualTo(panel.transform));
                Assert.That(retainer.Texture, Is.Null);
                probe.Target = null;
                panel.SetActive(true);
                yield return null;
                value.SetValue(40f);
                Assert.That(binding.Source, Is.SameAs(value));
                Assert.That(slider.value, Is.EqualTo(0.4f).Within(0.001f));
                retainer.enabled = false;
            }
            finally
            {
                Object.Destroy(canvasObject);
                Object.Destroy(events);
                Object.Destroy(material);
                Object.Destroy(actor);
                Object.Destroy(health);
            }
            yield return null;
            yield return new ExitPlayMode();
        }
    }
}
