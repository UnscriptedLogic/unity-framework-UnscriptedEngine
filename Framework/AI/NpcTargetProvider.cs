using UnityEngine;

namespace UnscriptedEngine
{
    /// <summary>Replace this component to supply patrol points or game-specific destinations.</summary>
    public abstract class NpcTargetProvider : MonoBehaviour
    {
        /// <summary>Continue a route without the normal idle interval after a successful move.</summary>
        public virtual bool ContinueAfterArrival => false;

        public virtual void MovementFinished(NpcActionStatus status) { }

        public abstract bool TryGetDestination(Vector3 home, Vector3 current,
            UnityEngine.AI.NavMeshAgent agent, out Vector3 destination);
    }
}
