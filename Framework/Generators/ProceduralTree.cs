using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Framework.Components
{
    /// <summary>
    /// Builds a tapered trunk and recursive branch levels from cube primitives.
    /// Dimensions are in local units; local Y is the initial growth direction.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class ProceduralTree : MonoBehaviour
    {
        private const int MaximumBlockCount = 10000;

        [Header("Appearance")]
        [Tooltip("Shared material for every trunk block. Leave empty to use Unity's default primitive material.")]
        [SerializeField] private Material trunkMaterial;
        [Tooltip("Unity layer index applied to generated trunk blocks and their container (0–31).")]
        [Range(0, 31)]
        [SerializeField] private int trunkLayer;

        [Header("Dimensions")]
        [Tooltip("Total length along the trunk in local units. Curving or slumping reduces its vertical reach.")]
        [Min(0.001f)]
        [SerializeField] private float height = 10f;
        [Tooltip("Random square width per block before taper, in local units. X is minimum, Y is maximum. Crossing an endpoint moves the other endpoint to match.")]
        [SerializeField] private Vector2 width = new Vector2(2f, 2f);
        [Tooltip("Fraction of base width lost at the tip. Zero keeps constant width; one tapers to a very thin tip.")]
        [Range(0f, 1f)]
        [SerializeField] private float sizeDropOff = 0.8f;
        [Tooltip("Random length per block in local units. X is minimum, Y is maximum. Crossing an endpoint moves the other endpoint to match. The final block is shortened to fit the height.")]
        [SerializeField] private Vector2 blockLength = new Vector2(1f, 1f);

        [Header("Shape")]
        [Tooltip("Strength of irregular sideways bends in degrees. Zero produces a straight trunk before slump.")]
        [Range(0f, 120f)]
        [SerializeField] private float curve = 20f;
        [Tooltip("Number of bend cycles along the trunk. Higher values create more frequent changes of direction.")]
        [Min(0f)]
        [SerializeField] private float curveFrequency = 1.5f;
        [Tooltip("Seed for repeatable curves, widths, and block lengths without affecting Unity's random state.")]
        [SerializeField] private int seed = 1;
        [Tooltip("Progressively bends the trunk toward world-down. Zero adds no slump; one points the tip downward.")]
        [Range(0f, 1f)]
        [SerializeField] private float slump;

        [Header("Branches")]
        [Tooltip("Number of branches per local unit along the trunk and parent branch paths. Zero disables branches. First child starts one spacing interval from its parent's base.")]
        [Min(0f)]
        [SerializeField] private float branchesPerTrunkDistance = 0.5f;
        [Tooltip("Total length of each branch along its path, in local units.")]
        [Min(0.001f)]
        [SerializeField] private float branchLength = 4f;
        [Tooltip("Strength of irregular branch bends in degrees. Zero keeps branches straight before slump.")]
        [Range(0f, 120f)]
        [SerializeField] private float branchCurve = 20f;
        [Tooltip("Number of bend cycles along each branch.")]
        [Min(0f)]
        [SerializeField] private float branchCurveFrequency = 1.5f;
        [Tooltip("Progressively bends branches toward world-up for negative values or world-down for positive values. Zero adds no slump; -1 and 1 give the strongest upward and downward bends.")]
        [Range(-1f, 1f)]
        [SerializeField] private float branchSlump = 0.25f;
        [Tooltip("Random branch block length in local units: X minimum, Y maximum. Crossing an endpoint moves the other to match. Final blocks are shortened to fit branch length.")]
        [SerializeField] private Vector2 branchBlockLength = new Vector2(0.5f, 1f);
        [Tooltip("Random square cross-section scale per branch block, relative to trunk width at attachment, before branch taper. X minimum, Y maximum. Crossing endpoints makes the other match.")]
        [SerializeField] private Vector2 branchBlockScale = new Vector2(0.3f, 0.5f);
        [Tooltip("Fraction of branch width lost toward its tip. Zero keeps untapered widths; one tapers to a very thin tip.")]
        [Range(0f, 1f)]
        [SerializeField] private float branchSizeDropOff = 0.85f;
        [Tooltip("Percentage chance for each branch to fork halfway along its length into two tips. Zero disables splitting; 100 splits every branch. Each root-to-tip path keeps the configured branch length.")]
        [Range(0f, 100f)]
        [SerializeField] private float branchSplitChance;
        [Tooltip("Angle in degrees that each fork tip turns away from its parent direction before curve and slump. The total fork opening is twice this value.")]
        [Range(0f, 90f)]
        [SerializeField] private float branchSplitAngle = 25f;
        [Tooltip("Number of branch generations. Zero generates only the trunk; one adds trunk branches; two lets those branches sprout children. Fork tips remain at their parent's level.")]
        [Range(0, 8)]
        [SerializeField] private int branchLevels = 1;
        [Tooltip("Length multiplier for each branch generation after the first. Width scales from the actual parent block using Branch Block Scale.")]
        [Range(0.1f, 1f)]
        [SerializeField] private float branchLevelLengthScale = 0.65f;

        [Header("Leaves")]
        [Tooltip("Shared material for leaf blocks. Leave empty to use Unity's default primitive material. Leaves have no colliders and use the trunk layer.")]
        [SerializeField] private Material leafMaterial;
        [Tooltip("Number of leaf blocks clustered at each branch end, including both fork tips at every level. Zero disables leaves.")]
        [Range(0, 32)]
        [SerializeField] private int leafBlocksPerTip = 3;
        [Tooltip("Random leaf block width in local units. X is minimum, Y is maximum. Crossing either endpoint moves the other to match.")]
        [SerializeField] private Vector2 leafBlockWidth = new Vector2(1f, 2f);
        [Tooltip("Random leaf block height in local units. X is minimum, Y is maximum. Crossing either endpoint moves the other to match.")]
        [SerializeField] private Vector2 leafBlockHeight = new Vector2(0.5f, 1f);
        [Tooltip("Random leaf block depth in local units. X is minimum, Y is maximum. Crossing either endpoint moves the other to match.")]
        [SerializeField] private Vector2 leafBlockDepth = new Vector2(1f, 2f);

        [Header("Generation")]
        [Tooltip("Rebuild automatically when Inspector values change, including during play.")]
        [SerializeField] private bool autoUpdate = true;
        [Tooltip("Generate BoxColliders on the blocks, suitable for a climbable or walkable world tree.")]
        [SerializeField] private bool generateColliders = true;
        [SerializeField, HideInInspector] private Transform generatedRoot;
        [SerializeField, HideInInspector] private Vector2 previousWidth = new Vector2(2f, 2f);
        [SerializeField, HideInInspector] private Vector2 previousBlockLength = new Vector2(1f, 1f);
        [SerializeField, HideInInspector] private Vector2 previousBranchBlockLength = new Vector2(0.5f, 1f);
        [SerializeField, HideInInspector] private Vector2 previousBranchBlockScale = new Vector2(0.3f, 0.5f);
        [SerializeField, HideInInspector] private Vector2 previousLeafBlockWidth = new Vector2(1f, 2f);
        [SerializeField, HideInInspector] private Vector2 previousLeafBlockHeight = new Vector2(0.5f, 1f);
        [SerializeField, HideInInspector] private Vector2 previousLeafBlockDepth = new Vector2(1f, 2f);

        private bool rebuildRequested;

        private void OnEnable()
        {
            rebuildRequested = true;
        }

        private void OnValidate()
        {
            height = Mathf.Max(0.001f, height);
            ValidateRange(ref width, ref previousWidth);
            ValidateRange(ref blockLength, ref previousBlockLength);
            sizeDropOff = Mathf.Clamp01(sizeDropOff);
            curve = Mathf.Clamp(curve, 0f, 120f);
            curveFrequency = Mathf.Max(0f, curveFrequency);
            slump = Mathf.Clamp01(slump);
            trunkLayer = Mathf.Clamp(trunkLayer, 0, 31);
            ValidateBranches();
            ValidateLeaves();
            // Update performs hierarchy changes outside Unity's validation callback.
            rebuildRequested = autoUpdate;
        }

        private void Update()
        {
            if (rebuildRequested)
            {
                Generate();
            }
        }

        private static void ValidateRange(ref Vector2 range, ref Vector2 previousRange)
        {
            range.x = Mathf.Max(0.001f, range.x);
            range.y = Mathf.Max(0.001f, range.y);
            if (range.x > range.y)
            {
                if (range.x != previousRange.x)
                {
                    range.y = range.x;
                }
                else
                {
                    range.x = range.y;
                }
            }

            previousRange = range;
        }

        private void ValidateBranches()
        {
            branchesPerTrunkDistance = Mathf.Max(0f, branchesPerTrunkDistance);
            branchLength = Mathf.Max(0.001f, branchLength);
            branchCurve = Mathf.Clamp(branchCurve, 0f, 120f);
            branchCurveFrequency = Mathf.Max(0f, branchCurveFrequency);
            branchSlump = Mathf.Clamp(branchSlump, -1f, 1f);
            branchSizeDropOff = Mathf.Clamp01(branchSizeDropOff);
            branchSplitChance = Mathf.Clamp(branchSplitChance, 0f, 100f);
            branchSplitAngle = Mathf.Clamp(branchSplitAngle, 0f, 90f);
            branchLevels = Mathf.Clamp(branchLevels, 0, 8);
            branchLevelLengthScale = Mathf.Clamp(branchLevelLengthScale, 0.1f, 1f);
            ValidateRange(ref branchBlockLength, ref previousBranchBlockLength);
            ValidateRange(ref branchBlockScale, ref previousBranchBlockScale);
        }

        private void ValidateLeaves()
        {
            leafBlocksPerTip = Mathf.Clamp(leafBlocksPerTip, 0, 32);
            ValidateRange(ref leafBlockWidth, ref previousLeafBlockWidth);
            ValidateRange(ref leafBlockHeight, ref previousLeafBlockHeight);
            ValidateRange(ref leafBlockDepth, ref previousLeafBlockDepth);
        }

        [ContextMenu("Generate Tree")]
        public void Generate()
        {
            rebuildRequested = false;
#if UNITY_EDITOR
            if (EditorUtility.IsPersistent(this))
            {
                return;
            }
#endif
            ValidateRange(ref width, ref previousWidth);
            ValidateRange(ref blockLength, ref previousBlockLength);
            ValidateBranches();
            ValidateLeaves();
            float safeHeight = Mathf.Max(0.001f, height);
            float requestedCount = Mathf.Ceil(safeHeight / blockLength.x);
            double branchCountEstimate = branchLevels == 0 ? 0d :
                System.Math.Floor((double)safeHeight * branchesPerTrunkDistance);
            double blocksPerBranch = branchLevels == 0 ? 0d : EstimateBranchBlocks(branchLength, 1);
            double totalBlockEstimate = requestedCount + branchCountEstimate * blocksPerBranch;
            if (double.IsNaN(totalBlockEstimate) || double.IsInfinity(totalBlockEstimate) ||
                totalBlockEstimate > MaximumBlockCount)
            {
                Debug.LogWarning("Tree could exceed 10,000 blocks. Increase minimum block lengths or reduce height, branch length, density, levels, or leaves per tip.", this);
                return;
            }

            ClearGeneratedTrunk();
            generatedRoot = new GameObject("Generated Trunk").transform;
            generatedRoot.SetParent(transform, false);
            generatedRoot.gameObject.layer = Mathf.Clamp(trunkLayer, 0, 31);

            int blockCount = Mathf.Max(1, (int)requestedCount);
            System.Random random = new System.Random(seed);
            // Separate streams keep trunk geometry stable when branch settings change.
            System.Random branchRandom = new System.Random(unchecked(seed ^ 0x59A73B21));
            float branchAzimuth = (float)branchRandom.NextDouble() * 360f;
            int branchCount = (int)branchCountEstimate;
            int branchIndex = 0;
            float phaseX = (float)random.NextDouble() * Mathf.PI * 2f;
            float phaseZ = (float)random.NextDouble() * Mathf.PI * 2f;
            Vector3 position = Vector3.zero;
            Vector3 localDown = transform.InverseTransformDirection(Vector3.down).normalized;
            float distance = 0f;

            for (int index = 0; index < blockCount && distance < safeHeight; index++)
            {
                float sampledLength = Mathf.Lerp(blockLength.x, blockLength.y, (float)random.NextDouble());
                float length = Mathf.Min(sampledLength, safeHeight - distance);
                float progress = (distance + length * 0.5f) / safeHeight;
                float angle = progress * Mathf.Max(0f, curveFrequency) * Mathf.PI * 2f;
                float bendX = EvaluateBend(angle, phaseX) * curve;
                float bendZ = EvaluateBend(angle, phaseZ) * curve;
                Vector3 direction = Quaternion.Euler(bendX, 0f, bendZ) * Vector3.up;
                float slumpAngle = Vector3.Angle(direction, localDown) * Mathf.Deg2Rad;
                direction = Vector3.RotateTowards(direction, localDown,
                    slumpAngle * Mathf.Clamp01(slump) * progress * progress, 0f).normalized;

                float blockWidth = Mathf.Lerp(width.x, width.y, (float)random.NextDouble()) *
                    Mathf.Max(0.01f, 1f - Mathf.Clamp01(sizeDropOff) * progress);
                CreateBlock(generatedRoot, $"Trunk Block {index + 1:000}",
                    position, direction, length, blockWidth);

                while (branchIndex < branchCount)
                {
                    float attachmentDistance = (float)((branchIndex + 1d) / branchesPerTrunkDistance);
                    if (attachmentDistance > distance + length)
                    {
                        break;
                    }

                    Vector3 attachment = position + direction *
                        Mathf.Clamp(attachmentDistance - distance, 0f, length);
                    Quaternion trunkRotation = Quaternion.FromToRotation(Vector3.up, direction);
                    Vector3 radialDirection = trunkRotation *
                        (Quaternion.AngleAxis(branchAzimuth + branchIndex * 137.50776f, Vector3.up) * Vector3.right);
                    GenerateBranch(generatedRoot, branchIndex, attachment, radialDirection,
                        localDown, blockWidth, branchRandom, branchLength, 1);
                    branchIndex++;
                }

                position += direction * length;
                distance += length;
            }
        }

        private double EstimateBranchBlocks(float totalLength, int level)
        {
            bool canSplit = branchSplitChance > 0f;
            double ownBlocks = canSplit
                ? 3d * System.Math.Ceiling((double)(totalLength * 0.5f) / branchBlockLength.x)
                : System.Math.Ceiling((double)totalLength / branchBlockLength.x);
            ownBlocks += leafBlocksPerTip * (canSplit ? 2d : 1d);
            if (level >= branchLevels)
            {
                return ownBlocks;
            }

            // Both fork tips can sprout children. This deliberately overestimates
            // the shared stem so the complete hierarchy fits before replacing it.
            double childCount = System.Math.Floor((double)totalLength * branchesPerTrunkDistance) *
                (canSplit ? 2d : 1d);
            return ownBlocks + childCount *
                EstimateBranchBlocks(totalLength * branchLevelLengthScale, level + 1);
        }

        private void GenerateBranch(Transform parent, int branchIndex, Vector3 attachment, Vector3 initialDirection,
            Vector3 localDown, float attachmentWidth, System.Random random, float totalLength, int level)
        {
            // Give each branch its own stream so forks do not change later branches.
            int branchSeed = random.Next();
            System.Random geometryRandom = new System.Random(branchSeed);
            System.Random splitRandom = new System.Random(unchecked(branchSeed ^ 0x2C91A531));
            bool split = splitRandom.NextDouble() * 100d < branchSplitChance;
            Transform branchRoot = new GameObject($"Branch {branchIndex + 1:000}").transform;
            branchRoot.SetParent(parent, false);
            branchRoot.gameObject.layer = generatedRoot.gameObject.layer;
            Quaternion growthRotation = Quaternion.FromToRotation(Vector3.up, initialDirection);
            float phaseX = (float)geometryRandom.NextDouble() * Mathf.PI * 2f;
            float phaseZ = (float)geometryRandom.NextDouble() * Mathf.PI * 2f;
            float stemLength = split ? totalLength * 0.5f : totalLength;
            Vector3 junction;
            Vector3 junctionDirection;
            GenerateBranchSection(branchRoot, attachment, growthRotation, localDown,
                attachmentWidth, geometryRandom, phaseX, phaseZ, 0f, stemLength, totalLength,
                level, unchecked(branchSeed ^ 0x3A715E12),
                out junction, out junctionDirection);
            if (!split)
            {
                GenerateLeaves(branchRoot, junction, junctionDirection, totalLength,
                    unchecked(branchSeed ^ 0x173F9A21));
                return;
            }

            Vector3 forkAxis = Vector3.ProjectOnPlane(-localDown, junctionDirection).normalized;
            if (forkAxis.sqrMagnitude < 0.001f)
            {
                forkAxis = Vector3.Cross(junctionDirection, Vector3.right).normalized;
                if (forkAxis.sqrMagnitude < 0.001f)
                {
                    forkAxis = Vector3.Cross(junctionDirection, Vector3.forward).normalized;
                }
            }

            for (int tipIndex = 0; tipIndex < 2; tipIndex++)
            {
                Transform tipRoot = new GameObject($"Fork Tip {tipIndex + 1}").transform;
                tipRoot.SetParent(branchRoot, false);
                tipRoot.gameObject.layer = branchRoot.gameObject.layer;
                float forkAngle = tipIndex == 0 ? -branchSplitAngle : branchSplitAngle;
                Quaternion tipRotation = Quaternion.AngleAxis(forkAngle, forkAxis) * growthRotation;
                Vector3 tipEnd;
                Vector3 tipDirection;
                GenerateBranchSection(tipRoot, junction, tipRotation, localDown,
                    attachmentWidth, geometryRandom, phaseX, phaseZ, stemLength, totalLength, totalLength,
                    level, unchecked(branchSeed ^ ((tipIndex + 1) * 7919)),
                    out tipEnd, out tipDirection);
                GenerateLeaves(tipRoot, tipEnd, tipDirection, totalLength,
                    unchecked(branchSeed ^ ((tipIndex + 1) * 104729) ^ 0x173F9A21));
            }
        }

        private void GenerateBranchSection(Transform parent, Vector3 start, Quaternion growthRotation,
            Vector3 localDown, float attachmentWidth, System.Random random, float phaseX, float phaseZ,
            float startDistance, float endDistance, float totalLength, int level, int childSeed,
            out Vector3 end, out Vector3 endDirection)
        {
            Vector3 position = start;
            float distance = startDistance;
            endDirection = growthRotation * Vector3.up;
            int blockCount = Mathf.CeilToInt((endDistance - startDistance) / branchBlockLength.x);
            System.Random childRandom = new System.Random(childSeed);
            float childAzimuth = (float)childRandom.NextDouble() * 360f;
            int childIndex = (int)System.Math.Floor((double)startDistance * branchesPerTrunkDistance);
            int childLimit = level < branchLevels
                ? (int)System.Math.Floor((double)endDistance * branchesPerTrunkDistance)
                : childIndex;

            for (int index = 0; index < blockCount && distance < endDistance; index++)
            {
                float sampledLength = Mathf.Lerp(branchBlockLength.x, branchBlockLength.y, (float)random.NextDouble());
                float length = Mathf.Min(sampledLength, endDistance - distance);
                float progress = (distance + length * 0.5f) / totalLength;
                float angle = progress * branchCurveFrequency * Mathf.PI * 2f;
                Vector3 direction = growthRotation * (Quaternion.Euler(
                    EvaluateBend(angle, phaseX) * branchCurve, 0f,
                    EvaluateBend(angle, phaseZ) * branchCurve) * Vector3.up);
                Vector3 slumpDirection = branchSlump < 0f ? -localDown : localDown;
                float slumpAngle = Vector3.Angle(direction, slumpDirection) * Mathf.Deg2Rad;
                direction = Vector3.RotateTowards(direction, slumpDirection,
                    slumpAngle * Mathf.Abs(branchSlump) * progress * progress, 0f).normalized;
                float scale = Mathf.Lerp(branchBlockScale.x, branchBlockScale.y, (float)random.NextDouble());
                float blockWidth = attachmentWidth * scale *
                    Mathf.Max(0.01f, 1f - branchSizeDropOff * progress);
                CreateBlock(parent, $"Branch Block {index + 1:000}",
                    position, direction, length, blockWidth);
                while (childIndex < childLimit)
                {
                    float attachmentDistance = (float)((childIndex + 1d) / branchesPerTrunkDistance);
                    if (attachmentDistance > distance + length)
                    {
                        break;
                    }

                    Vector3 attachment = position + direction *
                        Mathf.Clamp(attachmentDistance - distance, 0f, length);
                    Quaternion parentRotation = Quaternion.FromToRotation(Vector3.up, direction);
                    Vector3 childDirection = parentRotation *
                        (Quaternion.AngleAxis(childAzimuth + childIndex * 137.50776f, Vector3.up) * Vector3.right);
                    GenerateBranch(parent, childIndex, attachment, childDirection, localDown,
                        blockWidth, childRandom, totalLength * branchLevelLengthScale, level + 1);
                    childIndex++;
                }
                position += direction * length;
                distance += length;
                endDirection = direction;
            }

            end = position;
        }

        private void GenerateLeaves(Transform parent, Vector3 tip, Vector3 direction,
            float totalLength, int leafSeed)
        {
            // Independent stream keeps all woody geometry unchanged by leaf settings.
            System.Random random = new System.Random(leafSeed);
            Quaternion tipRotation = Quaternion.FromToRotation(Vector3.up, direction);
            for (int index = 0; index < leafBlocksPerTip; index++)
            {
                Vector3 size = new Vector3(
                    Mathf.Lerp(leafBlockWidth.x, leafBlockWidth.y, (float)random.NextDouble()),
                    Mathf.Lerp(leafBlockHeight.x, leafBlockHeight.y, (float)random.NextDouble()),
                    Mathf.Lerp(leafBlockDepth.x, leafBlockDepth.y, (float)random.NextDouble()));
                // One block covers the endpoint. Others overlap it in a small
                // cluster restricted to the last portion of the branch.
                Vector3 offset = Vector3.zero;
                if (index > 0)
                {
                    float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                    float radius = Mathf.Min(size.x, size.z) * 0.35f;
                    float retreat = Mathf.Min(totalLength * 0.2f, size.y * 0.35f) *
                        (float)random.NextDouble();
                    offset = tipRotation * new Vector3(
                        Mathf.Cos(angle) * radius, -retreat, Mathf.Sin(angle) * radius);
                }

                GameObject leaf = GameObject.CreatePrimitive(PrimitiveType.Cube);
                leaf.name = $"Leaf Block {index + 1:00}";
                leaf.layer = generatedRoot.gameObject.layer;
                leaf.transform.SetParent(parent, false);
                leaf.transform.localPosition = tip + offset;
                leaf.transform.localRotation = tipRotation *
                    Quaternion.AngleAxis((float)random.NextDouble() * 360f, Vector3.up);
                leaf.transform.localScale = size;
                if (leafMaterial != null)
                {
                    MeshRenderer renderer = leaf.GetComponent<MeshRenderer>();
                    renderer.sharedMaterial = leafMaterial;
                }

                Collider leafCollider = leaf.GetComponent<Collider>();
                leafCollider.enabled = false;
                DestroyGeneratedObject(leafCollider);
            }
        }

        private void CreateBlock(Transform parent, string blockName, Vector3 start,
            Vector3 direction, float length, float blockWidth)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = blockName;
            block.layer = generatedRoot.gameObject.layer;
            block.transform.SetParent(parent, false);
            block.transform.localPosition = start + direction * (length * 0.5f);
            block.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction);
            block.transform.localScale = new Vector3(blockWidth, length, blockWidth);
            if (trunkMaterial != null)
            {
                MeshRenderer renderer = block.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = trunkMaterial;
            }

            if (!generateColliders)
            {
                Collider blockCollider = block.GetComponent<Collider>();
                blockCollider.enabled = false;
                DestroyGeneratedObject(blockCollider);
            }
        }

        private static float EvaluateBend(float angle, float phase)
        {
            // Subtract the initial sample so every seed starts upright.
            return (Mathf.Sin(angle + phase) - Mathf.Sin(phase) +
                0.35f * (Mathf.Sin(angle * 2.3f + phase) - Mathf.Sin(phase))) / 2.7f;
        }

        [ContextMenu("Clear Generated Trunk")]
        public void ClearGeneratedTrunk()
        {
            rebuildRequested = false;
            // Only delete the stored, owned container; never search by name.
            if (generatedRoot == null || generatedRoot.parent != transform)
            {
                generatedRoot = null;
                return;
            }

            generatedRoot.gameObject.SetActive(false);
            DestroyGeneratedObject(generatedRoot.gameObject);
            generatedRoot = null;
        }

        private static void DestroyGeneratedObject(Object target)
        {
            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
