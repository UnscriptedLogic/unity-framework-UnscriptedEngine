using UnityEngine;
using UnityEngine.AI;

namespace UnscriptedEngine
{
    /// <summary>Server-side steering. CharacterMovementComponent remains the movement authority.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent), typeof(CharacterMovementComponent))]
    [DefaultExecutionOrder(-40)]
    public class NavMeshMovementComponent : MonoBehaviour
    {
        public enum NavigationStatus { Idle, Moving, Arrived, Failed }

        [SerializeField, Min(0.01f)] private float sampleDistance = 1f;
        [SerializeField, Min(0.01f)] private float maxAgentDrift = 1f;
        private NavMeshAgent agent;
        private CharacterMovementComponent movement;
        private bool stopping;

        public NavigationStatus Status { get; private set; }
        public bool HasRequest => Status == NavigationStatus.Moving;
        private bool CanNavigate => movement != null && movement.IsSpawned && movement.IsServer && movement.IsOwner;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            movement = GetComponent<CharacterMovementComponent>();
            agent.updatePosition = false;
            agent.updateRotation = false;
            agent.updateUpAxis = false;
            agent.autoTraverseOffMeshLink = false;
            agent.enabled = false;
        }

        private void FixedUpdate()
        {
            if (!CanNavigate)
            {
                Stop();
                agent.enabled = false;
                return;
            }

            if (agent.enabled && agent.isOnNavMesh)
                agent.nextPosition = movement.SimulationPosition;
        }

        public bool MoveTo(Vector3 candidate, float stoppingDistance)
        {
            Stop();
            if (!CanNavigate || !isActiveAndEnabled || movement.IsFlying)
                return Fail();

            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            // Never teleport the motor to make an invalid spawn navigable.
            if (!NavMesh.SamplePosition(movement.SimulationPosition, out NavMeshHit start, sampleDistance, filter)
                || !NavMesh.SamplePosition(candidate, out NavMeshHit target, sampleDistance, filter))
                return Fail();

            agent.enabled = true;
            if (!agent.Warp(start.position))
                return Fail();

            agent.speed = movement.MaxWalkSpeed;
            agent.acceleration = movement.WalkAcceleration;
            agent.stoppingDistance = Mathf.Max(0.01f, stoppingDistance);
            agent.autoBraking = true;
            agent.isStopped = false;
            stopping = false;
            if (!agent.SetDestination(target.position))
                return Fail();

            Status = NavigationStatus.Moving;
            return true;
        }

        public Vector3 GetMovementInput()
        {
            if (!HasRequest)
                return Vector3.zero;

            if (!CanNavigate || !agent.enabled || !agent.isOnNavMesh || movement.IsFlying)
            {
                Fail();
                return Vector3.zero;
            }

            agent.speed = movement.MaxWalkSpeed;
            agent.nextPosition = movement.SimulationPosition;
            if ((agent.nextPosition - movement.SimulationPosition).sqrMagnitude > maxAgentDrift * maxAgentDrift
                || agent.isOnOffMeshLink)
            {
                Fail();
                return Vector3.zero;
            }

            if (agent.pathPending)
                return Vector3.zero;

            if (!agent.hasPath || agent.pathStatus != NavMeshPathStatus.PathComplete)
            {
                Fail();
                return Vector3.zero;
            }

            if (stopping || agent.remainingDistance <= agent.stoppingDistance)
            {
                stopping = true;
                agent.isStopped = true;
                Vector3 velocity = movement.Velocity;
                velocity.y = 0f;
                if (velocity.sqrMagnitude <= 0.01f)
                {
                    agent.ResetPath();
                    Status = NavigationStatus.Arrived;
                }
                return Vector3.zero;
            }

            Vector3 desiredVelocity = agent.desiredVelocity;
            desiredVelocity.y = 0f;
            return Vector3.ClampMagnitude(desiredVelocity / Mathf.Max(movement.MaxWalkSpeed, 0.001f), 1f);
        }

        public void Stop()
        {
            // ResetPath on an active link completes it automatically; disable instead.
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                if (agent.isOnOffMeshLink)
                    agent.enabled = false;
                else
                {
                    agent.isStopped = true;
                    agent.ResetPath();
                }
            }
            stopping = false;
            Status = NavigationStatus.Idle;
            if (CanNavigate)
                movement.SetMovementInput(Vector2.zero);
        }

        private bool Fail()
        {
            Stop();
            Status = NavigationStatus.Failed;
            return false;
        }

        private void OnDisable()
        {
            Stop();
            if (agent != null)
                agent.enabled = false;
        }
    }
}
