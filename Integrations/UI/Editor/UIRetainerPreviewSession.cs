using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace UnscriptedEngine.UI.Editor
{
    /// <summary>Owns a visual-only copy in a preview scene. Never changes the source hierarchy or renderer.</summary>
    public sealed class UIRetainerPreviewSession : IDisposable
    {
        private readonly UIRetainer source;
        private readonly SerializedObject settings;
        private readonly List<Component> sourceComponents = new List<Component>();
        private Scene scene;
        private GameObject canvasObject;
        private Canvas canvas;
        private Camera camera;
        private RectTransform copy;
        private RenderTexture texture;
        private Material material;
        private Material sourceMaterial;
        private Vector2 previousSize;
        private int previousContentHash;
        private int previousMaterialHash;
        private bool invalidated = true;
        private bool disposed;

        public UIRetainer Source => source;
        public RenderTexture Texture => texture;
        public Material Material => material;
        public string Error { get; private set; }

        public UIRetainerPreviewSession(UIRetainer source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            this.source = source;
            settings = new SerializedObject(source);
        }

        public void Invalidate() => invalidated = true;

        /// <summary>Logical UI units, independent of the preview window's size. Returns true when the display changed.</summary>
        public bool UpdatePreview(Vector2 logicalSize)
        {
            if (disposed) throw new ObjectDisposedException(nameof(UIRetainerPreviewSession));
            if (EditorApplication.isPlayingOrWillChangePlaymode) return Fail("Live preview is available outside Play Mode.");
            if (source == null || source.Content == null) return Fail("Assign the retainer's Content child.");
            if (source.Content.parent != source.transform) return Fail("Content must be an immediate child of the retainer.");
            if (source.Content.GetComponentInChildren<Canvas>(true) != null || source.Content.GetComponentInChildren<UIRetainer>(true) != null)
                return Fail("Nested canvases and retainers are not supported.");
            settings.UpdateIfRequiredOrScript();
            Material effect = settings.FindProperty("effectMaterial").objectReferenceValue as Material;
            if (effect == null || effect.shader == null) return Fail("Assign an Effect Material to the retainer.");
            logicalSize = new Vector2(Mathf.Max(1f, logicalSize.x), Mathf.Max(1f, logicalSize.y));

            try
            {
                bool wasInvalid = Error != null;
                Error = null;
                EnsureScene();
                int contentHash = CalculateContentHash();
                bool rebuild = invalidated || wasInvalid || copy == null || contentHash != previousContentHash || previousSize != logicalSize;
                if (rebuild)
                {
                    RebuildVisualCopy(logicalSize);
                    previousSize = logicalSize;
                }

                float resolution = Mathf.Clamp(settings.FindProperty("resolutionScale").floatValue, 0.25f, 2f);
                int limit = Mathf.Min(Mathf.Clamp(settings.FindProperty("maximumTextureSize").intValue, 64, 4096), SystemInfo.maxTextureSize);
                float scale = Mathf.Min(resolution, limit / Mathf.Max(logicalSize.x, logicalSize.y));
                int width = Mathf.Max(1, Mathf.RoundToInt(logicalSize.x * scale));
                int height = Mathf.Max(1, Mathf.RoundToInt(logicalSize.y * scale));
                bool resized = EnsureTexture(width, height);
                if (rebuild || resized)
                {
                    RenderCopy(logicalSize);
                    // Canvas layout can finish authoring-side validation during the forced update.
                    previousContentHash = CalculateContentHash();
                }

                RawImage display = source.GetComponent<RawImage>();
                int materialHash = CalculateMaterialHash(effect, display);
                bool updateMaterial = invalidated || material == null || effect != sourceMaterial || materialHash != previousMaterialHash;
                if (updateMaterial)
                {
                    if (material == null || effect != sourceMaterial || material.shader != effect.shader)
                    {
                        DestroyOwned(material);
                        material = new Material(effect) { name = "Retainer Preview Effect", hideFlags = HideFlags.HideAndDontSave };
                        sourceMaterial = effect;
                    }
                    else material.CopyPropertiesFromMaterial(effect);
                    if (material.shader.keywordSpace.FindKeyword("RETAINER_EDITOR_PREVIEW").isValid)
                        material.EnableKeyword("RETAINER_EDITOR_PREVIEW");
                    material.SetFloat("_Curvature", source.Curvature);
                    Vector2 perspective = UIRetainerWarp.ClampPerspective(source.Perspective);
                    material.SetVector("_Perspective", new Vector4(perspective.x, perspective.y, 0f, 0f));
                    material.SetFloat("_PerspectiveFOV", source.PerspectiveFOV);
                    material.SetFloat("_FitContent", source.FitContent ? 1f : 0f);
                    if (material.HasProperty("_Color")) material.SetColor("_Color", effect.GetColor("_Color") * display.color);
                    previousMaterialHash = materialHash;
                }
                invalidated = false;
                return rebuild || resized || updateMaterial;
            }
            catch (Exception exception)
            {
                ReleaseScene();
                return Fail("Preview could not render: " + exception.Message);
            }
        }

        private bool Fail(string message)
        {
            bool changed = Error != message;
            Error = message;
            if (scene.IsValid()) ReleaseScene();
            return changed;
        }

        private int CalculateContentHash()
        {
            sourceComponents.Clear();
            source.Content.GetComponentsInChildren(true, sourceComponents);
            unchecked
            {
                int hash = source.Content.GetHashCode();
                foreach (Component component in sourceComponents)
                {
                    if (component == null) continue;
                    hash = hash * 31 + component.GetHashCode();
                    hash = hash * 31 + EditorUtility.GetDirtyCount(component);
                    if (component is Transform child) hash = hash * 31 + (child.gameObject.activeSelf ? 1 : 0);
                }
                Canvas parent = source.GetComponentInParent<Canvas>();
                if (parent != null) hash = hash * 31 + EditorUtility.GetDirtyCount(parent);
                return hash;
            }
        }

        private int CalculateMaterialHash(Material effect, RawImage display)
        {
            unchecked
            {
                int hash = effect.GetHashCode() * 31 + EditorUtility.GetDirtyCount(effect);
                hash = hash * 31 + source.Curvature.GetHashCode();
                hash = hash * 31 + source.Perspective.GetHashCode();
                hash = hash * 31 + source.PerspectiveFOV.GetHashCode();
                hash = hash * 31 + source.FitContent.GetHashCode();
                return hash * 31 + display.color.GetHashCode();
            }
        }

        private void EnsureScene()
        {
            if (scene.IsValid()) return;
            scene = EditorSceneManager.NewPreviewScene();
            canvasObject = new GameObject("Retainer Preview Canvas", typeof(RectTransform), typeof(Canvas));
            canvasObject.hideFlags = HideFlags.HideAndDontSave;
            canvasObject.layer = 31;
            SceneManager.MoveGameObjectToScene(canvasObject, scene);
            canvasObject.SetActive(false);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            GameObject cameraObject = new GameObject("Retainer Preview Camera", typeof(Camera));
            cameraObject.hideFlags = HideFlags.HideAndDontSave;
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            camera = cameraObject.GetComponent<Camera>();
            camera.enabled = false;
            camera.scene = scene;
            camera.transform.position = new Vector3(0f, 0f, -1000f);
            camera.orthographic = true;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 2000f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.cullingMask = 1 << 31;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            canvas.worldCamera = camera;
        }

        private void RebuildVisualCopy(Vector2 logicalSize)
        {
            canvasObject.SetActive(false);
            if (copy != null) DestroyOwned(copy.gameObject);
            RectTransform root = (RectTransform)canvasObject.transform;
            root.sizeDelta = logicalSize;
            root.position = Vector3.zero;
            root.rotation = Quaternion.identity;
            root.localScale = Vector3.one;
            Canvas parent = source.GetComponentInParent<Canvas>();
            canvas.additionalShaderChannels = parent != null ? parent.additionalShaderChannels :
                AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent;
            canvas.referencePixelsPerUnit = parent != null ? parent.referencePixelsPerUnit : 100f;
            copy = Object.Instantiate(source.Content, root, false);
            copy.name = "Retainer Preview Content";
            foreach (Transform child in copy.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.hideFlags = HideFlags.HideAndDontSave;
                child.gameObject.layer = 31;
            }
            // Instantiate beneath an inactive parent, then suppress app controllers before activation.
            foreach (MonoBehaviour behavior in copy.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behavior == null) continue;
                if (behavior is Slider slider) slider.onValueChanged = new Slider.SliderEvent();
                if (behavior is Scrollbar scrollbar) scrollbar.onValueChanged = new Scrollbar.ScrollEvent();
                if (!IsVisualBehavior(behavior)) behavior.enabled = false;
            }
            canvasObject.SetActive(true);
            // Toggle graphics use renderer alpha, which is not part of their serialized Image state.
            foreach (Toggle toggle in copy.GetComponentsInChildren<Toggle>(true))
                if (toggle.graphic != null && toggle.graphic.transform.IsChildOf(copy))
                    toggle.graphic.canvasRenderer.SetAlpha(toggle.isOn ? 1f : 0f);
        }

        private bool IsVisualBehavior(MonoBehaviour behavior)
        {
            // Only built-in controls may initialize driven rectangles; their copied callbacks are cleared.
            if (behavior is Slider slider) return behavior.GetType() == typeof(Slider) && IsCopiedRect(slider.fillRect) && IsCopiedRect(slider.handleRect);
            if (behavior is Scrollbar scrollbar) return behavior.GetType() == typeof(Scrollbar) && IsCopiedRect(scrollbar.handleRect);
            return behavior is Graphic || behavior is BaseMeshEffect || behavior is Mask || behavior is RectMask2D ||
                behavior is LayoutGroup || behavior is LayoutElement || behavior is ContentSizeFitter || behavior is AspectRatioFitter;
        }

        private bool IsCopiedRect(RectTransform rect) => rect == null || rect.IsChildOf(copy);

        private bool EnsureTexture(int width, int height)
        {
            if (texture != null && texture.width == width && texture.height == height && texture.IsCreated()) return false;
            if (camera != null) camera.targetTexture = null;
            if (texture != null) { texture.Release(); DestroyOwned(texture); }
            texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = "Retainer Editor Preview", hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
            };
            texture.Create();
            return true;
        }

        private void RenderCopy(Vector2 logicalSize)
        {
            camera.orthographicSize = logicalSize.y * 0.5f;
            camera.targetTexture = texture;
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)canvasObject.transform);
            foreach (TMPro.TextMeshProUGUI text in copy.GetComponentsInChildren<TMPro.TextMeshProUGUI>()) text.ForceMeshUpdate();
            foreach (Graphic graphic in copy.GetComponentsInChildren<Graphic>()) graphic.Rebuild(CanvasUpdate.PreRender);
            RenderPipeline.StandardRequest request = new RenderPipeline.StandardRequest { destination = texture };
            RenderTexture previous = RenderTexture.active;
            try
            {
                if (RenderPipeline.SupportsRenderRequest(camera, request)) RenderPipeline.SubmitRenderRequest(camera, request);
                else camera.Render();
            }
            finally { RenderTexture.active = previous; camera.targetTexture = null; }
        }

        private void ReleaseScene()
        {
            if (camera != null) camera.targetTexture = null;
            if (texture != null) { texture.Release(); DestroyOwned(texture); }
            DestroyOwned(material);
            if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            texture = null; material = null; sourceMaterial = null; canvasObject = null; canvas = null; camera = null; copy = null;
            scene = default;
            invalidated = true;
        }

        private static void DestroyOwned(Object value) { if (value != null) Object.DestroyImmediate(value); }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            ReleaseScene();
            settings.Dispose();
            sourceComponents.Clear();
        }
    }
}
