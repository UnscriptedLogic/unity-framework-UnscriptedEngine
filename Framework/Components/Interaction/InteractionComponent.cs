using System.Linq;
using UnityEngine;
using System;
using System.Collections.Generic;

namespace UnscriptedEngine
{
    public class InteractionComponent : UObjectComponent, IInteractionTargetProvider
    {
        [SerializeField] private float interactionRange = 2f;
        
        private UInteractableComponent[] _buttonComponents = System.Array.Empty<UInteractableComponent>();
        
        private Collider[] overlaps = new Collider[16];
        private readonly HashSet<UInteractableComponent> seen = new();
        private readonly List<Candidate> candidates = new();
        private struct Candidate
        {
            public UInteractableComponent Target;
            public float Distance;
            public int Order;
        }
        private static readonly Comparison<Candidate> CompareCandidates = (a, b) =>
        {
            int comparison = a.Distance.CompareTo(b.Distance);
            return comparison != 0 ? comparison : a.Order.CompareTo(b.Order);
        };
        public UInteractableComponent[] ButtonComponents
        {
            get
            {
                if (_buttonComponents == null)
                {
                    _buttonComponents = new UInteractableComponent[candidates.Count];
                    for (int i = 0; i < candidates.Count; i++) _buttonComponents[i] = candidates[i].Target;
                }
                return _buttonComponents;
            }
        }
        public bool HasAnyButtons => candidates.Count > 0;
        public UInteractableComponent FirstInteractable => _buttonComponents != null
            ? (_buttonComponents.Length > 0 ? _buttonComponents[0] : null)
            : (candidates.Count > 0 ? candidates[0].Target : null);
        public UInteractableComponent FocusedInteractable => FirstInteractable;
        
        protected override void Tick(float deltaTime)
        {
            base.Tick(deltaTime);
            
            //check in a sphere around you and see if there are any UButtonComponents
            Vector3 position = transform.position;
            int count;
            while ((count = Physics.OverlapSphereNonAlloc(position, interactionRange, overlaps)) == overlaps.Length)
                Array.Resize(ref overlaps, checked(overlaps.Length * 2));
            seen.Clear();
            candidates.Clear();
            for (int i = 0; i < count; i++)
            {
                var target = overlaps[i].GetComponentInParent<UInteractableComponent>();
                if (target != null && target.isActiveAndEnabled && seen.Add(target))
                    candidates.Add(new Candidate { Target = target,
                        Distance = (target.transform.position - position).sqrMagnitude, Order = i });
            }
            Array.Clear(overlaps, 0, overlaps.Length);
            seen.Clear();
            candidates.Sort(CompareCandidates);
            _buttonComponents = candidates.Count == 0 ? Array.Empty<UInteractableComponent>() : null;
        }
        
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, interactionRange);
        }
    }
}
