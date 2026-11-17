using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace UnscriptedEngine
{
    public enum ObjectMotionMode { TransformOnly, PhysicsKinematic }
    public enum ObjectPointTraversal { Path, ReturnToOrigin }
    public enum ObjectMotionCompletion { Stop, Restart, PingPong }

    /// <summary>Owns point sequencing and playback for one transform property.</summary>
    public abstract class ObjectMotionComponent : MonoBehaviour
    {
        public bool PlayOnAwake;
        public ObjectMotionMode MotionMode;
        public ObjectPointTraversal Traversal;
        public ObjectMotionCompletion Completion;
        [Min(0.0001f)] public float Speed = 1f;
        [Tooltip("Offsets from the captured starting pose. Rotation uses degrees; scale uses additive local-scale offsets.")]
        public Vector3[] Points = { Vector3.one };
        public UnityEvent Completed = new UnityEvent();
        public bool IsPlaying { get; private set; }
        protected Rigidbody Body { get; private set; }
        protected bool UsesPhysics => playingMode == ObjectMotionMode.PhysicsKinematic;
        private ObjectMotionMode playingMode;
        private ObjectMotionCompletion playingCompletion;
        private readonly List<Vector3> route = new List<Vector3>();
        private int destination;
        private int direction;
        private float progress;
        private float distance;
        private bool captured;

        private void Start() { if (PlayOnAwake) Play(); }
        private void OnDisable() => Stop();

        public void Play()
        {
            Stop();
            if (!isActiveAndEnabled || Points == null || Points.Length == 0) return;
            playingMode = MotionMode;
            playingCompletion = Completion;
            Body = GetComponent<Rigidbody>();
            if (UsesPhysics && (Body == null || !Body.isKinematic))
            {
                Debug.LogError("Kinematic object motion requires a kinematic Rigidbody on the same object.", this);
                return;
            }
            if (!captured) { CapturePose(); captured = true; }
            route.Clear();
            route.Add(Vector3.zero);
            foreach (var point in Points)
            {
                if (!IsFinite(point)) { Debug.LogError("Motion points must be finite.", this); return; }
                route.Add(point);
                if (Traversal == ObjectPointTraversal.ReturnToOrigin) route.Add(Vector3.zero);
            }
            direction = 1;
            destination = 1;
            progress = 0;
            ApplyOffset(route[0], route[0], 1f, true);
            distance = SegmentDistance(route[0], route[1]);
            IsPlaying = true;
            if (UsesPhysics) UpdateManager.RegisterFixedUpdate(this, Advance);
            else UpdateManager.RegisterUpdate(this, Advance);
        }

        public void Stop()
        {
            IsPlaying = false;
            UpdateManager.UnregisterUpdate(this);
            UpdateManager.UnregisterFixedUpdate(this);
        }

        public void ResetPose()
        {
            Stop();
            if (captured) ApplyOffset(Vector3.zero, Vector3.zero, 1f, true);
        }

        /// <summary>Stop and use the current pose as the origin for subsequent playback.</summary>
        public void CaptureCurrentPose() { Stop(); CapturePose(); captured = true; }

        /// <summary>Deterministic clock. Do not call alongside automatic runtime ticking.</summary>
        public void Advance(float seconds)
        {
            if (!IsPlaying || !isActiveAndEnabled || !IsFinite(seconds) || seconds <= 0 || !IsFinite(Speed) || Speed <= 0) return;
            float remaining = seconds * Speed;
            if (!IsFinite(remaining)) return;
            // Bound traversal work for coincident points and extreme frame steps.
            int transitions = 0;
            while (IsPlaying && transitions < 4096)
            {
                float needed = distance * (1f - progress);
                if (distance > 0.000001f && remaining < needed)
                {
                    progress += remaining / distance;
                    break;
                }
                remaining = Mathf.Max(0f, remaining - needed);
                progress = 1;
                if (destination + direction < 0 || destination + direction >= route.Count)
                {
                    if (playingCompletion == ObjectMotionCompletion.Stop)
                    {
                        ApplyOffset(route[destination - direction], route[destination], 1f, false);
                        Stop();
                        Completed.Invoke();
                        return;
                    }
                    if (playingCompletion == ObjectMotionCompletion.PingPong) direction = -direction;
                    else destination = 0;
                }
                destination += direction;
                progress = 0;
                distance = SegmentDistance(route[destination - direction], route[destination]);
                transitions++;
                if (remaining <= 0 && distance > 0.000001f) break;
            }
            // An all-coincident looping route has no motion to perform.
            if (transitions == 4096) { Stop(); return; }
            ApplyOffset(route[destination - direction], route[destination], progress, false);
        }

        protected static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        protected abstract void CapturePose();
        protected abstract float SegmentDistance(Vector3 from, Vector3 to);
        protected abstract void ApplyOffset(Vector3 from, Vector3 to, float progress, bool immediate);
    }
}
