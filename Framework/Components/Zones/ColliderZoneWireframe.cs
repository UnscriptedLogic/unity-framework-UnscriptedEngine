using System.Collections.Generic;
using UnityEngine;

namespace UnscriptedEngine.Gameplay
{
    /// <summary>World-space segment pairs shared by editor gizmos and runtime debug rendering.</summary>
    public static class ColliderZoneWireframe
    {
        public static void Collect(Collider collider, List<Vector3> lines)
        {
            lines.Clear();
            if (collider is SphereCollider sphere)
            {
                Vector3 scale = sphere.transform.lossyScale;
                float radius = sphere.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
                Vector3 center = sphere.transform.TransformPoint(sphere.center);
                Circle(lines, center, Vector3.right, Vector3.up, radius);
                Circle(lines, center, Vector3.right, Vector3.forward, radius);
                Circle(lines, center, Vector3.up, Vector3.forward, radius);
            }
            else if (collider is CapsuleCollider capsule)
            {
                Vector3 scale = capsule.transform.lossyScale;
                Vector3 localAxis = Axis(capsule.direction);
                Vector3 axis = capsule.transform.TransformDirection(localAxis);
                Vector3 side = capsule.transform.TransformDirection(Axis((capsule.direction + 1) % 3));
                Vector3 forward = Vector3.Cross(axis, side);
                float radius = capsule.radius * Mathf.Max(Mathf.Abs(scale[(capsule.direction + 1) % 3]), Mathf.Abs(scale[(capsule.direction + 2) % 3]));
                float length = Mathf.Max(0f, capsule.height * Mathf.Abs(scale[capsule.direction]) * 0.5f - radius);
                Vector3 center = capsule.transform.TransformPoint(capsule.center);
                Circle(lines, center + axis * length, side, forward, radius);
                Circle(lines, center - axis * length, side, forward, radius);
                CapsuleArc(lines, center, axis, side, radius, length);
                CapsuleArc(lines, center, axis, forward, radius, length);
            }
            else
            {
                BoxCollider box = collider as BoxCollider;
                Bounds bounds = collider.bounds;
                for (int axis = 0; axis < 3; axis++)
                for (int first = -1; first <= 1; first += 2)
                for (int second = -1; second <= 1; second += 2)
                {
                    Vector3 a = Vector3.zero;
                    a[axis] = -1f; a[(axis + 1) % 3] = first; a[(axis + 2) % 3] = second;
                    Vector3 b = a; b[axis] = 1f;
                    lines.Add(box != null ? box.transform.TransformPoint(box.center + Vector3.Scale(a, box.size * 0.5f)) : bounds.center + Vector3.Scale(a, bounds.extents));
                    lines.Add(box != null ? box.transform.TransformPoint(box.center + Vector3.Scale(b, box.size * 0.5f)) : bounds.center + Vector3.Scale(b, bounds.extents));
                }
            }
        }

        private static Vector3 Axis(int axis) => axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward;

        private static void Circle(List<Vector3> lines, Vector3 center, Vector3 first, Vector3 second, float radius)
        {
            for (int index = 0; index < 32; index++)
            {
                float a = index * Mathf.PI / 16f;
                float b = (index + 1) * Mathf.PI / 16f;
                lines.Add(center + radius * (first * Mathf.Cos(a) + second * Mathf.Sin(a)));
                lines.Add(center + radius * (first * Mathf.Cos(b) + second * Mathf.Sin(b)));
            }
        }

        private static void CapsuleArc(List<Vector3> lines, Vector3 center, Vector3 axis, Vector3 side, float radius, float length)
        {
            for (int sign = -1; sign <= 1; sign += 2)
            {
                Vector3 cap = center + axis * (length * sign);
                for (int index = 0; index < 16; index++)
                {
                    float a = index * Mathf.PI / 16f;
                    float b = (index + 1) * Mathf.PI / 16f;
                    lines.Add(cap + radius * (side * Mathf.Cos(a) + axis * (Mathf.Sin(a) * sign)));
                    lines.Add(cap + radius * (side * Mathf.Cos(b) + axis * (Mathf.Sin(b) * sign)));
                }
                lines.Add(center + axis * length + side * (radius * sign));
                lines.Add(center - axis * length + side * (radius * sign));
            }
        }

#if UNITY_EDITOR
        private static readonly List<Vector3> gizmoLines = new List<Vector3>(256);
#endif
        public static void DrawGizmos(Collider collider)
        {
#if UNITY_EDITOR
            Collect(collider, gizmoLines);
            for (int index = 0; index < gizmoLines.Count; index += 2) Gizmos.DrawLine(gizmoLines[index], gizmoLines[index + 1]);
#endif
        }
    }
}
