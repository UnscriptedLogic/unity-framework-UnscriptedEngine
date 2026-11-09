using UnityEngine;

namespace UnscriptedEngine
{
    public enum NpcActionStatus { Running, Succeeded, Failed }

    public interface INpcAction
    {
        void Start();
        NpcActionStatus Tick(float deltaTime);
        void Cancel();
    }

    public interface INpcNavigation
    {
        Vector3 Position { get; }
        NavMeshMovementComponent.NavigationStatus Status { get; }
        bool MoveTo(Vector3 destination, float stoppingDistance);
        void Stop();
    }

    public sealed class NpcIdleAction : INpcAction
    {
        private float duration;
        private float remaining;

        public void Configure(float seconds) => duration = Mathf.Max(0f, seconds);
        public void Start() => remaining = duration;
        public NpcActionStatus Tick(float deltaTime)
        {
            remaining -= Mathf.Max(0f, deltaTime);
            return remaining <= 0f ? NpcActionStatus.Succeeded : NpcActionStatus.Running;
        }
        public void Cancel() => remaining = 0f;
    }

    /// <summary>Reusable movement execution with total and no-progress deadlines.</summary>
    public sealed class NpcMoveAction : INpcAction
    {
        private readonly INpcNavigation navigation;
        private Vector3 destination;
        private Vector3 progressPosition;
        private float stoppingDistance;
        private float timeout;
        private float stuckTimeout;
        private float progressDistance;
        private bool includeVerticalProgress;
        private float elapsed;
        private float stalled;
        private NpcActionStatus status;

        public NpcMoveAction(INpcNavigation navigation) => this.navigation = navigation;

        public void Configure(Vector3 target, float arrivalDistance, float maxDuration,
            float maxStallDuration, float minimumProgress)
        {
            Configure(target, arrivalDistance, maxDuration, maxStallDuration, minimumProgress, false);
        }

        public void Configure(Vector3 target, float arrivalDistance, float maxDuration,
            float maxStallDuration, float minimumProgress, bool trackVerticalProgress)
        {
            destination = target;
            stoppingDistance = Mathf.Max(0.01f, arrivalDistance);
            timeout = Mathf.Max(0.1f, maxDuration);
            stuckTimeout = Mathf.Max(0.1f, maxStallDuration);
            progressDistance = Mathf.Max(0.01f, minimumProgress);
            includeVerticalProgress = trackVerticalProgress;
        }

        public void Start()
        {
            elapsed = 0f;
            stalled = 0f;
            progressPosition = navigation.Position;
            status = navigation.MoveTo(destination, stoppingDistance)
                ? NpcActionStatus.Running : NpcActionStatus.Failed;
            if (status == NpcActionStatus.Failed) navigation.Stop();
        }

        public NpcActionStatus Tick(float deltaTime)
        {
            if (status != NpcActionStatus.Running) return status;
            if (navigation.Status == NavMeshMovementComponent.NavigationStatus.Arrived)
                return Finish(NpcActionStatus.Succeeded);
            if (navigation.Status != NavMeshMovementComponent.NavigationStatus.Moving)
                return Finish(NpcActionStatus.Failed);

            float step = Mathf.Max(0f, deltaTime);
            elapsed += step;
            stalled += step;
            Vector3 displacement = navigation.Position - progressPosition;
            if (!includeVerticalProgress) displacement.y = 0f;
            if (displacement.sqrMagnitude >= progressDistance * progressDistance)
            {
                progressPosition = navigation.Position;
                stalled = 0f;
            }
            return elapsed >= timeout || stalled >= stuckTimeout
                ? Finish(NpcActionStatus.Failed) : status;
        }

        public void Cancel() => Finish(NpcActionStatus.Failed);

        private NpcActionStatus Finish(NpcActionStatus result)
        {
            navigation.Stop();
            status = result;
            return status;
        }
    }
}
