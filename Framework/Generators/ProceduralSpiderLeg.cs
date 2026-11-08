using System;
using UnityEngine;

namespace Framework.Components
{
    [Serializable]
    public sealed class SpiderLegSection
    {
        [Tooltip("Length of this rigid block at unit scale. Uniform hierarchy scale resizes it.")]
        [Min(0.01f)]
        [SerializeField]
        private float length = 0.8f;
        [Tooltip("Rest bend in degrees relative to the previous section. First section is relative to outward horizontal; positive angles rise above the body plane.")]
        [Range(-175f, 175f)]
        [SerializeField]
        private float bendAngle = -85f;

        public float Length => Mathf.Max(0.01f, length);
        public float BendAngle => bendAngle;

        public SpiderLegSection(float length, float bendAngle)
        {
            this.length = length;
            this.bendAngle = bendAngle;
        }
    }

    /// <summary>Independent surface probing, planted foot animation and variable-chain IK. Local Z points outward, Y away from the surface.</summary>
    [DisallowMultipleComponent]
    public sealed class ProceduralSpiderLeg : MonoBehaviour
    {
        [Tooltip("One block per entry. Each leg can use its own count, lengths and rest angles.")]
        [SerializeField]
        private SpiderLegSection[] sections = { new SpiderLegSection(0.8f, 40f), new SpiderLegSection(1.1f, -110f) };
        [Tooltip("Width and depth of each generated block at unit scale. Uniform hierarchy scale resizes it.")]
        [Min(0.005f)]
        [SerializeField]
        private float thickness = 0.1f;
        [Tooltip("Shared block material. No material instances are created.")]
        [SerializeField]
        private Material material;
        [Tooltip("Surfaces this leg may attach to. Exclude the spider's body layer.")]
        [SerializeField]
        private LayerMask surfaceLayers = ~0;
        [Tooltip("Probe start distance away from the resting foot, along body up, at unit scale.")]
        [Min(0.01f)]
        [SerializeField]
        private float probeHeight = 1f;
        [Tooltip("Probe distance below the resting foot at unit scale.")]
        [Min(0.01f)]
        [SerializeField]
        private float probeDepth = 2f;
        [Tooltip("Distance from desired footing before requesting a step, at unit scale.")]
        [Min(0.01f)]
        [SerializeField]
        private float stepDistance = 0.35f;
        [Tooltip("Seconds per step. Unaffected by spider scale.")]
        [Min(0.01f)]
        [SerializeField]
        private float stepDuration = 0.18f;
        [Tooltip("Foot lift away from the contact surface during a step, at unit scale.")]
        [Min(0f)]
        [SerializeField]
        private float stepHeight = 0.25f;
        [Tooltip("Bounded FABRIK iterations per frame. Higher values improve long-chain accuracy.")]
        [Range(1, 32)]
        [SerializeField]
        private int solverIterations = 12;
        [Header("Debug")]
        [Tooltip("Show labeled rest shape, block size, reach, footing probes and step dimensions when selected.")]
        [SerializeField]
        private bool showGizmos = true;
        [Tooltip("Generated block references. Managed by Rebuild Geometry; do not assign unrelated objects.")]
        [HideInInspector]
        [SerializeField]
        private Transform[] blocks = Array.Empty<Transform>();

        private Vector3[] joints;
        private float[] lengths;
        private float totalLength;
        private Vector3 foot;
        private Vector3 stepStart;
        private Vector3 stepEnd;
        private Vector3 stepNormal;
        private float stepProgress;
        private Transform plantedSurface;
        private Vector3 surfaceLocalPoint;
        private bool initialized;

