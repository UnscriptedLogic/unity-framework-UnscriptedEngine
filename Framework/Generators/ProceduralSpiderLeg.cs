using System;
using UnityEngine;

namespace Framework.Components
{
    [Serializable]
    public sealed class SpiderLegSection
    {
        [Tooltip("Length of this rigid block in world units."), Min(0.01f)]
        [SerializeField] private float length = 0.8f;
        [Tooltip("Rest bend in degrees relative to the previous section. First section is relative to outward horizontal; positive angles rise above the body plane."), Range(-175f, 175f)]
        [SerializeField] private float bendAngle = -85f;

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
        [SerializeField] private SpiderLegSection[] sections = { new SpiderLegSection(0.8f, 40f), new SpiderLegSection(1.1f, -110f) };
        [Tooltip("Width and depth of each generated block in world units."), Min(0.005f)]
        [SerializeField] private float thickness = 0.1f;
        [Tooltip("Shared block material. No material instances are created.")]
        [SerializeField] private Material material;
        [Tooltip("Surfaces this leg may attach to. Exclude the spider's body layer.")]
        [SerializeField] private LayerMask surfaceLayers = ~0;
        [Tooltip("Probe start distance away from the resting foot, along body up."), Min(0.01f)]
        [SerializeField] private float probeHeight = 1f;
        [Tooltip("Probe distance below the resting foot."), Min(0.01f)]
        [SerializeField] private float probeDepth = 2f;
        [Tooltip("Distance from desired footing before requesting a step."), Min(0.01f)]
        [SerializeField] private float stepDistance = 0.35f;
        [Tooltip("Seconds per step."), Min(0.01f)]
        [SerializeField] private float stepDuration = 0.18f;
        [Tooltip("Foot lift away from the contact surface during a step."), Min(0f)]
        [SerializeField] private float stepHeight = 0.25f;
        [Tooltip("Bounded FABRIK iterations per frame. Higher values improve long-chain accuracy."), Range(1, 32)]
        [SerializeField] private int solverIterations = 12;
        [Tooltip("Generated block references. Managed by Rebuild Geometry; do not assign unrelated objects."), HideInInspector]
        [SerializeField] private Transform[] blocks = Array.Empty<Transform>();

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
            totalLength = 0f;
            for (int index = 0; index < count; index++)
            {
                lengths[index] = sections[index] == null ? 0.01f : sections[index].Length;
                totalLength += lengths[index];
            }
            BuildRestPose();
            foot = joints[count];
            plantedSurface = null;
            if (TryFindFooting(out RaycastHit hit)) Plant(hit);
            initialized = true;
            IsStepping = false;
            Solve();
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
            for (int index = 0; index < lengths.Length; index++)
            {
                angle += sections[index] == null ? 0f : sections[index].BendAngle;
                float radians = angle * Mathf.Deg2Rad;
                position += (transform.forward * Mathf.Cos(radians) + transform.up * Mathf.Sin(radians)) * lengths[index];
            }
            return position;
        }

        private bool TryFindFooting(out RaycastHit hit)
        {
            Vector3 rest = RestFoot();
            if (Physics.Raycast(rest + transform.up * probeHeight, -transform.up, out hit,
                probeHeight + probeDepth, surfaceLayers, QueryTriggerInteraction.Ignore)) return true;
            // At corners also look outward, then inward toward the body-facing wall.
            if (Physics.Raycast(transform.position, transform.forward, out hit, totalLength,
                surfaceLayers, QueryTriggerInteraction.Ignore)) return true;
            return Physics.Raycast(rest + transform.forward * probeHeight, -transform.forward, out hit,
                probeHeight + probeDepth, surfaceLayers, QueryTriggerInteraction.Ignore);
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
            if (plantedSurface != null && (hit.point - foot).sqrMagnitude < stepDistance * stepDistance &&
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
            if (IsStepping)
            {
                stepProgress = Mathf.Clamp01(stepProgress + Mathf.Max(0f, deltaTime) / Mathf.Max(0.01f, stepDuration));
                if (plantedSurface != null) stepEnd = plantedSurface.TransformPoint(surfaceLocalPoint);
                float blend = Mathf.SmoothStep(0f, 1f, stepProgress);
                foot = Vector3.Lerp(stepStart, stepEnd, blend) + stepNormal * (Mathf.Sin(stepProgress * Mathf.PI) * stepHeight);
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
                    if ((joints[count] - foot).sqrMagnitude < 0.000001f) break;
                }
            }
            for (int index = 0; index < count && index < blocks.Length; index++)
            {
                Transform block = blocks[index];
                if (block == null) continue;
                Vector3 direction = joints[index + 1] - joints[index];
                block.SetPositionAndRotation((joints[index] + joints[index + 1]) * 0.5f,
                    Quaternion.FromToRotation(Vector3.up, direction));
                Vector3 scale = transform.lossyScale;
                block.localScale = new Vector3(thickness / Mathf.Max(0.0001f, Mathf.Abs(scale.x)),
                    lengths[index] / Mathf.Max(0.0001f, Mathf.Abs(scale.y)), thickness / Mathf.Max(0.0001f, Mathf.Abs(scale.z)));
            }
        }

        private static Vector3 SafeDirection(Vector3 direction, Vector3 fallback)
        {
            return direction.sqrMagnitude > 0.0000001f ? direction.normalized : fallback;
        }

        private void OnDrawGizmosSelected()
        {
            if (!initialized) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(foot, 0.06f);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(RestFoot(), stepDistance);
        }
    }
}
