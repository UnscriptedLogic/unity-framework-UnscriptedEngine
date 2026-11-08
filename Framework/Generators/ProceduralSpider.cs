using System;
using UnityEngine;

namespace Framework.Components
{
    /// <summary>Schedules independent legs. Never moves or rotates the body.</summary>
    [DisallowMultipleComponent]
    public sealed class ProceduralSpider : MonoBehaviour
    {
        [Tooltip("Legs directed by this brain. Populate with Collect Child Legs after adding custom legs.")]
        [SerializeField] private ProceduralSpiderLeg[] legs = Array.Empty<ProceduralSpiderLeg>();
        [Tooltip("Maximum simultaneous steps. One gives a stable crawl; larger values allow faster gaits."), Min(1)]
        [SerializeField] private int maximumSteppingLegs = 2;
        [Tooltip("Seconds between footing checks. Animation and IK still run every frame; probes are throttled to limit physics cost."), Min(0f)]
        [SerializeField] private float stepCheckInterval = 0.05f;
        private int nextLeg;
        private float checkTimer;

        public System.Collections.Generic.IReadOnlyList<ProceduralSpiderLeg> Legs => legs;

        private void OnEnable() { nextLeg = 0; checkTimer = 0f; }

        [ContextMenu("Collect Child Legs")]
        public void CollectChildLegs()
        {
            ProceduralSpiderLeg[] candidates = GetComponentsInChildren<ProceduralSpiderLeg>(true);
            System.Collections.Generic.List<ProceduralSpiderLeg> owned = new System.Collections.Generic.List<ProceduralSpiderLeg>();
            foreach (ProceduralSpiderLeg leg in candidates)
            {
                if (leg.GetComponentInParent<ProceduralSpider>() == this) owned.Add(leg);
            }
            legs = owned.ToArray();
            nextLeg = 0;
        }

        private void LateUpdate()
        {
            if (legs == null || legs.Length == 0) return;
            int stepping = 0;
            for (int index = 0; index < legs.Length; index++)
            {
                ProceduralSpiderLeg leg = legs[index];
                if (leg == null || !leg.isActiveAndEnabled) continue;
                leg.Tick(Time.deltaTime);
                if (leg.IsStepping) stepping++;
            }
            checkTimer -= Time.deltaTime;
            if (checkTimer > 0f) return;
            checkTimer = Mathf.Max(0f, stepCheckInterval);
            int start = nextLeg % legs.Length;
            for (int offset = 0; offset < legs.Length && stepping < Mathf.Max(1, maximumSteppingLegs); offset++)
            {
                int index = (start + offset) % legs.Length;
                ProceduralSpiderLeg leg = legs[index];
                if (leg == null || !leg.isActiveAndEnabled || !leg.TryBeginStep()) continue;
                stepping++;
                nextLeg = (index + 1) % legs.Length;
            }
        }
    }
}
