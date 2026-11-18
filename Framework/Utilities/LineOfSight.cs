using UnityEngine;

namespace UnscriptedEngine
{
    public static class LineOfSight
    {
        /// <summary>Blocker layers should exclude the observer. Target children count as visible hits.</summary>
        public static bool IsClear(Vector3 origin, Vector3 destination, LayerMask blockers, Transform target = null)
        {
            return !Physics.Linecast(origin, destination, out RaycastHit hit, blockers, QueryTriggerInteraction.Ignore)
                || target != null && hit.transform.IsChildOf(target);
        }
    }
}
