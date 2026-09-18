using UnityEngine;

[ExecuteAlways]
public class CircularBoundary : MonoBehaviour
{
    private const string GeneratedRootName = "__Generated Circular Boundary";

    private enum RotationMode
    {
        None,
        FaceOutward,
        Tangent
    }

    private enum BoundaryPlane
    {
        XY,
        XZ
    }

    [SerializeField] private GameObject prefab;
    [SerializeField, Min(0.1f)] private float radius = 8f;
    [SerializeField, Min(3)] private int prefabCount = 48;
    [SerializeField] private BoundaryPlane boundaryPlane = BoundaryPlane.XY;
    [SerializeField] private RotationMode rotationMode = RotationMode.Tangent;
    [SerializeField] private float rotationOffset;
    [SerializeField] private Vector2 randomRotationRange = Vector2.zero;
    [SerializeField] private Vector3 baseScale = Vector3.one;
    [SerializeField] private Vector2 randomUniformScaleRange = Vector2.one;
    [SerializeField] private Vector3 randomScaleRange = Vector3.zero;
    [SerializeField] private Vector2 randomRadiusOffsetRange = Vector2.zero;
    [SerializeField] private int randomSeed = 12345;
    [SerializeField] private bool regenerateInEditor = true;

#if UNITY_EDITOR
    private bool queuedRegeneration;

    private void OnValidate()
    {
        if (!regenerateInEditor || Application.isPlaying)
        {
            return;
        }

        QueueRegenerate();
    }

    private void OnEnable()
    {
        if (!regenerateInEditor || Application.isPlaying)
        {
            return;
        }

        QueueRegenerate();
    }

    [ContextMenu("Regenerate Boundary")]
    private void RegenerateBoundary()
    {
        queuedRegeneration = false;
        ClearGeneratedBoundary();

        if (prefab == null || prefabCount < 3 || radius <= 0f)
        {
            return;
        }

        Transform root = CreateGeneratedRoot();
        Random.InitState(randomSeed);

        for (int i = 0; i < prefabCount; i++)
        {
            float normalizedIndex = (float)i / prefabCount;
            float angle = normalizedIndex * Mathf.PI * 2f;
            float angleDegrees = angle * Mathf.Rad2Deg;
            float currentRadius = Mathf.Max(0.1f, radius + RandomRange(randomRadiusOffsetRange));

            GameObject instance = CreatePrefabInstance(root);
            instance.name = $"{prefab.name}_{i:00}";
            instance.transform.localPosition = GetPosition(angle, currentRadius);
            instance.transform.localRotation = GetRotation(angleDegrees);
            instance.transform.localScale = GetScale();
        }
    }

    private void QueueRegenerate()
    {
        if (queuedRegeneration)
        {
            return;
        }

        queuedRegeneration = true;
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null)
            {
                return;
            }

            RegenerateBoundary();
        };
    }

    private Transform CreateGeneratedRoot()
    {
        GameObject root = new GameObject(GeneratedRootName);
        UnityEditor.Undo.RegisterCreatedObjectUndo(root, "Create Circular Boundary");

        Transform rootTransform = root.transform;
        rootTransform.SetParent(transform, false);
        rootTransform.localPosition = Vector3.zero;
        rootTransform.localRotation = Quaternion.identity;
        rootTransform.localScale = Vector3.one;
        return rootTransform;
    }

    private GameObject CreatePrefabInstance(Transform root)
    {
        GameObject instance = UnityEditor.PrefabUtility.InstantiatePrefab(prefab, root) as GameObject;

        if (instance == null)
        {
            instance = Instantiate(prefab, root);
        }

        UnityEditor.Undo.RegisterCreatedObjectUndo(instance, "Create Circular Boundary Piece");
        return instance;
    }

    private void ClearGeneratedBoundary()
    {
        Transform existingRoot = transform.Find(GeneratedRootName);

        if (existingRoot == null)
        {
            return;
        }

        UnityEditor.Undo.DestroyObjectImmediate(existingRoot.gameObject);
    }

    private Vector3 GetPosition(float angle, float currentRadius)
    {
        float x = Mathf.Cos(angle) * currentRadius;
        float y = Mathf.Sin(angle) * currentRadius;

        if (boundaryPlane == BoundaryPlane.XZ)
        {
            return new Vector3(x, 0f, y);
        }

        return new Vector3(x, y, 0f);
    }

    private Quaternion GetRotation(float angleDegrees)
    {
        float alignedRotation = 0f;

        switch (rotationMode)
        {
            case RotationMode.FaceOutward:
                alignedRotation = angleDegrees;
                break;
            case RotationMode.Tangent:
                alignedRotation = angleDegrees + 90f;
                break;
        }

        float finalRotation = alignedRotation + rotationOffset + RandomRange(randomRotationRange);

        if (boundaryPlane == BoundaryPlane.XZ)
        {
            return Quaternion.Euler(0f, -finalRotation, 0f);
        }

        return Quaternion.Euler(0f, 0f, finalRotation);
    }

    private Vector3 GetScale()
    {
        float uniformScale = RandomRange(randomUniformScaleRange);
        Vector3 scale = baseScale * uniformScale;

        scale.x += Random.Range(-Mathf.Abs(randomScaleRange.x), Mathf.Abs(randomScaleRange.x));
        scale.y += Random.Range(-Mathf.Abs(randomScaleRange.y), Mathf.Abs(randomScaleRange.y));
        scale.z += Random.Range(-Mathf.Abs(randomScaleRange.z), Mathf.Abs(randomScaleRange.z));

        return scale;
    }

    private float RandomRange(Vector2 range)
    {
        return Random.Range(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y));
    }

    private void OnDrawGizmosSelected()
    {
        UnityEditor.Handles.color = Color.red;
        Vector3 normal = boundaryPlane == BoundaryPlane.XZ ? transform.up : transform.forward;
        UnityEditor.Handles.DrawWireDisc(transform.position, normal, radius);
    }
#endif
}
