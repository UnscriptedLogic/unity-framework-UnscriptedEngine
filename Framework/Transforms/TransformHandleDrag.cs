using UnityEngine;

namespace UnscriptedEngine.Transforms
{
    /// <summary>Converts pointer rays into baseline-relative deltas with a frozen camera-facing drag plane.</summary>
    public sealed class TransformHandleDrag
    {
        private readonly TransformGroupEdit edit;
        private readonly TransformToolMode mode;
        private readonly TransformGizmoHandle handle;
        private readonly Quaternion basis;
        private readonly Vector3 axis, startPoint;
        private readonly Plane plane;
        private readonly Vector2 startPointer, screenAxis;
        private readonly Vector2 screenPivot;
        private readonly float size, unitsPerPixel;
        private readonly bool rayPlaneValid;
        private float previousAngle, totalAngle;
        public Vector3 GizmoPivot { get; private set; }

        public TransformHandleDrag(TransformGroupEdit edit, RuntimeTransformGizmo gizmo, TransformGizmoHandle handle, Camera camera, Vector2 pointer)
        {
            this.edit = edit; this.handle = handle; mode = gizmo.Mode; basis = gizmo.Basis;
            GizmoPivot = edit.Pivot;
            size = gizmo.Size; unitsPerPixel = RuntimeTransformGizmo.WorldUnitsPerPixel(camera, edit.Pivot);
            startPointer = pointer;
            axis = handle == TransformGizmoHandle.Center ? camera.transform.forward : basis * RuntimeTransformGizmo.Axis((int)handle);
            Vector3 normal = mode == TransformToolMode.Rotate ? axis : handle == TransformGizmoHandle.Center ? camera.transform.forward
                : Vector3.ProjectOnPlane(camera.transform.forward, axis).normalized;
            if (normal.sqrMagnitude < .01f) normal = Vector3.ProjectOnPlane(camera.transform.up, axis).normalized;
            plane = new Plane(normal, edit.Pivot);
            rayPlaneValid = TryPoint(camera.ScreenPointToRay(pointer), out startPoint);
            screenPivot = camera.WorldToScreenPoint(edit.Pivot);
            Vector2 projectedAxis = (Vector2)camera.WorldToScreenPoint(edit.Pivot + axis * size) - screenPivot;
            screenAxis = projectedAxis.sqrMagnitude > 16f ? projectedAxis.normalized : Vector2.up;
        }

        public void Update(Camera camera, Vector2 pointer)
        {
            bool hasPoint = TryPoint(camera.ScreenPointToRay(pointer), out Vector3 point);
            Vector2 pixels = pointer - startPointer;
            if (mode == TransformToolMode.Rotate)
            {
                float angle = rayPlaneValid && hasPoint && (startPoint - edit.Pivot).sqrMagnitude > .00001f
                    ? Vector3.SignedAngle(startPoint - edit.Pivot, point - edit.Pivot, axis)
                    : Vector2.SignedAngle(startPointer - screenPivot, pointer - screenPivot)
                        * (Vector3.Dot(axis, camera.transform.forward) >= 0f ? 1f : -1f);
                totalAngle += Mathf.DeltaAngle(previousAngle, angle); previousAngle = angle;
                edit.Rotate(Quaternion.AngleAxis(totalAngle, axis));
                return;
            }
            if (mode == TransformToolMode.Move)
            {
                Vector3 delta;
                if (rayPlaneValid && hasPoint)
                    delta = handle == TransformGizmoHandle.Center ? point - startPoint : axis * Vector3.Dot(point - startPoint, axis);
                else delta = axis * Vector2.Dot(pixels, screenAxis) * unitsPerPixel;
                edit.Move(delta); GizmoPivot = edit.Pivot + delta;
                return;
            }
            Vector3 factors = Vector3.one;
            if (handle == TransformGizmoHandle.Center)
                factors *= Mathf.Clamp(Mathf.Exp((pixels.x + pixels.y) * .01f), .01f, 100f);
            else
            {
                float distance = rayPlaneValid && hasPoint ? Vector3.Dot(point - startPoint, axis) : Vector2.Dot(pixels, screenAxis) * unitsPerPixel;
                factors[(int)handle] = Mathf.Clamp(1f + distance / Mathf.Max(.0001f, size), .01f, 100f);
            }
            edit.Scale(factors, basis);
        }

        private bool TryPoint(Ray ray, out Vector3 point)
        {
            if (plane.Raycast(ray, out float distance) && distance < 10000f) { point = ray.GetPoint(distance); return true; }
            point = default; return false;
        }
    }
}
