using System.Collections.Generic;
using UnityEngine;

namespace UnscriptedEngine.Gameplay
{
    /// <summary>Appends low-resolution world-space debug surfaces to caller-owned buffers.</summary>
    public static class ColliderZoneSurface
    {
        public static void Append(Collider collider, List<Vector3> vertices, List<int> triangles)
        {
            if (collider is SphereCollider sphere)
            {
                Vector3 scale = sphere.transform.lossyScale;
                float radius = sphere.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
                AppendRounded(vertices, triangles, sphere.transform.TransformPoint(sphere.center),
                    Vector3.up, Vector3.right, Vector3.forward, radius, 0f);
            }
            else if (collider is CapsuleCollider capsule)
            {
                Vector3 scale = capsule.transform.lossyScale;
                Vector3 axis = capsule.transform.TransformDirection(Axis(capsule.direction));
                Vector3 side = capsule.transform.TransformDirection(Axis((capsule.direction + 1) % 3));
                float radius = capsule.radius * Mathf.Max(Mathf.Abs(scale[(capsule.direction + 1) % 3]), Mathf.Abs(scale[(capsule.direction + 2) % 3]));
                float length = Mathf.Max(0f, capsule.height * Mathf.Abs(scale[capsule.direction]) * 0.5f - radius);
                AppendRounded(vertices, triangles, capsule.transform.TransformPoint(capsule.center),
                    axis, side, Vector3.Cross(axis, side), radius, length);
            }
            else
            {
                BoxCollider box = collider as BoxCollider;
                Bounds bounds = collider.bounds;
                for (int axis = 0; axis < 3; axis++)
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    int start = vertices.Count;
                    for (int corner = 0; corner < 4; corner++)
                    {
                        Vector3 point = Axis(axis) * sign;
                        point[(axis + 1) % 3] = corner == 0 || corner == 3 ? -1f : 1f;
                        point[(axis + 2) % 3] = corner < 2 ? -1f : 1f;
                        vertices.Add(box != null ? box.transform.TransformPoint(box.center + Vector3.Scale(point, box.size * 0.5f))
                            : bounds.center + Vector3.Scale(point, bounds.extents));
                    }
                    AddQuad(triangles, start, start + 1, start + 2, start + 3);
                }
            }
        }

        private static Vector3 Axis(int axis) => axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward;

        private static void AppendRounded(List<Vector3> vertices, List<int> triangles, Vector3 center,
            Vector3 axis, Vector3 side, Vector3 forward, float radius, float length)
        {
            const int sides = 16;
            const int hemisphereRings = 6;
            int start = vertices.Count;
            // Duplicate the equator for capsules so the connecting strip is a straight cylinder.
            int rings = length > 0f ? hemisphereRings * 2 + 1 : hemisphereRings * 2;
            for (int ring = 0; ring <= rings; ring++)
            {
                int latitude = length > 0f && ring > hemisphereRings ? ring - 1 : ring;
                float angle = latitude * Mathf.PI / (hemisphereRings * 2);
                float offset = ring <= hemisphereRings ? length : -length;
                for (int sideIndex = 0; sideIndex <= sides; sideIndex++)
                {
                    float longitude = sideIndex * Mathf.PI * 2f / sides;
                    Vector3 radial = side * Mathf.Cos(longitude) + forward * Mathf.Sin(longitude);
                    vertices.Add(center + axis * (Mathf.Cos(angle) * radius + offset) + radial * (Mathf.Sin(angle) * radius));
                }
            }
            for (int ring = 0; ring < rings; ring++)
            for (int sideIndex = 0; sideIndex < sides; sideIndex++)
            {
                int a = start + ring * (sides + 1) + sideIndex;
                AddQuad(triangles, a, a + 1, a + sides + 2, a + sides + 1);
            }
        }

        private static void AddQuad(List<int> triangles, int a, int b, int c, int d)
        {
            triangles.Add(a); triangles.Add(b); triangles.Add(c);
            triangles.Add(a); triangles.Add(c); triangles.Add(d);
        }
    }
}
