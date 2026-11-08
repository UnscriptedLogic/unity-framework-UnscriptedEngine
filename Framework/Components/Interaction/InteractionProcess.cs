using System;

namespace UnscriptedEngine
{
    public enum InteractionMode { Instant, HoldReset, HoldResume }
    public enum InteractionState { Idle, Active, Paused, Completed }

    /// <summary>Timing and interruption policy, independent of input, Unity events, and networking.</summary>
    public sealed class InteractionProcess
    {
        public float Progress01 { get; private set; }
        public InteractionState State { get; private set; }
        private InteractionMode activeMode;
        private float activeDuration;

        public bool TryBegin(InteractionMode mode, float duration, bool repeatable)
        {
            if (State == InteractionState.Active || (State == InteractionState.Completed && !repeatable))
                return false;

            if (State == InteractionState.Completed) Progress01 = 0f;
            activeMode = mode;
            activeDuration = Math.Max(0f, duration);
            State = InteractionState.Active;
            return true;
        }

        public bool Advance(float deltaTime)
        {
            if (State != InteractionState.Active) return false;
            Progress01 = activeMode == InteractionMode.Instant || activeDuration <= 0f
                ? 1f : Math.Min(1f, Progress01 + Math.Max(0f, deltaTime) / activeDuration);
            if (Progress01 < 1f) return false;
            State = InteractionState.Completed;
            return true;
        }

        public void Interrupt()
        {
            if (State != InteractionState.Active) return;
            if (activeMode == InteractionMode.HoldResume)
                State = InteractionState.Paused;
            else
                Reset();
        }

        public void Reset()
        {
            Progress01 = 0f;
            State = InteractionState.Idle;
        }
    }
}
