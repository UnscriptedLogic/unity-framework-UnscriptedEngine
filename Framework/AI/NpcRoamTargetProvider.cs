using UnityEngine;
using UnityEngine.AI;

namespace UnscriptedEngine
{
    [DisallowMultipleComponent]
    public sealed class NpcRoamTargetProvider : NpcTargetProvider
    {
        [SerializeField, Min(0.1f), Tooltip("Horizontal destination radius around the home position.")]
        private float radius = 10f;
        [SerializeField, Min(0.1f), Tooltip("Maximum NavMesh projection distance, also limiting vertical displacement from home.")]
        private float sampleDistance = 2f;
        [SerializeField, Min(0.1f), Tooltip("Minimum horizontal distance from the current position to a new destination.")]
        private float minimumTravel = 1f;
        [SerializeField, Range(1, 16), Tooltip("Maximum samples and path checks per destination request. Failure waits before retrying.")]
        private int attempts = 4;
        private NavMeshPath path;

        public override bool TryGetDestination(Vector3 home, Vector3 current, NavMeshAgent agent,
            out Vector3 destination)
        {
            destination = default;
            if (!isActiveAndEnabled || agent == null) return false;
            NavMeshQueryFilter filter = new NavMeshQueryFilter
                { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            if (!NavMesh.SamplePosition(current, out NavMeshHit start, sampleDistance, filter)) return false;
            if (path == null) path = new NavMeshPath();
            for (int index = 0; index < Mathf.Clamp(attempts, 1, 16); index++)
            {
                Vector2 offset = Random.insideUnitCircle * radius;
                Vector3 candidate = home + new Vector3(offset.x, 0f, offset.y);
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, sampleDistance, filter)) continue;
                Vector3 fromHome = hit.position - home;
                if (Mathf.Abs(fromHome.y) > sampleDistance) continue;
                fromHome.y = 0f;
                Vector3 fromCurrent = hit.position - current;
                fromCurrent.y = 0f;
                if (fromHome.sqrMagnitude > radius * radius
                    || fromCurrent.sqrMagnitude < minimumTravel * minimumTravel) continue;
                if (!NavMesh.CalculatePath(start.position, hit.position, filter, path)
                    || path.status != NavMeshPathStatus.PathComplete) continue;
                destination = hit.position;
                return true;
            }
            return false;
        }
    }
}
