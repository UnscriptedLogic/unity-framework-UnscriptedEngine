using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnscriptedEngine.Transforms
{
    /// <summary>Camera-sized runtime transform handles. Rendering and picking use the same geometry.</summary>
    public sealed class RuntimeTransformGizmo : MonoBehaviour
    {
        private const int Segments = 96;
        private readonly LineRenderer[] axes = new LineRenderer[3];
        private readonly MeshRenderer[] tips = new MeshRenderer[3];
        private readonly LineRenderer[] rings = new LineRenderer[4];
        private readonly Vector3[][] ringPoints = new Vector3[4][];
        private readonly Material[] colors = new Material[4];
        private readonly List<LineRenderer> boxes = new List<LineRenderer>();
        private readonly Vector3[] boxPoints = new Vector3[16];
        private static readonly int[] BoxOrder = { 0,1,3,2,0,4,5,1,5,7,3,7,6,2,6,4 };
        private Mesh cone;
        private Material highlight;
        private MeshRenderer center;
        private TransformToolMode mode;
        private bool initialized;
        public float Size { get; private set; }
        public Vector3 Pivot => transform.position;
        public Quaternion Basis => transform.rotation;
        public TransformToolMode Mode => mode;

        public void Initialize(Shader shader)
        {
            if (initialized) return;
            if (shader == null) throw new System.ArgumentNullException(nameof(shader));
            initialized = true;
            Color[] palette = { new Color(1f,.22f,.22f), new Color(.3f,.95f,.3f), new Color(.25f,.5f,1f), Color.white };
            for (int index = 0; index < colors.Length; index++)
            {
                colors[index] = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                colors[index].color = palette[index];
            }
            highlight = new Material(shader) { color = new Color(1f,.85f,.1f), hideFlags = HideFlags.HideAndDontSave };
            cone = CreateCone();
            for (int index = 0; index < 3; index++)
            {
                axes[index] = CreateLine("Axis " + index, colors[index], false, 2);
                axes[index].SetPositions(new[] { Axis(index) * .12f, Axis(index) * .86f });
                tips[index] = CreateMesh("Tip " + index, cone, colors[index]);
                tips[index].transform.localPosition = Axis(index) * .84f;
                tips[index].transform.localRotation = Quaternion.FromToRotation(Vector3.up, Axis(index));
            }
            center = CreateCube("Free move / uniform scale", colors[3]);
            center.transform.localScale = Vector3.one * .13f;
            for (int index = 0; index < 4; index++)
            {
                rings[index] = CreateLine("Rotation " + index, colors[index], false, Segments + 1);
                ringPoints[index] = new Vector3[Segments + 1];
            }
        }

        public void Present(Camera camera, TransformSelection selection, TransformToolMode tool, Vector3 pivot,
            Quaternion basis, TransformGizmoHandle hovered)
        {
            if (!initialized || camera == null) return;
            mode = tool;
            transform.SetPositionAndRotation(pivot, basis);
            Size = WorldUnitsPerPixel(camera, pivot) * 90f;
            transform.localScale = Vector3.one * Size;
            bool rotation = tool == TransformToolMode.Rotate;
            center.gameObject.SetActive(!rotation);
            center.sharedMaterial = hovered == TransformGizmoHandle.Center ? highlight : colors[3];
            for (int index = 0; index < 3; index++)
            {
                axes[index].gameObject.SetActive(!rotation);
                tips[index].gameObject.SetActive(!rotation);
                axes[index].widthMultiplier = Size * .018f;
                axes[index].SetPosition(1, Axis(index) * (tool == TransformToolMode.Scale ? .94f : .86f));
                Material color = hovered == (TransformGizmoHandle)index ? highlight : colors[index];
                axes[index].sharedMaterial = tips[index].sharedMaterial = color;
                MeshFilter filter = tips[index].GetComponent<MeshFilter>();
                filter.sharedMesh = tool == TransformToolMode.Scale ? center.GetComponent<MeshFilter>().sharedMesh : cone;
                tips[index].transform.localPosition = Axis(index) * (tool == TransformToolMode.Scale ? 1f : .84f);
                tips[index].transform.localScale = tool == TransformToolMode.Scale ? Vector3.one * .13f : Vector3.one;
            }
            for (int index = 0; index < rings.Length; index++)
            {
                rings[index].gameObject.SetActive(rotation);
                if (!rotation) continue;
                Vector3 normal = index < 3 ? Axis(index) : Quaternion.Inverse(basis) * camera.transform.forward;
                FillRing(ringPoints[index], normal, index == 3 ? 1.18f : 1f);
                rings[index].SetPositions(ringPoints[index]);
                rings[index].widthMultiplier = Size * .014f;
                rings[index].sharedMaterial = hovered == (TransformGizmoHandle)index ? highlight : colors[index];
            }
            DrawSelection(selection);
        }

        public TransformGizmoHandle Pick(Camera camera, Vector2 pointer)
        {
            if (!initialized || camera == null || camera.WorldToScreenPoint(Pivot).z <= 0f) return TransformGizmoHandle.None;
            Vector2 screenPivot = camera.WorldToScreenPoint(Pivot);
            if (mode != TransformToolMode.Rotate && Vector2.Distance(pointer, screenPivot) <= 10f) return TransformGizmoHandle.Center;
            float nearest = 11f;
            TransformGizmoHandle picked = TransformGizmoHandle.None;
            int count = mode == TransformToolMode.Rotate ? 4 : 3;
            for (int index = 0; index < count; index++)
            {
                if (mode == TransformToolMode.Rotate)
                {
                    for (int point = 1; point <= Segments; point++)
                    {
                        float distance = ScreenSegmentDistance(camera, pointer, transform.TransformPoint(ringPoints[index][point - 1]),
                            transform.TransformPoint(ringPoints[index][point]));
                        if (distance < nearest) { nearest = distance; picked = (TransformGizmoHandle)index; }
                    }
                }
                else
                {
                    float distance = ScreenSegmentDistance(camera, pointer, Pivot + Basis * Axis(index) * Size * .15f,
                        Pivot + Basis * Axis(index) * Size * 1.06f);
                    if (distance < nearest) { nearest = distance; picked = (TransformGizmoHandle)index; }
                }
            }
            return picked;
        }

        public static Vector3 Axis(int index) => index == 0 ? Vector3.right : index == 1 ? Vector3.up : Vector3.forward;
        public static float WorldUnitsPerPixel(Camera camera, Vector3 position)
        {
            if (camera.orthographic) return 2f * camera.orthographicSize / Mathf.Max(1, camera.pixelHeight);
            float depth = Mathf.Max(.1f, Vector3.Dot(position - camera.transform.position, camera.transform.forward));
            return 2f * depth * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * .5f) / Mathf.Max(1, camera.pixelHeight);
        }

        private static float ScreenSegmentDistance(Camera camera, Vector2 pointer, Vector3 start, Vector3 end)
        {
            Vector3 a = camera.WorldToScreenPoint(start), b = camera.WorldToScreenPoint(end);
            if (a.z <= 0f || b.z <= 0f) return float.PositiveInfinity;
            Vector2 span = (Vector2)(b - a);
            float fraction = span.sqrMagnitude > .001f ? Mathf.Clamp01(Vector2.Dot(pointer - (Vector2)a, span) / span.sqrMagnitude) : 0f;
            return Vector2.Distance(pointer, (Vector2)a + span * fraction);
        }

        private static void FillRing(Vector3[] points, Vector3 normal, float radius)
        {
            Vector3 tangent = Vector3.Cross(normal, Mathf.Abs(normal.y) > .9f ? Vector3.right : Vector3.up).normalized;
            Vector3 bitangent = Vector3.Cross(normal, tangent).normalized;
            for (int index = 0; index <= Segments; index++)
            {
                float angle = index * Mathf.PI * 2f / Segments;
                points[index] = (tangent * Mathf.Cos(angle) + bitangent * Mathf.Sin(angle)) * radius;
            }
        }

        private void DrawSelection(TransformSelection selection)
        {
            while (boxes.Count < selection.Count) boxes.Add(CreateLine("Selected bounds", highlight, true, BoxOrder.Length));
            for (int index = 0; index < boxes.Count; index++)
            {
                boxes[index].gameObject.SetActive(index < selection.Count);
                if (index >= selection.Count) continue;
                Bounds bounds = selection.GetBounds(selection.Targets[index]);
                for (int point = 0; point < BoxOrder.Length; point++)
                {
                    int corner = BoxOrder[point];
                    boxPoints[point] = bounds.center + Vector3.Scale(bounds.extents,
                        new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                }
                boxes[index].SetPositions(boxPoints);
                boxes[index].widthMultiplier = Size * .009f;
            }
        }

        private LineRenderer CreateLine(string label, Material material, bool worldSpace, int count)
        {
            GameObject root = new GameObject(label) { layer = 2, hideFlags = HideFlags.DontSave };
            root.transform.SetParent(transform, false);
            LineRenderer line = root.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = worldSpace;
            line.positionCount = count;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        private MeshRenderer CreateMesh(string label, Mesh mesh, Material material)
        {
            GameObject root = new GameObject(label) { layer = 2, hideFlags = HideFlags.DontSave };
            root.transform.SetParent(transform, false);
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = root.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        private MeshRenderer CreateCube(string label, Material material)
        {
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = label; root.layer = 2; root.hideFlags = HideFlags.DontSave;
            root.transform.SetParent(transform, false);
            Collider collider = root.GetComponent<Collider>();
            collider.enabled = false; Destroy(collider);
            MeshRenderer renderer = root.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        private static Mesh CreateCone()
        {
            const int sides = 16;
            Vector3[] vertices = new Vector3[sides + 2];
            int[] triangles = new int[sides * 6];
            vertices[sides] = Vector3.up * .22f;
            for (int index = 0; index < sides; index++)
            {
                float angle = index * Mathf.PI * 2f / sides;
                vertices[index] = new Vector3(Mathf.Cos(angle) * .075f, 0f, Mathf.Sin(angle) * .075f);
                int next = (index + 1) % sides;
                triangles[index * 6] = index; triangles[index * 6 + 1] = sides; triangles[index * 6 + 2] = next;
                triangles[index * 6 + 3] = next; triangles[index * 6 + 4] = sides + 1; triangles[index * 6 + 5] = index;
            }
            Mesh mesh = new Mesh { name = "Runtime gizmo cone", hideFlags = HideFlags.HideAndDontSave };
            mesh.vertices = vertices; mesh.triangles = triangles; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        private void OnDestroy()
        {
            foreach (Material material in colors) if (material != null) Destroy(material);
            if (highlight != null) Destroy(highlight);
            if (cone != null) Destroy(cone);
        }
    }
}
