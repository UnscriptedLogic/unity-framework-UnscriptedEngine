using System;
using UnityEngine;

namespace UnscriptedEngine
{
    // A control owns one frame subscription only while its visual is moving.
    internal sealed class ValueAnimation
    {
        private readonly Behaviour owner;
        private Action<float> apply;
        private Action completed;
        private float elapsed, duration;
        public ValueAnimation(Behaviour owner) => this.owner = owner;
        public void Play(float seconds, Action<float> applyValue, Action onCompleted = null)
        {
            Stop();
            apply = applyValue;
            completed = onCompleted;
            duration = seconds;
            elapsed = 0;
            if (seconds <= 0 || !owner.isActiveAndEnabled || !Application.isPlaying)
            { applyValue(1); Stop(); onCompleted?.Invoke(); return; }
            UpdateManager.RegisterUpdate(owner, Advance);
        }
        private void Advance(float deltaTime)
        {
            elapsed += deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            apply?.Invoke((1f - Mathf.Cos(t * Mathf.PI)) * 0.5f);
            if (t < 1) return;
            var callback = completed;
            Stop();
            callback?.Invoke();
        }
        public void Stop()
        {
            UpdateManager.UnregisterUpdate(owner);
            apply = null;
            completed = null;
        }
    }
}
