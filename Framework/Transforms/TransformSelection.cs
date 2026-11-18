using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnscriptedEngine.Transforms
{
    public enum TransformToolMode { Move, Rotate, Scale }
    public enum TransformGizmoHandle { None = -1, X, Y, Z, Center }

    /// <summary>Non-overlapping transform roots with cached geometry for a bounds-center pivot.</summary>
    public sealed class TransformSelection
    {
        private readonly List<Transform> targets = new List<Transform>();
        private readonly Dictionary<Transform, Renderer[]> renderers = new Dictionary<Transform, Renderer[]>();
        private readonly Dictionary<Transform, Collider[]> colliders = new Dictionary<Transform, Collider[]>();
        public const int MaximumTargets = 32;
        public IReadOnlyList<Transform> Targets => targets;
        public int Count => targets.Count;
        public Transform Active => targets.Count > 0 ? targets[targets.Count - 1] : null;
        public Vector3 Pivot => Bounds.center;
        public Bounds Bounds
        {
            get
            {
                Bounds result = default;
                bool found = false;
                foreach (Transform target in targets)
                {
                    if (target == null) continue;
                    Bounds bounds = GetBounds(target);
                    if (!found) { result = bounds; found = true; }
                    else result.Encapsulate(bounds);
                }
                return result;
            }
        }

        public Bounds GetBounds(Transform target)
        {
            Bounds result = new Bounds(target.position, Vector3.zero);
            bool found = false;
            if (renderers.TryGetValue(target, out Renderer[] shapes))
                foreach (Renderer shape in shapes)
                {
                    if (shape == null || !shape.enabled || shape is LineRenderer || shape is ParticleSystemRenderer) continue;
                    if (!found) { result = shape.bounds; found = true; }
                    else result.Encapsulate(shape.bounds);
                }
            if (!found && colliders.TryGetValue(target, out Collider[] volumes))
                foreach (Collider volume in volumes)
                {
                    if (volume == null || !volume.enabled) continue;
                    if (!found) { result = volume.bounds; found = true; }
                    else result.Encapsulate(volume.bounds);
                }
            return result;
        }

        public bool Contains(Transform target) => targets.Contains(target);
        public void Set(Transform target) { Clear(); if (target != null) Toggle(target); }
        public bool Toggle(Transform target)
        {
            if (target == null) return false;
            if (targets.Contains(target)) { Remove(target); return true; }
            // Selecting a parent replaces its selected children, and vice versa: never transform twice.
            for (int index = targets.Count - 1; index >= 0; index--)
                if (targets[index] == null || target.IsChildOf(targets[index]) || targets[index].IsChildOf(target)) Remove(targets[index]);
            if (targets.Count >= MaximumTargets) return false;
            targets.Add(target);
            renderers[target] = target.GetComponentsInChildren<Renderer>(true);
            colliders[target] = target.GetComponentsInChildren<Collider>(true);
            return true;
        }

        public void Prune(Predicate<Transform> valid)
        {
            for (int index = targets.Count - 1; index >= 0; index--)
                if (targets[index] == null || !valid(targets[index])) Remove(targets[index]);
        }

        private void Remove(Transform target)
        {
            targets.Remove(target);
            // Destroyed Unity objects still have a valid managed dictionary key.
            if (ReferenceEquals(target, null)) return;
            renderers.Remove(target);
            colliders.Remove(target);
        }

        public void Clear() { targets.Clear(); renderers.Clear(); colliders.Clear(); }
    }

    public readonly struct TransformPose
    {
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;
        public readonly Vector3 LocalScale;
        public TransformPose(Transform target) : this(target.position, target.rotation, target.localScale) { }
        public TransformPose(Vector3 position, Quaternion rotation, Vector3 localScale)
        { Position = position; Rotation = rotation; LocalScale = localScale; }
        public bool IsValid => PhysicsForceUtility.IsFinite(Position) && PhysicsForceUtility.IsFinite(LocalScale)
            && PhysicsForceUtility.IsFinite(new Vector3(Rotation.x, Rotation.y, Rotation.z))
            && !float.IsNaN(Rotation.w) && !float.IsInfinity(Rotation.w)
            && Mathf.Abs(1f - Quaternion.Dot(Rotation, Rotation)) < .01f
            && Mathf.Abs(LocalScale.x) >= .0001f && Mathf.Abs(LocalScale.y) >= .0001f && Mathf.Abs(LocalScale.z) >= .0001f;

        public void Apply(Transform target)
        {
            target.SetPositionAndRotation(Position, Rotation);
            target.localScale = LocalScale;
            if (target.TryGetComponent(out Rigidbody body)) { body.position = Position; body.rotation = Rotation; }
        }
    }

    /// <summary>Baseline-relative group edits. No reparenting; physics state is restored on commit/cancel.</summary>
    public sealed class TransformGroupEdit : IDisposable
    {
        private readonly Transform[] targets;
        private readonly TransformPose[] originals;
        private readonly Matrix4x4[] originalMatrices;
        private readonly Dictionary<Rigidbody, BodyState> bodies = new Dictionary<Rigidbody, BodyState>();
        private bool finished;
        public Vector3 Pivot { get; }
        public IReadOnlyList<Transform> Targets => targets;

        private readonly struct BodyState
        {
            public readonly bool Kinematic;
            public readonly Vector3 Velocity, AngularVelocity;
            public BodyState(Rigidbody body)
            { Kinematic = body.isKinematic; Velocity = body.linearVelocity; AngularVelocity = body.angularVelocity; }
        }

        public TransformGroupEdit(TransformSelection selection)
        {
            Pivot = selection.Pivot;
            targets = new Transform[selection.Count];
            originals = new TransformPose[selection.Count];
            originalMatrices = new Matrix4x4[selection.Count];
            for (int index = 0; index < targets.Length; index++)
            {
                Transform target = selection.Targets[index];
                targets[index] = target;
                originals[index] = new TransformPose(target);
                originalMatrices[index] = target.localToWorldMatrix;
                foreach (Rigidbody body in target.GetComponentsInChildren<Rigidbody>())
                {
                    if (bodies.ContainsKey(body)) continue;
                    bodies.Add(body, new BodyState(body));
                    body.isKinematic = true;
                }
            }
        }

        public void Move(Vector3 delta)
        {
            if (finished || !PhysicsForceUtility.IsFinite(delta)) return;
            for (int index = 0; index < targets.Length; index++)
                if (targets[index] != null) new TransformPose(originals[index].Position + delta,
                    originals[index].Rotation, originals[index].LocalScale).Apply(targets[index]);
        }

        public void Rotate(Quaternion delta)
        {
            if (finished || !new TransformPose(Vector3.zero, delta, Vector3.one).IsValid) return;
            for (int index = 0; index < targets.Length; index++)
                if (targets[index] != null) new TransformPose(Pivot + delta * (originals[index].Position - Pivot),
                    delta * originals[index].Rotation, originals[index].LocalScale).Apply(targets[index]);
        }

        public void Scale(Vector3 factors, Quaternion basis)
        {
            if (finished || !PhysicsForceUtility.IsFinite(factors) || factors.x < .01f || factors.y < .01f || factors.z < .01f) return;
            Quaternion inverse = Quaternion.Inverse(basis);
            Matrix4x4 scaleMatrix = Matrix4x4.Rotate(basis) * Matrix4x4.Scale(factors) * Matrix4x4.Rotate(inverse);
            for (int index = 0; index < targets.Length; index++)
            {
                Transform target = targets[index];
                if (target == null) continue;
                TransformPose original = originals[index];
                Matrix4x4 parentInverse = target.parent != null ? target.parent.worldToLocalMatrix : Matrix4x4.identity;
                Matrix4x4 local = parentInverse * scaleMatrix * originalMatrices[index];
                // Unity Transform stores TRS, not shear. Preserve rotation and use scaled basis lengths.
                Vector3 scale = new Vector3(local.MultiplyVector(Vector3.right).magnitude * Mathf.Sign(original.LocalScale.x),
                    local.MultiplyVector(Vector3.up).magnitude * Mathf.Sign(original.LocalScale.y),
                    local.MultiplyVector(Vector3.forward).magnitude * Mathf.Sign(original.LocalScale.z));
                new TransformPose(Pivot + scaleMatrix.MultiplyVector(original.Position - Pivot), original.Rotation, scale).Apply(target);
            }
        }

        public void Finish(bool cancel)
        {
            if (finished) return;
            finished = true;
            if (cancel)
                for (int index = 0; index < targets.Length; index++)
                    if (targets[index] != null) originals[index].Apply(targets[index]);
            foreach (KeyValuePair<Rigidbody, BodyState> pair in bodies)
            {
                if (pair.Key == null) continue;
                pair.Key.isKinematic = pair.Value.Kinematic;
                if (!pair.Value.Kinematic)
                { pair.Key.linearVelocity = pair.Value.Velocity; pair.Key.angularVelocity = pair.Value.AngularVelocity; }
            }
            Physics.SyncTransforms();
        }
        public void Dispose() => Finish(true);
    }
}
