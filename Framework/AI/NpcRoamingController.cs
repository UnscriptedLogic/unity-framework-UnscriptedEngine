using UnityEngine;
using UnityEngine.AI;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UnscriptedEngine
{
    /// <summary>Server-only idle/roam coordinator. Owns no movement simulation or planner.</summary>
    [DisallowMultipleComponent]
    public class NpcRoamingController : UController
    {
        public enum RoamingState { Inactive, Idle, Moving, RetryDelay }

        [SerializeField]
        [Tooltip("Destination source on this controller. Defaults to a local provider when left empty.")]
        private NpcTargetProvider targetProvider;
        [SerializeField]
        [Tooltip("Optional home anchor. Its current position is read for each destination request; otherwise use possession position.")]
        private Transform homeAnchor;
        [SerializeField]
        [Tooltip("Minimum and maximum idle time in seconds; initial delay also staggers NPC requests.")]
        private Vector2 idleDuration = new Vector2(1f, 3f);
        [SerializeField]
        [Min(0.1f)]
        [Tooltip("Seconds between failed destination or movement attempts.")]
        private float retryDelay = 2f;
        [SerializeField]
        [Min(0.01f)]
        [Tooltip("Navigation arrival tolerance in metres.")]
        private float stoppingDistance = 0.3f;
        [SerializeField]
        [Min(0.1f)]
        [Tooltip("Maximum seconds allowed for a single movement action.")]
        private float moveTimeout = 30f;
        [SerializeField]
        [Min(0.1f)]
        [Tooltip("Fail after this many seconds without sufficient horizontal displacement.")]
        private float stuckTimeout = 3f;
        [SerializeField]
        [Min(0.01f)]
        [Tooltip("Horizontal displacement in metres that resets the stuck timer.")]
        private float progressDistance = 0.15f;

        [Header("Debug")]
        [SerializeField]
        [Tooltip("Show the current arrival radius, progress threshold, and time ranges when selected.")]
        private bool showGizmos = true;

        private readonly NpcIdleAction idle = new NpcIdleAction();
        private NpcMoveAction move;
        private INpcAction activeAction;
        private NpcNavigationAdapter navigation;
        private NavMeshAgent agent;
        private UPawn boundPawn;
        private Vector3 homePosition;
        private bool roamingEnabled = true;
        private Vector3 activeDestination;

        public RoamingState State { get; private set; }
        public bool RoamingEnabled => roamingEnabled;
        public Vector3 HomePosition => homeAnchor != null ? homeAnchor.position : homePosition;
        public NpcActionStatus? LastMoveStatus { get; private set; }
        protected override int TickOrder => -60;

        public void SetRoamingEnabled(bool enabled)
        {
            roamingEnabled = enabled;
            if (!enabled) StopRoaming();
        }

        public void SetHomePosition(Vector3 position) => homePosition = position;

        public override void UnPossess()
        {
            if (IsServer) ClearBinding();
            base.UnPossess();
        }

        protected override void OnPossess(UPawn pawn)
        {
            base.OnPossess(pawn);
            if (IsServer) BindPawn(pawn);
        }

        private void BindPawn(UPawn pawn)
        {
            ClearBinding();
            LastMoveStatus = null;
            boundPawn = pawn;
            homePosition = pawn.transform.position;
            NavMeshMovementComponent steering = pawn.GetComponent<NavMeshMovementComponent>();
            CharacterMovementComponent motor = pawn.GetComponent<CharacterMovementComponent>();
            agent = pawn.GetComponent<NavMeshAgent>();
            if (steering != null && motor != null && agent != null)
            {
                navigation = new NpcNavigationAdapter(steering, motor);
                move = new NpcMoveAction(navigation);
            }
            if (targetProvider == null) targetProvider = GetComponent<NpcTargetProvider>();
        }

        protected override void OnUnPossess(UPawn pawn)
        {
            ClearBinding();
            base.OnUnPossess(pawn);
        }

        protected override void ServerFixedTick(float deltaTime)
        {
            if (Pawn != null && Pawn != boundPawn) BindPawn(Pawn);
            if (!roamingEnabled || Pawn == null || Pawn != boundPawn || !Pawn.isActiveAndEnabled
                || Pawn.Controller != this || navigation == null || !navigation.IsReady
                || targetProvider == null || !targetProvider.isActiveAndEnabled)
            {
                StopRoaming();
                return;
            }
            if (activeAction == null) BeginIdle(false);
            navigation.Tick();
            NpcActionStatus result = activeAction.Tick(deltaTime);
            if (result == NpcActionStatus.Running) return;
            if (State == RoamingState.Moving)
            {
                LastMoveStatus = result;
                targetProvider.MovementFinished(result);
                if (result == NpcActionStatus.Succeeded && targetProvider.ContinueAfterArrival)
                {
                    StartNextMove();
                    return;
                }
                BeginIdle(result == NpcActionStatus.Failed);
                return;
            }
            StartNextMove();
        }

        private void StartNextMove()
        {
            if (!targetProvider.TryGetDestination(HomePosition, navigation.Position, agent, out Vector3 destination))
            {
                BeginIdle(true);
                return;
            }
            activeAction.Cancel();
            move.Configure(destination, stoppingDistance, moveTimeout, stuckTimeout, progressDistance);
            activeDestination = destination;
            activeAction = move;
            State = RoamingState.Moving;
            LastMoveStatus = NpcActionStatus.Running;
            activeAction.Start();
        }

        private void BeginIdle(bool retry)
        {
            activeAction?.Cancel();
            navigation?.Stop();
            float minimum = Mathf.Max(0.1f, idleDuration.x);
            float maximum = Mathf.Max(minimum, idleDuration.y);
            idle.Configure(retry ? Mathf.Max(0.1f, retryDelay) : Random.Range(minimum, maximum));
            activeAction = idle;
            State = retry ? RoamingState.RetryDelay : RoamingState.Idle;
            activeAction.Start();
        }

        private void StopRoaming()
        {
            activeAction?.Cancel();
            activeAction = null;
            navigation?.Stop();
            State = RoamingState.Inactive;
        }

        private void ClearBinding()
        {
            StopRoaming();
            navigation = null;
            move = null;
            agent = null;
            boundPawn = null;
        }

        protected override void OnDisable() { StopRoaming(); base.OnDisable(); }
        protected override void EndPlay() { StopRoaming(); base.EndPlay(); }
        protected override void OnDespawned() { ClearBinding(); base.OnDespawned(); }
        protected override void OnDestroyed() { ClearBinding(); base.OnDestroyed(); }

        private void OnDrawGizmosSelected()
        {
            if (!showGizmos) return;
            Vector3 position = Pawn != null ? Pawn.transform.position : transform.position;
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(position, progressDistance);
            if (State == RoamingState.Moving)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(activeDestination, stoppingDistance);
                Gizmos.DrawLine(position, activeDestination);
            }
#if UNITY_EDITOR
            Handles.Label(position, $"{State}; idle {idleDuration.x}–{idleDuration.y}s; retry {retryDelay}s\nMove limit {moveTimeout}s; stuck limit {stuckTimeout}s; progress {progressDistance}m; arrival {stoppingDistance}m");
#endif
        }
    }
}
