using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace UnscriptedEngine
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Unscripted Engine/Object Animation/Brain")]
    public sealed class ObjectAnimBrain : MonoBehaviour
    {
        [Tooltip("Empty: use components on this object only. Explicit lists may include child visuals.")]
        [SerializeField] private ObjectAnimComponent[] components;
        [Min(0f)] [SerializeField] private float duration = 0.3f;
        [Tooltip("Total cycles, including the first. Zero repeats until Stop or ResetPose.")]
        [Min(0)] [SerializeField] private int cycles = 1;
        [SerializeField] private bool playOnEnable;
        [SerializeField] private bool useUnscaledTime;
        [SerializeField] private UnityEvent completed = new UnityEvent();
        private readonly List<ObjectAnimComponent> active = new();
        private double elapsed;
        private float playingDuration;
        private int playingCycles;

        public bool IsPlaying { get; private set; }
        public float Duration { get => duration; set => duration = Mathf.Max(0f, value); }
        public int Cycles { get => cycles; set => cycles = Mathf.Max(0, value); }
        public bool UseUnscaledTime { get => useUnscaledTime; set => useUnscaledTime = value; }
        public bool PlayOnEnable { get => playOnEnable; set => playOnEnable = value; }
        public UnityEvent Completed => completed;

        /// <summary>Replace the composition, restoring the previous pose first.</summary>
        public void SetComponents(params ObjectAnimComponent[] values)
        {
            ResetPose();
            components = values == null ? null : (ObjectAnimComponent[])values.Clone();
        }

        [ContextMenu("Play")]
        public void Play()
        {
            ResetPose();
            if (!isActiveAndEnabled) return;
            var candidates = components != null && components.Length > 0 ? components : GetComponents<ObjectAnimComponent>();
            foreach (var component in candidates)
                if (component != null && !active.Contains(component) && component.Capture(this)) active.Add(component);
            if (active.Count == 0) return;
            elapsed = 0;
            playingDuration = duration;
            playingCycles = cycles;
            if (playingDuration <= 0f)
            {
                Sample(1f);
                completed.Invoke();
                return;
            }
            IsPlaying = true;
            Sample(0f);
            UpdateManager.RegisterUpdate(this, Tick);
        }

        private void Tick(float deltaTime) => Advance(useUnscaledTime ? Time.unscaledDeltaTime : deltaTime);

        /// <summary>Advance by seconds; also supports deterministic previews and tests.</summary>
        public void Advance(float seconds)
        {
            if (!IsPlaying || !isActiveAndEnabled || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
            elapsed += Mathf.Max(0f, seconds);
            if (playingCycles > 0 && elapsed >= (double)playingDuration * playingCycles)
            {
                Sample(1f);
                Stop();
                completed.Invoke();
                return;
            }
            Sample((float)((elapsed % playingDuration) / playingDuration));
        }

        private void Sample(float progress)
        {
            foreach (var component in active) if (component != null) component.Sample(this, progress);
        }

        /// <summary>Stop the clock, retaining the current pose. ResetPose restores it.</summary>
        [ContextMenu("Stop")]
        public void Stop()
        {
            IsPlaying = false;
            UpdateManager.UnregisterUpdate(this);
        }

        [ContextMenu("Reset Pose")]
        public void ResetPose()
        {
            Stop();
            foreach (var component in active) if (component != null) component.Restore(this);
            active.Clear();
        }

        private void OnEnable() { if (playOnEnable) Play(); }
        private void OnDisable() => ResetPose();
    }
}
