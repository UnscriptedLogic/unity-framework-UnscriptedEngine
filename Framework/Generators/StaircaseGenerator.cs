using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Framework.Components
{
    /// <summary>
    /// Generates evenly spaced steps along a straight or curved staircase.
    /// The generator's local forward direction is the direction of travel.
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class StaircaseGenerator : MonoBehaviour
    {
        private const string GeneratedRootName = "Generated Steps";

        [Header("Staircase")]
        [SerializeField] private GameObject stepPrefab;
        [Min(1)] [SerializeField] private int stepCount = 10;
        [Min(0.001f)] [SerializeField] private float stepLength = 0.3f;
        [Min(0f)] [SerializeField] private float stepHeight = 0.2f;
        [Tooltip("Total horizontal turn in degrees. Use 0 for a straight staircase and a negative value to turn the other way.")]
        [SerializeField] private float stepTurnArc;

        [Header("Generation")]
        [Tooltip("Generate the staircase when the component's GameObject starts.")]
        [SerializeField] private bool generateOnStart = true;
        [HideInInspector] [SerializeField] private Transform generatedStepsRoot;

#if UNITY_EDITOR
        private bool editorGenerationQueued;
#endif

        private void OnValidate()
        {
            stepCount = Mathf.Max(1, stepCount);
            stepLength = Mathf.Max(0.001f, stepLength);
            stepHeight = Mathf.Max(0f, stepHeight);

            // Hierarchy changes are deferred because Unity does not permit them
            // directly from inside OnValidate.
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                QueueEditorGeneration();
            }
#endif
        }

        private void Start()
        {
            if (generateOnStart)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    QueueEditorGeneration();
                    return;
                }
#endif
                Generate();
            }
        }

#if UNITY_EDITOR
        private void QueueEditorGeneration()
        {
            if (editorGenerationQueued)
            {
                return;
            }

            editorGenerationQueued = true;
            EditorApplication.delayCall += GenerateQueuedEditorPreview;
        }

        private void GenerateQueuedEditorPreview()
        {
            editorGenerationQueued = false;

            if (this == null || Application.isPlaying || !isActiveAndEnabled)
            {
                return;
            }

            if (stepPrefab == null)
            {
                ClearGeneratedSteps();
            }
            else
            {
                Generate();
            }
        }

        private void OnDisable()
        {
            if (editorGenerationQueued)
            {
                EditorApplication.delayCall -= GenerateQueuedEditorPreview;
                editorGenerationQueued = false;
            }
        }
#endif

        /// <summary>
        /// Removes the previously generated steps and creates a new staircase.
        /// Step length is measured along the staircase path, not as a chord on a turn.
        /// </summary>
        [ContextMenu("Generate Staircase")]
        public void Generate()
        {
            if (stepPrefab == null)
            {
                Debug.LogWarning($"{nameof(StaircaseGenerator)} on {name} needs a step prefab.", this);
                return;
            }

            if (stepPrefab == gameObject)
            {
                Debug.LogError("The step prefab cannot be the staircase generator's own GameObject.", this);
                return;
            }

            ClearGeneratedSteps();
            generatedStepsRoot = new GameObject(GeneratedRootName).transform;
            generatedStepsRoot.SetParent(transform, false);

            float turnRadians = stepTurnArc * Mathf.Deg2Rad;
            float pathLength = Mathf.Max(0f, (stepCount - 1) * stepLength);
            float radius = Mathf.Approximately(turnRadians, 0f)
                ? 0f
                : pathLength / turnRadians;

            for (int index = 0; index < stepCount; index++)
            {
                float distance = index * stepLength;
                float angle = pathLength <= 0f
                    ? 0f
                    : turnRadians * distance / pathLength;

                Vector3 localPosition;
                Quaternion localRotation;

                if (Mathf.Approximately(turnRadians, 0f))
                {
                    localPosition = new Vector3(0f, index * stepHeight, distance);
                    localRotation = Quaternion.identity;
                }
                else
                {
                    localPosition = new Vector3(
                        radius * (1f - Mathf.Cos(angle)),
                        index * stepHeight,
                        radius * Mathf.Sin(angle));
                    localRotation = Quaternion.Euler(0f, angle * Mathf.Rad2Deg, 0f);
                }

                GameObject step = Instantiate(stepPrefab, generatedStepsRoot);
                step.name = $"Step {index + 1:00}";
                step.transform.localPosition = localPosition;
                step.transform.localRotation = localRotation * stepPrefab.transform.localRotation;
                step.transform.localScale = stepPrefab.transform.localScale;
            }
        }

        /// <summary>
        /// Removes only the children created by this generator.
        /// </summary>
        [ContextMenu("Clear Generated Steps")]
        public void ClearGeneratedSteps()
        {
            if (generatedStepsRoot == null)
            {
                Transform existingRoot = transform.Find(GeneratedRootName);
                if (existingRoot != null)
                {
                    generatedStepsRoot = existingRoot;
                }
            }

            if (generatedStepsRoot == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(generatedStepsRoot.gameObject);
            }
            else
            {
                DestroyImmediate(generatedStepsRoot.gameObject);
            }

            generatedStepsRoot = null;
        }
    }
}