        // Positive uniform scale is supported, including inherited scale from parent objects.
        private float WorldScale => Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.y));

        public bool IsStepping { get; private set; }
        public Vector3 FootPosition => foot;
        public int SectionCount => sections == null ? 0 : sections.Length;
        public float Reach => totalLength;

        private void OnEnable() { initialized = false; IsStepping = false; }
        private void OnDisable() { initialized = false; IsStepping = false; plantedSurface = null; }

        public void Configure(SpiderLegSection[] shape, Material sharedMaterial)
        {
            sections = shape == null ? Array.Empty<SpiderLegSection>() : (SpiderLegSection[])shape.Clone();
            material = sharedMaterial;
            initialized = false;
        }

        [ContextMenu("Rebuild Geometry")]
        public void RebuildGeometry()
        {
            for (int index = 0; index < blocks.Length; index++)
            {
                if (blocks[index] == null) continue;
                GameObject oldBlock = blocks[index].gameObject;
                oldBlock.SetActive(false);
                if (Application.isPlaying) Destroy(oldBlock);
                else DestroyImmediate(oldBlock);
            }

            blocks = new Transform[SectionCount];
            for (int index = 0; index < blocks.Length; index++)
            {
                GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                block.name = "Section " + (index + 1);
                block.layer = gameObject.layer;
                block.transform.SetParent(transform, false);
                Collider blockCollider = block.GetComponent<Collider>();
                blockCollider.enabled = false;
                if (Application.isPlaying) Destroy(blockCollider);
                else DestroyImmediate(blockCollider);
                if (material != null) block.GetComponent<Renderer>().sharedMaterial = material;
                blocks[index] = block.transform;
            }
            initialized = false;
            Initialize();
        }

        private void Initialize()
        {
            int count = SectionCount;
            joints = new Vector3[count + 1];
            lengths = new float[count];
            RefreshLengths();
            BuildRestPose();
            foot = joints[count];
            plantedSurface = null;
            if (TryFindFooting(out RaycastHit hit)) Plant(hit);
            initialized = true;
            IsStepping = false;
            Solve();
        }

        private void RefreshLengths()
        {
            float scale = WorldScale;
            totalLength = 0f;
            for (int index = 0; index < lengths.Length; index++)
            {
                lengths[index] = (sections[index] == null ? 0.01f : sections[index].Length) * scale;
                totalLength += lengths[index];
            }
        }

        private void BuildRestPose()
        {
            joints[0] = transform.position;
            float angle = 0f;
            for (int index = 0; index < lengths.Length; index++)
            {
                angle += sections[index] == null ? 0f : sections[index].BendAngle;
                float radians = angle * Mathf.Deg2Rad;
                Vector3 direction = transform.forward * Mathf.Cos(radians) + transform.up * Mathf.Sin(radians);
                joints[index + 1] = joints[index] + direction * lengths[index];
            }
        }

        private Vector3 RestFoot()
        {
            Vector3 position = transform.position;
            float angle = 0f;
            float scale = WorldScale;
            for (int index = 0; index < SectionCount; index++)
            {
                angle += sections[index] == null ? 0f : sections[index].BendAngle;
                float radians = angle * Mathf.Deg2Rad;
                float length = (sections[index] == null ? 0.01f : sections[index].Length) * scale;
                position += (transform.forward * Mathf.Cos(radians) + transform.up * Mathf.Sin(radians)) * length;
            }
            return position;
        }

        private bool TryFindFooting(out RaycastHit hit)
        {
            Vector3 rest = RestFoot();
            float scale = WorldScale;
            float height = probeHeight * scale;
            float distance = (probeHeight + probeDepth) * scale;
            if (Physics.Raycast(rest + transform.up * height, -transform.up, out hit,
                distance, surfaceLayers, QueryTriggerInteraction.Ignore)) return true;
            // At corners also look outward, then inward toward the body-facing wall.
            if (Physics.Raycast(transform.position, transform.forward, out hit, totalLength,
                surfaceLayers, QueryTriggerInteraction.Ignore)) return true;
            return Physics.Raycast(rest + transform.forward * height, -transform.forward, out hit,
                distance, surfaceLayers, QueryTriggerInteraction.Ignore);
        }

        private void Plant(RaycastHit hit)
        {
            foot = hit.point;
            plantedSurface = hit.transform;
            surfaceLocalPoint = plantedSurface.InverseTransformPoint(foot);
        }

        public bool TryBeginStep()
        {
            if (!initialized || IsStepping || lengths.Length == 0 || !TryFindFooting(out RaycastHit hit)) return false;
            float distance = stepDistance * WorldScale;
            if (plantedSurface != null && (hit.point - foot).sqrMagnitude < distance * distance &&
                (transform.position - foot).sqrMagnitude < totalLength * totalLength * 0.95f) return false;
            stepStart = foot;
            stepEnd = hit.point;
            stepNormal = hit.normal;
            plantedSurface = hit.transform;
            surfaceLocalPoint = plantedSurface.InverseTransformPoint(stepEnd);
            stepProgress = 0f;
            IsStepping = true;
            return true;
        }

        // Brain calls exactly once per frame; standalone users may call this from their own scheduler.
        public void Tick(float deltaTime)
        {
            if (!isActiveAndEnabled) return;
            if (!initialized)
            {
                if (blocks.Length != SectionCount) RebuildGeometry();
                else Initialize();
            }
            // Resizing preserves existing world-space contacts and never rebuilds block objects.
            RefreshLengths();
            if (IsStepping)
            {
                stepProgress = Mathf.Clamp01(stepProgress + Mathf.Max(0f, deltaTime) / Mathf.Max(0.01f, stepDuration));
                if (plantedSurface != null) stepEnd = plantedSurface.TransformPoint(surfaceLocalPoint);
                float blend = Mathf.SmoothStep(0f, 1f, stepProgress);
                foot = Vector3.Lerp(stepStart, stepEnd, blend) + stepNormal * (Mathf.Sin(stepProgress * Mathf.PI) * stepHeight * WorldScale);
                if (stepProgress >= 1f) { IsStepping = false; foot = stepEnd; }
            }
            else if (plantedSurface != null) foot = plantedSurface.TransformPoint(surfaceLocalPoint);
            else foot = RestFoot();
            Solve();
        }

        private void Solve()
        {
            BuildRestPose();
            int count = lengths.Length;
            Vector3 root = transform.position;
            Vector3 scale = transform.lossyScale;
            float worldScale = Mathf.Max(0.0001f, Mathf.Abs(scale.y));
            float toleranceSquared = 0.000001f * worldScale * worldScale;
            if ((foot - root).sqrMagnitude >= totalLength * totalLength)
            {
                Vector3 direction = (foot - root).normalized;
                for (int index = 0; index < count; index++) joints[index + 1] = joints[index] + direction * lengths[index];
            }
            else
            {
                for (int iteration = 0; iteration < solverIterations; iteration++)
                {
                    joints[count] = foot;
                    for (int index = count - 1; index >= 0; index--)
                        joints[index] = joints[index + 1] + SafeDirection(joints[index] - joints[index + 1], -transform.forward) * lengths[index];
                    joints[0] = root;
                    for (int index = 0; index < count; index++)
                        joints[index + 1] = joints[index] + SafeDirection(joints[index + 1] - joints[index], transform.forward) * lengths[index];
                    if ((joints[count] - foot).sqrMagnitude < toleranceSquared) break;
                }
            }
            float worldThickness = thickness * worldScale;
            for (int index = 0; index < count && index < blocks.Length; index++)
            {
                Transform block = blocks[index];
                if (block == null) continue;
                Vector3 direction = joints[index + 1] - joints[index];
                block.SetPositionAndRotation((joints[index] + joints[index + 1]) * 0.5f,
                    Quaternion.FromToRotation(Vector3.up, direction));
                block.localScale = new Vector3(worldThickness / Mathf.Max(0.0001f, Mathf.Abs(scale.x)),
                    lengths[index] / Mathf.Max(0.0001f, Mathf.Abs(scale.y)), worldThickness / Mathf.Max(0.0001f, Mathf.Abs(scale.z)));
            }
        }

        private static Vector3 SafeDirection(Vector3 direction, Vector3 fallback)
        {
            return direction.sqrMagnitude > 0.0000001f ? direction.normalized : fallback;
        }

        private void OnDrawGizmosSelected()
        {
            if (!showGizmos) return;
            float scale = WorldScale;
            Vector3 position = transform.position;
            float angle = 0f;
            float reach = 0f;
            for (int index = 0; index < SectionCount; index++)
            {
                angle += sections[index] == null ? 0f : sections[index].BendAngle;
                float length = (sections[index] == null ? 0.01f : sections[index].Length) * scale;
                float radians = angle * Mathf.Deg2Rad;
                Vector3 end = position + (transform.forward * Mathf.Cos(radians) + transform.up * Mathf.Sin(radians)) * length;
                Gizmos.color = Color.green;
                Gizmos.DrawLine(position, end);
                Label((position + end) * 0.5f, "Section " + (index + 1) + " rest length / bend");
                position = end;
                reach += length;
            }
            Vector3 rest = position;
            Gizmos.color = Color.gray;
            Gizmos.DrawWireSphere(transform.position, reach);
            Label(transform.position + transform.right * reach, "Maximum reach");
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(rest, stepDistance * scale);
            Label(rest + transform.right * stepDistance * scale, "Step distance");
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(rest, rest + transform.up * stepHeight * scale);
            Label(rest + transform.up * stepHeight * scale, "Step height");
            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(rest, Vector3.one * thickness * scale);
            Label(rest, "Rest foot / block thickness");
            DrawProbe(rest, transform.up, scale, "Surface probe");
            DrawProbe(rest, transform.forward, scale, "Corner probe");
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * reach);
            Label(transform.position + transform.forward * reach, "Outward probe");
            if (initialized)
            {
                Gizmos.DrawWireSphere(foot, 0.06f * scale);
                Label(foot, "Current foot");
            }
        }

        private void DrawProbe(Vector3 rest, Vector3 axis, float scale, string label)
        {
            Gizmos.color = Color.cyan;
            Vector3 start = rest + axis * probeHeight * scale;
            Vector3 end = rest - axis * probeDepth * scale;
            Gizmos.DrawLine(start, end);
            Gizmos.DrawWireSphere(start, 0.03f * scale);
            Label(start, label + " height");
            Label(end, label + " depth");
        }

        private static void Label(Vector3 position, string text)
        {
#if UNITY_EDITOR
            UnityEditor.Handles.Label(position, text);
#endif
        }
    }
}
