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
        [Header("Obstacle Avoidance")]
        [Tooltip("Check block clearance and swivel the leg around its planted foot to avoid nearby walls. Uses Surface Layers; exclude the body layer.")]
        [SerializeField]
        private bool avoidObstacles = true;
        [Tooltip("Extra clearance around block sides at unit scale. Contact at the very tip of the final block is allowed.")]
        [Min(0f)]
        [SerializeField]
        private float wallClearance = 0.025f;
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
        private Vector3[] avoidancePose;
        private readonly Collider[] obstacleBuffer = new Collider[16];
        private Vector3 contactLocalNormal;
        private float avoidanceAngle;
        private bool obstructed;

        // Positive uniform scale is supported, including inherited scale from parent objects.
        private float WorldScale => Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.y));

        public bool IsStepping { get; private set; }
        public Vector3 FootPosition => foot;
        public int SectionCount => sections == null ? 0 : sections.Length;
        public float Reach => totalLength;
        public bool IsObstructed => obstructed;
        private bool strikeActive;
        private Vector3 strikeStart;
        private Vector3 strikeTarget;
        private Vector3 strikeUp;
        private float strikeLift;
        private float strikeWindup;
        private float strikeSlam;
        private float strikeRecovery;
        private float strikeTime;

        /// <summary>Overrides walking with a lifted windup, fast stab and return to footing.</summary>
        public void BeginStrike(Vector3 target, Vector3 up, float lift, float windup, float slam, float recovery)
        {
            if (!initialized)
            {
                if (blocks.Length != SectionCount) RebuildGeometry();
                else Initialize();
            }
            strikeStart = foot;
            strikeTarget = target;
            strikeUp = up.normalized;
            strikeLift = Mathf.Max(0f, lift);
            strikeWindup = Mathf.Max(0.01f, windup);
            strikeSlam = Mathf.Max(0.01f, slam);
            strikeRecovery = Mathf.Max(0.01f, recovery);
            strikeTime = 0f;
            strikeActive = true;
            IsStepping = false;
            plantedSurface = null;
        }

        public void CancelStrike() { strikeActive = false; initialized = false; IsStepping = false; }

        private void OnEnable() { initialized = false; IsStepping = false; strikeActive = false; }
        private void OnDisable() { initialized = false; IsStepping = false; strikeActive = false; plantedSurface = null; }

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
            avoidancePose = new Vector3[count + 1];
            avoidanceAngle = 0f;
            obstructed = false;
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
                distance, surfaceLayers, QueryTriggerInteraction.Ignore))
            {
                // A floor hit behind a wall is not a usable foot placement. Prefer the blocking wall.
                Vector3 towardFoot = hit.point - transform.position;
                float footDistance = towardFoot.magnitude;
                if (avoidObstacles && footDistance > 0f &&
                    Physics.Raycast(transform.position, towardFoot / footDistance, out RaycastHit wallHit,
                        footDistance, surfaceLayers, QueryTriggerInteraction.Ignore) &&
                    wallHit.distance < footDistance - (thickness + wallClearance) * scale)
                    hit = wallHit;
                return true;
            }
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
            contactLocalNormal = plantedSurface.InverseTransformDirection(hit.normal);
        }

        public bool TryBeginStep()
        {
            if (!initialized || strikeActive || IsStepping || lengths.Length == 0 || !TryFindFooting(out RaycastHit hit)) return false;
            float distance = stepDistance * WorldScale;
            float movementSquared = (hit.point - foot).sqrMagnitude;
            bool needsClearanceStep = obstructed && movementSquared > 0.000001f * WorldScale * WorldScale;
            if (!needsClearanceStep && plantedSurface != null && movementSquared < distance * distance &&
                (transform.position - foot).sqrMagnitude < totalLength * totalLength * 0.95f) return false;
            stepStart = foot;
            stepEnd = hit.point;
            stepNormal = hit.normal;
            plantedSurface = hit.transform;
            surfaceLocalPoint = plantedSurface.InverseTransformPoint(stepEnd);
            contactLocalNormal = plantedSurface.InverseTransformDirection(hit.normal);
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
            if (strikeActive)
            {
                strikeTime += Mathf.Max(0f, deltaTime);
                Vector3 lifted = strikeStart + strikeUp * strikeLift;
                if (strikeTime < strikeWindup)
                    foot = Vector3.Lerp(strikeStart, lifted, Mathf.SmoothStep(0f, 1f, strikeTime / strikeWindup));
                else if (strikeTime < strikeWindup + strikeSlam)
                    foot = Vector3.Lerp(lifted, strikeTarget, Mathf.Pow((strikeTime - strikeWindup) / strikeSlam, 2f));
                else
                {
                    float recovery = Mathf.Clamp01((strikeTime - strikeWindup - strikeSlam) / strikeRecovery);
                    foot = Vector3.Lerp(strikeTarget, RestFoot(), Mathf.SmoothStep(0f, 1f, recovery));
                    if (recovery >= 1f)
                    {
                        strikeActive = false;
                        if (TryFindFooting(out RaycastHit footing)) Plant(footing);
                    }
                }
                Solve();
                return;
            }
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
            AvoidObstacles(worldScale);
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

        private void AvoidObstacles(float scale)
        {
            obstructed = false;
            if (!avoidObstacles || lengths.Length == 0) return;
            if (HasBlockedSection(scale) == false)
            {
                avoidanceAngle = 0f;
                return;
            }

            // Rotating the whole solved chain about its root-tip axis preserves every section length
            // and both endpoint positions. No iterative physics queries inside the FABRIK solver.
            Array.Copy(joints, avoidancePose, joints.Length);
            Vector3 axis = joints[joints.Length - 1] - joints[0];
            if (axis.sqrMagnitude > 0.00000001f * scale * scale && lengths.Length > 1)
            {
                axis.Normalize();
                if (avoidanceAngle != 0f && TryAvoidanceAngle(axis, avoidanceAngle, scale)) return;
                // Bounded search, smallest change first. Normal clear legs only pay one overlap per block.
                for (int index = 1; index <= 6; index++)
                {
                    float angle = index * 30f;
                    if (TryAvoidanceAngle(axis, angle, scale)) return;
                    if (index < 6 && TryAvoidanceAngle(axis, -angle, scale)) return;
                }
            }
            Array.Copy(avoidancePose, joints, joints.Length);
            avoidanceAngle = 0f;
            // No valid posture: let the brain schedule a new footing. Never shorten rigid blocks.
            obstructed = true;
        }

        private bool TryAvoidanceAngle(Vector3 axis, float angle, float scale)
        {
            Quaternion rotation = Quaternion.AngleAxis(angle, axis);
            for (int index = 1; index < joints.Length - 1; index++)
                joints[index] = avoidancePose[0] + rotation * (avoidancePose[index] - avoidancePose[0]);
            if (HasBlockedSection(scale)) return false;
            avoidanceAngle = angle;
            return true;
        }

        private bool HasBlockedSection(float scale)
        {
            float halfWidth = (thickness * 0.5f + wallClearance) * scale;
            Vector3 normal = plantedSurface == null ? Vector3.zero : plantedSurface.TransformDirection(contactLocalNormal).normalized;
            for (int index = 0; index < lengths.Length; index++)
            {
                Vector3 start = joints[index];
                Vector3 end = joints[index + 1];
                Vector3 delta = end - start;
                float length = delta.magnitude;
                if (length <= 0.00001f * scale) continue;
                Vector3 direction = delta / length;

                // The tip is intentionally planted on a collider. Trim only the contact wedge from
                // the last query, based on approach angle, so ordinary toe contact is not an obstacle.
                if (index == lengths.Length - 1 && !IsStepping && plantedSurface != null)
                {
                    float approach = Vector3.Dot(-direction, normal);
                    if (approach > 0.01f)
                    {
                        float trim = Mathf.Min(length * 0.25f, halfWidth * 1.5f / approach);
                        end -= direction * trim;
                        length -= trim;
                    }
                }
                if (length <= 0.00001f * scale) continue;
                Quaternion rotation = Quaternion.FromToRotation(Vector3.up, direction);
                int count = Physics.OverlapBoxNonAlloc((start + end) * 0.5f,
                    new Vector3(halfWidth, length * 0.5f, halfWidth), obstacleBuffer,
                    rotation, surfaceLayers, QueryTriggerInteraction.Ignore);
                // A saturated buffer is conservatively treated as blocked.
                if (count == obstacleBuffer.Length) return true;
                for (int obstacle = 0; obstacle < count; obstacle++)
                {
                    Collider candidate = obstacleBuffer[obstacle];
                    if (candidate != null && !candidate.transform.IsChildOf(transform)) return true;
                }
            }
            return false;
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
                if (avoidObstacles)
                {
                    Gizmos.color = obstructed ? Color.red : Color.green;
                    for (int index = 0; index < lengths.Length; index++)
                    {
                        Vector3 delta = joints[index + 1] - joints[index];
                        Matrix4x4 previousMatrix = Gizmos.matrix;
                        Gizmos.matrix = Matrix4x4.TRS((joints[index] + joints[index + 1]) * 0.5f,
                            Quaternion.FromToRotation(Vector3.up, delta), Vector3.one);
                        float width = (thickness + wallClearance * 2f) * scale;
                        Gizmos.DrawWireCube(Vector3.zero, new Vector3(width, delta.magnitude, width));
                        Gizmos.matrix = previousMatrix;
                    }
                    Label(joints[0], obstructed ? "No clear leg posture" : "Block clearance (includes Wall Clearance)");
                }
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
