using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnscriptedEngine.UI;

namespace UnscriptedEngine.Tests
{
    public sealed class UIMovementSwayTests
    {
        private static void SetMovement(CharacterMovementComponent movement, Vector3 velocity, bool grounded, bool flying = false)
        {
            typeof(CharacterMovementComponent).GetField("velocity", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(movement, velocity);
            typeof(CharacterMovementComponent).GetField("isGrounded", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(movement, grounded);
            typeof(CharacterMovementComponent).GetField("isFlying", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(movement, flying);
        }

        private static RectTransform Image(Transform parent, string name, Vector2 position)
        {
            GameObject child = new GameObject(name, typeof(RectTransform), typeof(Image));
            child.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)child.transform;
            rect.sizeDelta = new Vector2(40f, 30f);
            rect.anchoredPosition = position;
            return rect;
        }

        private static bool ExpectsReload => !EditorSettings.enterPlayModeOptionsEnabled ||
            (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableDomainReload) == 0;

        [UnityTest]
        public IEnumerator MovementFallsOffAtCenterRestoresPoseAndJoltsOnlyOnValidTransitions()
        {
            yield return new EnterPlayMode(ExpectsReload);
            float previousCaptureDelta = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60f;
            GameObject actor = new GameObject("Sway actor", typeof(CharacterMovementComponent));
            GameObject canvasObject = new GameObject("Sway canvas", typeof(Canvas));
            try
            {
                CharacterMovementComponent movement = actor.GetComponent<CharacterMovementComponent>();
                SetMovement(movement, Vector3.right * 6f, true);
                canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                Canvas.ForceUpdateCanvases();
                RectTransform center = Image(canvasObject.transform, "Center", Vector2.zero);
                RectTransform near = Image(canvasObject.transform, "Near", new Vector2(Screen.width * 0.1f, 0f));
                RectTransform far = Image(canvasObject.transform, "Far", new Vector2(Screen.width * 0.4f, 0f));
                Vector3 nearOriginal = near.anchoredPosition3D;
                Vector3 farOriginal = far.anchoredPosition3D;
                Quaternion farRotation = Quaternion.Euler(0f, 0f, 7f);
                far.localRotation = farRotation;
                UIMovementSway sway = canvasObject.AddComponent<UIMovementSway>();
                sway.SetMovementSource(movement);
                for (int i = 0; i < 12; i++) yield return null;
                Canvas.ForceUpdateCanvases();
                Assert.That(center.anchoredPosition3D, Is.EqualTo(Vector3.zero));
                Assert.That(far.anchoredPosition3D.x, Is.LessThan(farOriginal.x));
                Assert.That((far.anchoredPosition3D - farOriginal).magnitude,
                    Is.GreaterThan((near.anchoredPosition3D - nearOriginal).magnitude * 2f));
                Assert.That(Quaternion.Angle(far.localRotation, farRotation), Is.GreaterThan(0.01f));
                Vector3 once = far.anchoredPosition3D;
                for (int i = 0; i < 5; i++) Canvas.ForceUpdateCanvases();
                Assert.That(Vector3.Distance(far.anchoredPosition3D, once), Is.LessThan(0.001f), "Repeated render callbacks must not accumulate offsets.");
                sway.enabled = false;
                Assert.That(Vector3.Distance(far.anchoredPosition3D, farOriginal), Is.LessThan(0.001f));
                Assert.That(Quaternion.Angle(far.localRotation, farRotation), Is.LessThan(0.001f));

                SerializedObject settings = new SerializedObject(sway);
                settings.FindProperty("shift").vector2Value = Vector2.zero;
                settings.ApplyModifiedPropertiesWithoutUndo();
                SetMovement(movement, Vector3.zero, true);
                sway.enabled = true;
                yield return null;
                SetMovement(movement, Vector3.up * 7f, false);
                yield return null;
                Canvas.ForceUpdateCanvases();
                Assert.That(far.anchoredPosition3D.y, Is.LessThan(farOriginal.y - 0.01f), "Takeoff must produce an inertial jolt.");
                sway.enabled = false;
                SetMovement(movement, Vector3.down * 9f, false);
                sway.enabled = true;
                yield return null;
                SetMovement(movement, Vector3.zero, true);
                yield return null;
                Canvas.ForceUpdateCanvases();
                Assert.That(far.anchoredPosition3D.y, Is.LessThan(farOriginal.y - 0.01f), "Landing must use observed fall speed even when motor velocity resets to zero.");
                float timeScale = Time.timeScale;
                Time.timeScale = 0f;
                yield return null;
                Canvas.ForceUpdateCanvases();
                Vector3 paused = far.anchoredPosition3D;
                yield return null;
                Canvas.ForceUpdateCanvases();
                Assert.That(Vector3.Distance(far.anchoredPosition3D, paused), Is.LessThan(0.001f));
                Time.timeScale = timeScale;
                sway.enabled = false;
                SetMovement(movement, Vector3.down * 9f, false, true);
                sway.enabled = true;
                yield return null;
                SetMovement(movement, Vector3.zero, true, false);
                yield return null;
                Canvas.ForceUpdateCanvases();
                Assert.That(far.anchoredPosition3D.y, Is.EqualTo(farOriginal.y).Within(0.001f), "Leaving flight must not trigger a landing jolt.");
                sway.enabled = false;
                Assert.That(center.anchoredPosition3D, Is.EqualTo(Vector3.zero));
            }
            finally
            {
                Time.timeScale = 1f;
                Time.captureDeltaTime = previousCaptureDelta;
                Object.Destroy(canvasObject);
                Object.Destroy(actor);
            }
            yield return null;
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator AutomaticSourceAndLayoutControlledTargetsRemainStable()
        {
            yield return new EnterPlayMode(ExpectsReload);
            GameObject actor = new GameObject("Only movement source", typeof(CharacterMovementComponent));
            GameObject canvasObject = new GameObject("Layout sway canvas", typeof(Canvas));
            try
            {
                CharacterMovementComponent movement = actor.GetComponent<CharacterMovementComponent>();
                SetMovement(movement, Vector3.right * 6f, true);
                canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                Canvas.ForceUpdateCanvases();
                GameObject group = new GameObject("Layout", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                group.transform.SetParent(canvasObject.transform, false);
                RectTransform groupRect = (RectTransform)group.transform;
                groupRect.sizeDelta = new Vector2(Screen.width * 0.8f, 60f);
                groupRect.anchoredPosition = new Vector2(0f, Screen.height * 0.3f);
                HorizontalLayoutGroup layout = group.GetComponent<HorizontalLayoutGroup>();
                layout.childControlWidth = layout.childControlHeight = false;
                layout.childForceExpandWidth = layout.childForceExpandHeight = false;
                layout.childAlignment = TextAnchor.MiddleCenter;
                layout.spacing = 20f;
                RectTransform left = Image(group.transform, "Left", Vector2.zero);
                RectTransform right = Image(group.transform, "Right", Vector2.zero);
                LayoutRebuilder.ForceRebuildLayoutImmediate(groupRect);
                Vector3 baseline = right.anchoredPosition3D;
                UIMovementSway sway = canvasObject.AddComponent<UIMovementSway>();
                SerializedObject settings = new SerializedObject(sway);
                SerializedProperty targets = settings.FindProperty("targets");
                targets.arraySize = 2;
                targets.GetArrayElementAtIndex(0).objectReferenceValue = left;
                targets.GetArrayElementAtIndex(1).objectReferenceValue = right;
                settings.ApplyModifiedPropertiesWithoutUndo();
                sway.RefreshTargets();
                for (int i = 0; i < 12; i++) yield return null;
                LayoutRebuilder.MarkLayoutForRebuild(groupRect);
                Canvas.ForceUpdateCanvases();
                Assert.That(sway.ResolvedMovement, Is.SameAs(movement), "A plain canvas should discover a unique local movement source.");
                Assert.That(right.anchoredPosition3D.x, Is.LessThan(baseline.x - 0.01f), "Sway must apply after layout rebuilds.");
                Vector3 animated = right.anchoredPosition3D;
                for (int i = 0; i < 4; i++)
                {
                    LayoutRebuilder.MarkLayoutForRebuild(groupRect);
                    Canvas.ForceUpdateCanvases();
                }
                Assert.That(Vector3.Distance(right.anchoredPosition3D, animated), Is.LessThan(0.001f));
                sway.enabled = false;
                Assert.That(Vector3.Distance(right.anchoredPosition3D, baseline), Is.LessThan(0.001f));
            }
            finally
            {
                Object.Destroy(canvasObject);
                Object.Destroy(actor);
            }
            yield return null;
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator CapturedSwayKeepsWholeGraphicAndMappedClicksAtStrongTilt()
        {
            yield return new EnterPlayMode(ExpectsReload);
            float captureDelta = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60f;
            GameObject actor = new GameObject("Clip test source", typeof(CharacterMovementComponent));
            GameObject canvasObject = new GameObject("Clip test canvas", typeof(Canvas), typeof(EventSystem));
            Material material = new Material(Shader.Find("UnscriptedEngine/UI/Retainer Warp"));
            Texture2D pixels = null;
            try
            {
                canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                GameObject panel = new GameObject("Small retainer", typeof(RectTransform), typeof(RawImage));
                panel.SetActive(false);
                panel.transform.SetParent(canvasObject.transform, false);
                RectTransform panelRect = (RectTransform)panel.transform;
                panelRect.sizeDelta = new Vector2(400f, 80f);
                panelRect.anchoredPosition = new Vector2(Screen.width * 0.2f, 0f);
                GameObject contentObject = new GameObject("Content", typeof(RectTransform));
                contentObject.transform.SetParent(panel.transform, false);
                RectTransform content = (RectTransform)contentObject.transform;
                content.anchorMin = Vector2.zero;
                content.anchorMax = Vector2.one;
                content.sizeDelta = Vector2.zero;
                RectTransform graphic = Image(content, "Full capture graphic", Vector2.zero);
                graphic.anchorMin = Vector2.zero;
                graphic.anchorMax = Vector2.one;
                graphic.sizeDelta = Vector2.zero;
                graphic.localScale = new Vector3(0.99f, 0.99f, 1f);
                Vector3 originalScale = graphic.localScale;
                graphic.gameObject.AddComponent<RectMask2D>();
                Image(graphic, "Intentionally masked overscroll", new Vector2(10000f, 0f));
                int clicks = 0;
                graphic.gameObject.AddComponent<Button>().onClick.AddListener(() => clicks++);
                UIRetainer retainer = panel.AddComponent<UIRetainer>();
                SerializedObject settings = new SerializedObject(retainer);
                settings.FindProperty("content").objectReferenceValue = content;
                settings.FindProperty("effectMaterial").objectReferenceValue = material;
                settings.ApplyModifiedPropertiesWithoutUndo();
                retainer.Curvature = -0.2f;
                retainer.Perspective = new Vector2(0.2f, 0.1f);
                panel.SetActive(true);
                CharacterMovementComponent movement = actor.GetComponent<CharacterMovementComponent>();
                SetMovement(movement, new Vector3(5f, 0f, 5f), true);
                UIMovementSway sway = canvasObject.AddComponent<UIMovementSway>();
                sway.SetMovementSource(movement);
                SerializedObject swaySettings = new SerializedObject(sway);
                swaySettings.FindProperty("shift").vector2Value = new Vector2(100f, 100f);
                swaySettings.FindProperty("tilt").vector2Value = new Vector2(25f, 25f);
                swaySettings.FindProperty("roll").floatValue = 15f;
                swaySettings.FindProperty("centerStrength").floatValue = 1f;
                swaySettings.ApplyModifiedPropertiesWithoutUndo();
                for (int i = 0; i < 20; i++) yield return null;
                Time.timeScale = 0f;
                for (int i = 0; i < 4; i++) yield return null;
                Canvas.ForceUpdateCanvases();
                Camera camera = retainer.CaptureRaycaster.eventCamera;
                Vector3[] corners = new Vector3[4];
                graphic.GetWorldCorners(corners);
                Vector2[] projected = new Vector2[4];
                for (int i = 0; i < 4; i++)
                {
                    Vector3 viewport = camera.WorldToViewportPoint(corners[i]);
                    Assert.That(viewport.x, Is.InRange(-0.0001f, 1.0001f));
                    Assert.That(viewport.y, Is.InRange(-0.0001f, 1.0001f));
                    Assert.That(viewport.z, Is.InRange(camera.nearClipPlane, camera.farClipPlane), "Tilted corners must not intersect the capture camera depth planes.");
                    projected[i] = new Vector2(viewport.x * retainer.Texture.width, viewport.y * retainer.Texture.height);
                }
                float area = 0f;
                for (int i = 0; i < 4; i++)
                {
                    Vector2 a = projected[i];
                    Vector2 b = projected[(i + 1) % 4];
                    area += a.x * b.y - b.x * a.y;
                }
                area = Mathf.Abs(area) * 0.5f;
                RenderTexture previous = RenderTexture.active;
                pixels = new Texture2D(retainer.Texture.width, retainer.Texture.height, TextureFormat.RGBA32, false, true);
                try
                {
                    RenderTexture.active = retainer.Texture;
                    pixels.ReadPixels(new Rect(0f, 0f, pixels.width, pixels.height), 0, 0);
                    pixels.Apply();
                }
                finally { RenderTexture.active = previous; }
                int opaque = 0;
                foreach (Color32 pixel in pixels.GetPixels32()) if (pixel.a > 127) opaque++;
                Assert.That(opaque, Is.EqualTo(area).Within(area * 0.06f + 20f), "Capture pixels must contain the complete tilted quad, not a clipped slice.");
                Vector3 center = camera.WorldToViewportPoint(graphic.TransformPoint(graphic.rect.center));
                Vector2 displayUV = retainer.MapFromSource(new Vector2(center.x, center.y));
                Vector2 displayLocal = panelRect.rect.min + Vector2.Scale(displayUV, panelRect.rect.size);
                PointerEventData pointer = new PointerEventData(canvasObject.GetComponent<EventSystem>())
                {
                    position = RectTransformUtility.WorldToScreenPoint(null, panelRect.TransformPoint(displayLocal)),
                    pointerId = -1, button = PointerEventData.InputButton.Left, eligibleForClick = true
                };
                UIRetainerInput input = panel.GetComponent<UIRetainerInput>();
                input.OnPointerDown(pointer);
                input.OnPointerUp(pointer);
                input.OnPointerClick(pointer);
                Assert.That(clicks, Is.EqualTo(1));
                sway.enabled = false;
                Assert.That(Vector3.Distance(graphic.localScale, originalScale), Is.LessThan(0.0001f));
                Assert.That(graphic.anchoredPosition3D.magnitude, Is.LessThan(0.001f));
                Assert.That(Quaternion.Angle(graphic.localRotation, Quaternion.identity), Is.LessThan(0.001f));
                retainer.enabled = false;
            }
            finally
            {
                Time.timeScale = 1f;
                Time.captureDeltaTime = captureDelta;
                Object.Destroy(pixels);
                Object.Destroy(canvasObject);
                Object.Destroy(actor);
                Object.Destroy(material);
            }
            yield return null;
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator ContextFollowsReplacementAndRetainerChildrenAnimateWithoutReparenting()
        {
            yield return new EnterPlayMode(ExpectsReload);
            GameObject actor = new GameObject("Sway source", typeof(CharacterMovementComponent));
            GameObject replacement = new GameObject("Replacement source", typeof(CharacterMovementComponent));
            GameObject canvasObject = new GameObject("Retained sway canvas", typeof(Canvas), typeof(TaggedValueContext));
            Material material = new Material(Shader.Find("UnscriptedEngine/UI/Retainer Warp"));
            try
            {
                canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                TaggedValueContext context = canvasObject.GetComponent<TaggedValueContext>();
                context.SetSource(actor);
                SetMovement(actor.GetComponent<CharacterMovementComponent>(), Vector3.right * 6f, true);
                SetMovement(replacement.GetComponent<CharacterMovementComponent>(), Vector3.left * 6f, true);
                Canvas.ForceUpdateCanvases();
                GameObject panel = new GameObject("Retainer", typeof(RectTransform), typeof(RawImage));
                panel.SetActive(false);
                panel.transform.SetParent(canvasObject.transform, false);
                RectTransform panelRect = (RectTransform)panel.transform;
                panelRect.anchorMin = Vector2.zero;
                panelRect.anchorMax = Vector2.one;
                panelRect.sizeDelta = Vector2.zero;
                GameObject contentObject = new GameObject("Content", typeof(RectTransform));
                contentObject.transform.SetParent(panel.transform, false);
                RectTransform content = (RectTransform)contentObject.transform;
                content.anchorMin = Vector2.zero;
                content.anchorMax = Vector2.one;
                content.sizeDelta = Vector2.zero;
                RectTransform image = Image(content, "Captured edge", new Vector2(Screen.width * 0.3f, 0f));
                Vector3 original = image.anchoredPosition3D;
                UIRetainer retainer = panel.AddComponent<UIRetainer>();
                SerializedObject settings = new SerializedObject(retainer);
                settings.FindProperty("content").objectReferenceValue = content;
                settings.FindProperty("effectMaterial").objectReferenceValue = material;
                settings.ApplyModifiedPropertiesWithoutUndo();
                retainer.Curvature = -0.2f;
                retainer.Perspective = new Vector2(0.2f, 0.1f);
                retainer.PerspectiveFOV = 100f;
                panel.SetActive(true);
                UIMovementSway sway = canvasObject.AddComponent<UIMovementSway>();
                for (int i = 0; i < 12; i++) yield return null;
                Canvas.ForceUpdateCanvases();
                Assert.That(sway.ResolvedMovement, Is.SameAs(actor.GetComponent<CharacterMovementComponent>()));
                Assert.That(image.anchoredPosition3D.x, Is.LessThan(original.x));
                Assert.That(image.parent, Is.EqualTo(content), "Sway must not create wrappers or change UI ownership.");
                context.SetSource(replacement);
                for (int i = 0; i < 12; i++) yield return null;
                Canvas.ForceUpdateCanvases();
                Assert.That(sway.ResolvedMovement, Is.SameAs(replacement.GetComponent<CharacterMovementComponent>()));
                Assert.That(image.anchoredPosition3D.x, Is.GreaterThan(original.x));
                RectTransform added = Image(content, "Dynamic edge", new Vector2(-Screen.width * 0.35f, 0f));
                Vector3 addedOriginal = added.anchoredPosition3D;
                sway.RefreshTargets();
                Canvas.ForceUpdateCanvases();
                Assert.That(added.anchoredPosition3D.x, Is.GreaterThan(addedOriginal.x));
                context.SetSource(null);
                yield return null;
                Assert.That(sway.ResolvedMovement, Is.Null, "An empty explicit context must not pick another scene actor.");
                sway.enabled = false;
                Assert.That(Vector3.Distance(image.anchoredPosition3D, original), Is.LessThan(0.001f));
                Assert.That(Vector3.Distance(added.anchoredPosition3D, addedOriginal), Is.LessThan(0.001f));
                retainer.enabled = false;
            }
            finally
            {
                Object.Destroy(canvasObject);
                Object.Destroy(actor);
                Object.Destroy(replacement);
                Object.Destroy(material);
            }
            yield return null;
            yield return new ExitPlayMode();
        }
    }
}
