using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UnscriptedEngine.UI
{
    /// <summary>Canvas-wide, screen-distance-weighted movement sway and jump/landing impulses.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    [DefaultExecutionOrder(-100)]
    [AddComponentMenu("UnscriptedEngine/UI/Movement Sway")]
    public sealed class UIMovementSway : MonoBehaviour
    {
        [Header("Source")]
        [Tooltip("Optional explicit source. Otherwise follows the owning canvas/context pawn, then a unique local movement component.")]
        [SerializeField] private CharacterMovementComponent movementSource;
        [Tooltip("Optional view transform for camera-relative movement and turning. Otherwise uses the canvas camera, main camera, or movement transform.")]
        [SerializeField] private Transform view;
        [Header("Motion")]
        [Range(0f, 2f)] [SerializeField] private float intensity = 1f;
        [Min(0.01f)] [SerializeField] private float referenceSpeed = 6f;
        [Tooltip("Maximum movement displacement in each target's local UI units.")]
        [SerializeField] private Vector2 shift = new Vector2(8f, 5f);
        [Tooltip("Maximum pitch and yaw in degrees.")]
        [SerializeField] private Vector2 tilt = new Vector2(1.5f, 1.5f);
        [SerializeField] private float roll = 1f;
        [Range(0f, 1f)] [SerializeField] private float lookInfluence = 0.2f;
        [Min(0.1f)] [SerializeField] private float followSpeed = 9f;
        [Header("Jump and landing")]
        [Tooltip("Downward spring velocity on takeoff, in local UI units per second.")]
        [Min(0f)] [SerializeField] private float jumpImpulse = 35f;
        [Tooltip("Downward spring velocity when landing at Reference Speed. Scales with observed fall speed, up to twice this value.")]
        [Min(0f)] [SerializeField] private float landingImpulse = 60f;
        [Range(0.1f, 20f)] [SerializeField] private float springFrequency = 3.5f;
        [Range(0.1f, 1f)] [SerializeField] private float springDamping = 0.55f;
        [Header("Screen falloff")]
        [Tooltip("Motion at screen center relative to screen edges. Zero keeps centered elements still.")]
        [Range(0f, 1f)] [SerializeField] private float centerStrength;
        [Min(0.1f)] [SerializeField] private float edgePower = 1.5f;
        [Header("Targets")]
        [Tooltip("Leave empty for automatic groups. Full-screen wrappers are traversed; smaller branches move as one group. Explicit targets must not overlap.")]
        [SerializeField] private RectTransform[] targets = new RectTransform[0];
        [SerializeField] private RectTransform[] excludedRoots = new RectTransform[0];

        private sealed class TargetPose
        {
            internal RectTransform Rect;
            internal UIRetainer Retainer;
            internal Vector3 Offset;
            internal Quaternion Rotation = Quaternion.identity;
            internal Vector3 LastOffset;
            internal Quaternion LastRotation = Quaternion.identity;
            internal float Scale = 1f;
            internal float LastScale = 1f;
            internal readonly List<RectTransform> Graphics = new List<RectTransform>();
        }

        private readonly List<TargetPose> poses = new List<TargetPose>();
        private readonly List<RectTransform> visited = new List<RectTransform>();
        private readonly Vector3[] captureCorners = new Vector3[4];
        private RectTransform root;
        private Canvas canvas;
        private UCanvasComponent ownerCanvas;
        private UCanvasController canvasController;
        private TaggedValueContext context;
        private GameObject previousActor;
        private CharacterMovementComponent resolvedMovement;
        private Transform resolvedView;
        private Vector2 previousLook;
        private Vector2 motion;
        private float springPosition;
        private float springVelocity;
        private float fallSpeed;
        private bool sampled;
        private bool grounded;
        private bool flying;
        private bool targetsDirty;
        private double nextDiscovery;
        public CharacterMovementComponent ResolvedMovement => resolvedMovement;

        public void SetMovementSource(CharacterMovementComponent source)
        {
            movementSource = source;
            SetResolvedMovement(source);
            nextDiscovery = 0d;
        }

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            root = (RectTransform)transform;
            canvas = GetComponentInParent<Canvas>();
            ownerCanvas = UIRetainerCaptureOwner.FindLogicalParent<UCanvasComponent>(this);
            canvasController = UIRetainerCaptureOwner.FindLogicalParent<UCanvasController>(this);
            context = UIRetainerCaptureOwner.FindLogicalParent<TaggedValueContext>(this);
            // Register after uGUI's layout pass, so layout-controlled targets can also sway.
            CanvasUpdateRegistry registry = CanvasUpdateRegistry.instance;
            Canvas.preWillRenderCanvases += RemoveOffsets;
            Canvas.willRenderCanvases += ApplyOffsets;
            nextDiscovery = 0d;
            targetsDirty = true;
        }

        private void Update() => RemoveOffsets();
        private void OnTransformChildrenChanged() => targetsDirty = true;

        private void LateUpdate()
        {
            if (!Application.isPlaying) return;
            ResolveMovement();
            if (Time.unscaledTimeAsDouble >= nextDiscovery)
            {
                targetsDirty = true;
                nextDiscovery = Time.unscaledTimeAsDouble + 1d;
            }
            if (targetsDirty) RefreshTargets();
            float deltaTime = Mathf.Min(Time.deltaTime, 0.1f);
            if (deltaTime <= 0f) return;
            Vector2 desired = Vector2.zero;
            if (resolvedMovement != null && resolvedMovement.isActiveAndEnabled)
            {
                Transform basis = view != null ? view : resolvedView != null ? resolvedView : resolvedMovement.transform;
                Vector3 velocity = resolvedMovement.Velocity;
                Vector3 local = Quaternion.Inverse(Quaternion.Euler(0f, basis.eulerAngles.y, 0f)) * velocity;
                Vector2 look = new Vector2(basis.eulerAngles.x, basis.eulerAngles.y);
                Vector2 lookVelocity = sampled ? new Vector2(Mathf.DeltaAngle(previousLook.x, look.x),
                    Mathf.DeltaAngle(previousLook.y, look.y)) / (180f * deltaTime) : Vector2.zero;
                desired = new Vector2(-local.x, -local.z * 0.35f - velocity.y * 0.15f) / Mathf.Max(0.01f, referenceSpeed);
                desired += new Vector2(-lookVelocity.y, lookVelocity.x) * lookInfluence;
                desired = Vector2.ClampMagnitude(desired, 1f);
                SampleGroundTransitions(velocity.y);
                previousLook = look;
            }
            else sampled = false;
            motion = Vector2.Lerp(motion, desired, 1f - Mathf.Exp(-Mathf.Max(0.1f, followSpeed) * deltaTime));
            AdvanceSpring(deltaTime);
        }

        private void ResolveMovement()
        {
            UController controller = ownerCanvas != null ? ownerCanvas.OwningController :
                canvasController != null ? canvasController.OwningController : null;
            GameObject actor = context != null ? context.SourceRoot :
                controller != null && controller.Pawn != null ? controller.Pawn.gameObject : null;
            if (movementSource != null) { SetResolvedMovement(movementSource); return; }
            if (actor != previousActor || (actor != null && resolvedMovement == null && Time.unscaledTimeAsDouble >= nextDiscovery))
            {
                previousActor = actor;
                SetResolvedMovement(actor != null ? actor.GetComponentInChildren<CharacterMovementComponent>() : null);
            }
            if (actor != null || context != null || ownerCanvas != null || canvasController != null)
            {
                if (actor == null) SetResolvedMovement(null);
                return;
            }
            if (Time.unscaledTimeAsDouble < nextDiscovery) return;
            CharacterMovementComponent[] candidates = FindObjectsByType<CharacterMovementComponent>(FindObjectsSortMode.None);
            CharacterMovementComponent selected = null;
            foreach (CharacterMovementComponent candidate in candidates)
            {
                if (!candidate.isActiveAndEnabled || (candidate.IsSpawned && !candidate.IsOwner)) continue;
                if (selected != null) { selected = null; break; }
                selected = candidate;
            }
            SetResolvedMovement(selected);
        }

        private void SetResolvedMovement(CharacterMovementComponent source)
        {
            if (resolvedMovement == source) return;
            resolvedMovement = source;
            Camera camera = canvas != null && canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
            resolvedView = camera != null ? camera.transform : null;
            sampled = false;
            motion = Vector2.zero;
            springPosition = springVelocity = fallSpeed = 0f;
        }

        private void SampleGroundTransitions(float verticalSpeed)
        {
            bool nextGrounded = resolvedMovement.IsGrounded;
            bool nextFlying = resolvedMovement.IsFlying;
            if (sampled && !flying && !nextFlying)
            {
                if (grounded && !nextGrounded && verticalSpeed > 0.5f) springVelocity -= jumpImpulse;
                if (!grounded && nextGrounded && fallSpeed > 0.5f)
                    springVelocity -= landingImpulse * Mathf.Clamp(fallSpeed / Mathf.Max(0.01f, referenceSpeed), 0f, 2f);
            }
            fallSpeed = nextGrounded || nextFlying ? 0f : Mathf.Max(fallSpeed, -verticalSpeed);
            grounded = nextGrounded;
            flying = nextFlying;
            sampled = true;
        }

        private void AdvanceSpring(float deltaTime)
        {
            float frequency = 2f * Mathf.PI * Mathf.Clamp(springFrequency, 0.1f, 20f);
            int steps = Mathf.Max(1, Mathf.CeilToInt(deltaTime / 0.004f));
            float step = deltaTime / steps;
            for (int i = 0; i < steps; i++)
            {
                springVelocity += (-frequency * frequency * springPosition - 2f * springDamping * frequency * springVelocity) * step;
                springPosition = Mathf.Clamp(springPosition + springVelocity * step, -20f, 20f);
            }
        }

        /// <summary>Rediscover groups after adding UI. Automatic discovery also runs once per second.</summary>
        public void RefreshTargets()
        {
            RemoveOffsets();
            poses.Clear();
            visited.Clear();
            if (root == null) return;
            if (targets != null && targets.Length > 0)
            {
                foreach (RectTransform target in targets)
                {
                    if (target == null || target == root || !BelongsToRoot(target) || IsExcluded(target)) continue;
                    bool overlaps = false;
                    foreach (RectTransform other in targets)
                        if (other != null && other != target && target.IsChildOf(other)) { overlaps = true; break; }
                    if (!overlaps) AddTarget(target, FindRetainer(target));
                }
            }
            else CollectChildren(root, null);
            targetsDirty = false;
        }

        private bool BelongsToRoot(RectTransform target)
        {
            if (target.IsChildOf(root)) return true;
            UIRetainer retainer = FindRetainer(target);
            return retainer != null && retainer.transform.IsChildOf(root);
        }

        private UIRetainer FindRetainer(RectTransform target)
        {
            UIRetainerCaptureOwner capture = target.GetComponentInParent<UIRetainerCaptureOwner>();
            return capture != null ? capture.Owner : target.GetComponentInParent<UIRetainer>();
        }

        private bool IsExcluded(RectTransform target)
        {
            if (excludedRoots == null) return false;
            foreach (RectTransform excluded in excludedRoots)
                if (excluded != null && (target == excluded || target.IsChildOf(excluded))) return true;
            return false;
        }

        private void CollectChildren(RectTransform parent, UIRetainer retainer)
        {
            foreach (Transform child in parent)
            {
                RectTransform rect = child as RectTransform;
                if (rect == null || IsExcluded(rect) || visited.Contains(rect)) continue;
                visited.Add(rect);
                if (rect.TryGetComponent(out UIMovementSway nested) && nested != this && nested.isActiveAndEnabled) continue;
                if (rect.TryGetComponent(out UIRetainer capture) && capture.Content != null)
                {
                    if (capture.Content.childCount == 0 && capture.Content.GetComponent<Graphic>() != null) AddTarget(capture.Content, capture);
                    else CollectChildren(capture.Content, capture);
                    continue;
                }
                Rect reference = retainer != null ? ((RectTransform)retainer.transform).rect : root.rect;
                Vector2 size = rect.rect.size;
                bool wrapper = size.x <= 1f || size.y <= 1f ||
                    (size.x >= reference.width * 0.75f && size.y >= reference.height * 0.75f);
                if (wrapper && rect.childCount > 0) CollectChildren(rect, retainer);
                else if (rect.GetComponentInChildren<Graphic>(true) != null) AddTarget(rect, retainer);
            }
        }

        private void AddTarget(RectTransform rect, UIRetainer retainer)
        {
            foreach (TargetPose pose in poses) if (pose.Rect == rect) return;
            TargetPose target = new TargetPose { Rect = rect, Retainer = retainer };
            if (retainer != null)
                foreach (Graphic graphic in rect.GetComponentsInChildren<Graphic>(true))
                {
                    // Scroll content outside an authored mask is intentionally hidden. Measure
                    // its viewport, not the entire offscreen list, when fitting the moving group.
                    RectTransform bounds = graphic.rectTransform;
                    Transform current = graphic.transform;
                    while (current != null && current != rect.parent)
                    {
                        if ((current.TryGetComponent(out RectMask2D rectangleMask) && rectangleMask.IsActive()) ||
                            (current.TryGetComponent(out Mask stencilMask) && stencilMask.IsActive())) bounds = (RectTransform)current;
                        current = current.parent;
                    }
                    if (!target.Graphics.Contains(bounds)) target.Graphics.Add(bounds);
                }
            poses.Add(target);
        }

        private void RemoveOffsets()
        {
            foreach (TargetPose pose in poses)
            {
                if (pose.Rect == null) continue;
                if (pose.Offset.sqrMagnitude == 0f && pose.Rotation.Equals(Quaternion.identity) && pose.Scale == 1f) continue;
                pose.Rect.anchoredPosition3D -= pose.Offset;
                pose.Rect.localRotation *= Quaternion.Inverse(pose.Rotation);
                pose.Rect.localScale /= pose.Scale;
                pose.Offset = Vector3.zero;
                pose.Rotation = Quaternion.identity;
                pose.Scale = 1f;
            }
        }

        private void ApplyOffsets()
        {
            if (!isActiveAndEnabled || !Application.isPlaying) return;
            RemoveOffsets();
            Camera screenCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (canvas != null && canvas.renderMode == RenderMode.WorldSpace && screenCamera == null) screenCamera = Camera.main;
            Rect screen = screenCamera != null ? screenCamera.pixelRect : new Rect(0f, 0f, Screen.width, Screen.height);
            foreach (TargetPose pose in poses)
            {
                if (pose.Rect == null || !pose.Rect.gameObject.activeInHierarchy) continue;
                Vector2 position;
                if (pose.Retainer != null && pose.Retainer.CaptureRaycaster != null && pose.Rect.IsChildOf(pose.Retainer.Content))
                {
                    // Captured transforms live in a different camera space; project through the same warp.
                    Camera captureCamera = pose.Retainer.CaptureRaycaster.eventCamera;
                    Vector2 pixel = RectTransformUtility.WorldToScreenPoint(captureCamera, pose.Rect.TransformPoint(pose.Rect.rect.center));
                    RenderTexture texture = pose.Retainer.Texture;
                    if (texture == null) continue;
                    Vector2 uv = pose.Retainer.MapFromSource(new Vector2(pixel.x / texture.width, pixel.y / texture.height));
                    RectTransform display = (RectTransform)pose.Retainer.transform;
                    Vector2 local = display.rect.min + Vector2.Scale(uv, display.rect.size);
                    position = RectTransformUtility.WorldToScreenPoint(pose.Retainer.DisplayCamera, display.TransformPoint(local));
                }
                else position = RectTransformUtility.WorldToScreenPoint(screenCamera, pose.Rect.TransformPoint(pose.Rect.rect.center));
                Vector2 distance = new Vector2((position.x - screen.center.x) / Mathf.Max(1f, screen.width * 0.5f),
                    (position.y - screen.center.y) / Mathf.Max(1f, screen.height * 0.5f));
                float weight = Mathf.Lerp(centerStrength, 1f, Mathf.Pow(Mathf.Clamp01(distance.magnitude), Mathf.Max(0.1f, edgePower))) * intensity;
                pose.Offset = new Vector3(motion.x * shift.x, motion.y * shift.y + springPosition, 0f) * weight;
                pose.Rotation = Quaternion.Euler((-motion.y * tilt.x - springPosition * 0.15f) * weight,
                    motion.x * tilt.y * weight, -motion.x * roll * weight);
                pose.Rect.anchoredPosition3D += pose.Offset;
                pose.Rect.localRotation *= pose.Rotation;
                FitCapturedPose(pose);
                if (pose.Retainer != null && (pose.LastOffset != pose.Offset || pose.LastRotation != pose.Rotation || pose.LastScale != pose.Scale)) pose.Retainer.RequestRefresh();
                pose.LastOffset = pose.Offset;
                pose.LastRotation = pose.Rotation;
                pose.LastScale = pose.Scale;
            }
        }

        private void FitCapturedPose(TargetPose pose)
        {
            if (pose.Retainer == null || pose.Retainer.CaptureRaycaster == null || !pose.Rect.IsChildOf(pose.Retainer.Content)) return;
            if (pose.Offset.sqrMagnitude == 0f && pose.Rotation.Equals(Quaternion.identity)) return;
            Camera camera = pose.Retainer.CaptureRaycaster.eventCamera;
            if (camera == null || !MeasureCaptureBounds(pose, camera, out Bounds bounds)) return;
            // Uniform scale only when rotation makes the group wider/taller than the capture.
            // Translation alone is corrected inward, keeping the authored size whenever possible.
            pose.Scale = Mathf.Min(1f, 1f / Mathf.Max(0.0001f, bounds.size.x), 1f / Mathf.Max(0.0001f, bounds.size.y));
            if (pose.Scale < 1f)
            {
                pose.Rect.localScale *= pose.Scale;
                MeasureCaptureBounds(pose, camera, out bounds);
            }
            Vector2 correction = new Vector2(bounds.min.x < 0f ? -bounds.min.x : bounds.max.x > 1f ? 1f - bounds.max.x : 0f,
                bounds.min.y < 0f ? -bounds.min.y : bounds.max.y > 1f ? 1f - bounds.max.y : 0f);
            if (correction.sqrMagnitude == 0f) return;
            Vector3 world = camera.ViewportToWorldPoint(new Vector3(correction.x, correction.y, bounds.center.z)) -
                camera.ViewportToWorldPoint(new Vector3(0f, 0f, bounds.center.z));
            Vector3 local = pose.Rect.parent.InverseTransformVector(world);
            pose.Rect.anchoredPosition3D += local;
            pose.Offset += local;
        }

        private bool MeasureCaptureBounds(TargetPose pose, Camera camera, out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            foreach (RectTransform graphic in pose.Graphics)
            {
                if (graphic == null || !graphic.gameObject.activeInHierarchy) continue;
                graphic.GetWorldCorners(captureCorners);
                foreach (Vector3 corner in captureCorners)
                {
                    Vector3 viewport = camera.WorldToViewportPoint(corner);
                    if (!found) { bounds = new Bounds(viewport, Vector3.zero); found = true; }
                    else bounds.Encapsulate(viewport);
                }
            }
            return found;
        }

        private void OnDisable()
        {
            Canvas.preWillRenderCanvases -= RemoveOffsets;
            Canvas.willRenderCanvases -= ApplyOffsets;
            RemoveOffsets();
            foreach (TargetPose pose in poses) if (pose.Retainer != null) pose.Retainer.RequestRefresh();
            poses.Clear();
            sampled = false;
            motion = Vector2.zero;
            springPosition = springVelocity = fallSpeed = 0f;
        }
    }
}
