using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UnscriptedEngine.UI
{
    /// <summary>Captures one UI subtree and displays it through an effect material.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RawImage), typeof(UIRetainerInput))]
    [AddComponentMenu("UnscriptedEngine/UI/Retainer")]
    public sealed class UIRetainer : MonoBehaviour
    {
        [Header("Content")]
        [Tooltip("Immediate child stretched to this panel. Moved into an owned capture canvas during play, restored on disable.")]
        [SerializeField]
        private RectTransform content;
        [Tooltip("Retainer Warp material asset. Keep an asset reference so the shader is included in builds.")]
        [SerializeField]
        private Material effectMaterial;
        [Tooltip("Dedicated capture layer (0–31). Exclude this layer from gameplay cameras and use it for captured UI only.")]
        [Range(0, 31)]
        [SerializeField]
        private int captureLayer = 31;
        [Header("Rendering")]
        [Tooltip("Texture pixels per displayed screen pixel. Lower values reduce GPU and memory cost.")]
        [Range(0.25f, 2f)]
        [SerializeField]
        private float resolutionScale = 1f;
        [Tooltip("Maximum texture dimension. Aspect ratio is preserved when clamping.")]
        [Range(64, 4096)]
        [SerializeField]
        private int maximumTextureSize = 2048;
        [Tooltip("Capture frames per second using unscaled time. Zero captures only on enable, resize, or RequestRefresh.")]
        [Range(0f, 120f)]
        [SerializeField]
        private float refreshRate = 60f;
        [Header("Warp")]
        [Tooltip("Radial curvature. Zero disables curvature; positive values bend the panel inward.")]
        [Range(0f, 0.5f)]
        [SerializeField]
        private float curvature = 0.08f;
        [Tooltip("Horizontal and vertical perspective strength. Combined absolute strength is clamped below one.")]
        [SerializeField]
        private Vector2 perspective;
        [Header("Debug")]
        [Tooltip("Draw the capture rectangle and texture size label when selected.")]
        [SerializeField]
        private bool showGizmos = true;

        private RawImage display;
        private GameObject captureRoot;
        private Camera captureCamera;
        private Canvas captureCanvas;
        private GraphicRaycaster captureRaycaster;
        private RenderTexture texture;
        private Material materialInstance;
        private Texture previousTexture;
        private Material previousMaterial;
        private Rect previousUVRect;
        private Transform previousParent;
        private int previousSibling;
        private readonly Dictionary<GameObject, int> previousLayers = new Dictionary<GameObject, int>();
        private readonly List<GameObject> releasedLayers = new List<GameObject>();
        private readonly Vector3[] corners = new Vector3[4];
        private bool refreshRequested;
        private double nextCaptureTime;
        private Vector2 previousLogicalSize;
        private Vector2 originalAnchorMin;
        private Vector2 originalAnchorMax;
        private Vector2 originalPivot;
        private Vector2 originalSizeDelta;
        private Vector3 originalPosition;
        private Vector3 originalScale;
        private Quaternion originalRotation;

        public RectTransform Content => content;
        public GraphicRaycaster CaptureRaycaster => captureRaycaster;
        public RenderTexture Texture => texture;
        public Camera DisplayCamera
        {
            get
            {
                Canvas canvas = display != null ? display.canvas : null;
                if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay) return null;
                if (canvas.worldCamera != null) return canvas.worldCamera;
                return canvas.renderMode == RenderMode.WorldSpace ? Camera.main : null;
            }
        }
        public float Curvature { get => curvature; set => curvature = Mathf.Clamp(value, 0f, 0.5f); }
        public Vector2 Perspective { get => perspective; set => perspective = ClampPerspective(value); }
        public Vector2 MapToSource(Vector2 uv) => UIRetainerWarp.MapToSource(uv, curvature, ClampPerspective(perspective));
        public void RequestRefresh() => refreshRequested = true;

        /// <summary>Call after adding or removing captured objects. Updates layer ownership and requests a capture.</summary>
        public void RefreshContent()
        {
            if (captureRoot == null || content == null) return;
            releasedLayers.Clear();
            foreach (KeyValuePair<GameObject, int> entry in previousLayers)
            {
                if (entry.Key != null && entry.Key.transform.IsChildOf(content)) continue;
                if (entry.Key != null) entry.Key.layer = entry.Value;
                releasedLayers.Add(entry.Key);
            }
            foreach (GameObject released in releasedLayers) previousLayers.Remove(released);
            foreach (Transform child in content.GetComponentsInChildren<Transform>(true))
            {
                if (!previousLayers.ContainsKey(child.gameObject)) previousLayers.Add(child.gameObject, child.gameObject.layer);
                child.gameObject.layer = captureLayer;
            }
            RequestRefresh();
        }

        private static Vector2 ClampPerspective(Vector2 value)
        {
            float magnitude = Mathf.Abs(value.x) + Mathf.Abs(value.y);
            return magnitude > 0.8f ? value * (0.8f / magnitude) : value;
        }

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            display = GetComponent<RawImage>();
            if (content == null || content.parent != transform || effectMaterial == null ||
                content.GetComponentInChildren<Canvas>(true) != null || GetComponentInParent<Canvas>() == null ||
                (transform.parent != null && transform.parent.GetComponentInParent<UIRetainer>() != null) ||
                content.GetComponentInChildren<UIRetainer>(true) != null)
            {
                Debug.LogError("UIRetainer needs an immediate content child without nested Canvases, a material, and no parent retainer.", this);
                enabled = false;
                return;
            }

            previousTexture = display.texture;
            previousMaterial = display.material;
            previousUVRect = display.uvRect;
            display.uvRect = new Rect(0f, 0f, 1f, 1f);
            previousParent = content.parent;
            previousSibling = content.GetSiblingIndex();
            originalAnchorMin = content.anchorMin;
            originalAnchorMax = content.anchorMax;
            originalPivot = content.pivot;
            originalSizeDelta = content.sizeDelta;
            originalPosition = content.anchoredPosition3D;
            originalScale = content.localScale;
            originalRotation = content.localRotation;
            materialInstance = new Material(effectMaterial) { hideFlags = HideFlags.DontSave };
            display.material = materialInstance;

            captureRoot = new GameObject("Retainer Capture", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            captureRoot.hideFlags = HideFlags.DontSave;
            captureRoot.layer = captureLayer;
            // Keep temporary objects in the owner's scene, including additive scenes.
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(captureRoot, gameObject.scene);
            GameObject cameraObject = new GameObject("Retainer Camera", typeof(Camera));
            cameraObject.hideFlags = HideFlags.DontSave;
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject, gameObject.scene);
            captureCamera = cameraObject.GetComponent<Camera>();
            captureCamera.enabled = false;
            captureCamera.orthographic = true;
            captureCamera.clearFlags = CameraClearFlags.SolidColor;
            captureCamera.backgroundColor = Color.clear;
            captureCamera.cullingMask = 1 << captureLayer;
            captureCamera.nearClipPlane = 0.1f;
            captureCamera.farClipPlane = 2f;
            captureCamera.allowHDR = false;
            captureCamera.allowMSAA = false;
            captureCamera.depth = -100f;

            captureCanvas = captureRoot.GetComponent<Canvas>();
            captureCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            captureCanvas.worldCamera = captureCamera;
            captureCanvas.planeDistance = 1f;
            captureCanvas.additionalShaderChannels = GetComponentInParent<Canvas>().additionalShaderChannels;
            captureCanvas.referencePixelsPerUnit = GetComponentInParent<Canvas>().referencePixelsPerUnit;
            captureRaycaster = captureRoot.GetComponent<GraphicRaycaster>();
            // Called explicitly by the input bridge; never participate in screen-space raycasts.
            captureRaycaster.enabled = false;
            content.SetParent(captureRoot.transform, false);
            RestoreContentRect();
            RefreshContent();
        }

        private void RestoreContentRect()
        {
            content.anchorMin = originalAnchorMin;
            content.anchorMax = originalAnchorMax;
            content.pivot = originalPivot;
            content.sizeDelta = originalSizeDelta;
            content.anchoredPosition3D = originalPosition;
            content.localScale = originalScale;
            content.localRotation = originalRotation;
        }

        private void LateUpdate()
        {
            if (captureCamera == null) return;
            RectTransform rect = (RectTransform)transform;
            rect.GetWorldCorners(corners);
            Camera screenCamera = DisplayCamera;
            Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(screenCamera, corners[0]);
            Vector2 topLeft = RectTransformUtility.WorldToScreenPoint(screenCamera, corners[1]);
            Vector2 bottomRight = RectTransformUtility.WorldToScreenPoint(screenCamera, corners[3]);
            float width = Mathf.Max(1f, Vector2.Distance(bottomLeft, bottomRight) * resolutionScale);
            float height = Mathf.Max(1f, Vector2.Distance(bottomLeft, topLeft) * resolutionScale);
            float scale = Mathf.Min(1f, Mathf.Min(maximumTextureSize, SystemInfo.maxTextureSize) / Mathf.Max(width, height));
            int pixelWidth = Mathf.Max(1, Mathf.RoundToInt(width * scale));
            int pixelHeight = Mathf.Max(1, Mathf.RoundToInt(height * scale));
            if (texture == null || texture.width != pixelWidth || texture.height != pixelHeight)
            {
                ReleaseTexture();
                texture = new RenderTexture(pixelWidth, pixelHeight, 24, RenderTextureFormat.ARGB32)
                {
                    name = "UI Retainer", hideFlags = HideFlags.DontSave,
                    filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
                };
                texture.Create();
                captureCamera.targetTexture = texture;
                display.texture = texture;
                RequestRefresh();
            }
            if (!texture.IsCreated()) { texture.Create(); RequestRefresh(); }
            float logicalWidth = Mathf.Max(1f, rect.rect.width);
            float logicalHeight = Mathf.Max(1f, rect.rect.height);
            Vector2 logicalSize = new Vector2(logicalWidth, logicalHeight);
            if (logicalSize != previousLogicalSize) { previousLogicalSize = logicalSize; RequestRefresh(); }
            captureCanvas.scaleFactor = pixelWidth / logicalWidth;
            captureCamera.orthographicSize = logicalHeight * 0.5f;
            materialInstance.SetFloat("_Curvature", curvature);
            Vector2 tilt = ClampPerspective(perspective);
            materialInstance.SetVector("_Perspective", new Vector4(tilt.x, tilt.y, 0f, 0f));

            bool due = refreshRequested || (refreshRate > 0f && Time.unscaledTimeAsDouble >= nextCaptureTime);
            captureCamera.enabled = due;
            if (due)
            {
                refreshRequested = false;
                nextCaptureTime = Time.unscaledTimeAsDouble + (refreshRate > 0f ? 1d / refreshRate : 0d);
            }
        }

        private void OnDisable()
        {
            if (captureRoot == null) return;
            GetComponent<UIRetainerInput>().CancelInput();
            if (content != null && previousParent != null)
            {
                content.SetParent(previousParent, false);
                content.SetSiblingIndex(previousSibling);
                RestoreContentRect();
            }
            foreach (KeyValuePair<GameObject, int> entry in previousLayers)
                if (entry.Key != null) entry.Key.layer = entry.Value;
            previousLayers.Clear();
            captureCamera.enabled = false;
            ReleaseTexture();
            display.texture = previousTexture;
            display.material = previousMaterial;
            display.uvRect = previousUVRect;
            Destroy(captureCamera.gameObject);
            Destroy(captureRoot);
            Destroy(materialInstance);
            captureRoot = null;
            captureCamera = null;
            captureRaycaster = null;
        }

        private void ReleaseTexture()
        {
            if (texture == null) return;
            captureCamera.targetTexture = null;
            texture.Release();
            Destroy(texture);
            texture = null;
        }

        private void OnDrawGizmosSelected()
        {
            if (!showGizmos) return;
            ((RectTransform)transform).GetWorldCorners(corners);
            Color previous = Gizmos.color;
            Gizmos.color = Color.cyan;
            for (int i = 0; i < 4; i++) Gizmos.DrawLine(corners[i], corners[(i + 1) % 4]);
            Gizmos.color = previous;
#if UNITY_EDITOR
            string label = texture != null ? $"Retainer: {texture.width} x {texture.height} px"
                : $"Retainer: max {maximumTextureSize} px, scale {resolutionScale:0.##}";
            UnityEditor.Handles.Label(corners[1], label);
#endif
        }
    }
}
