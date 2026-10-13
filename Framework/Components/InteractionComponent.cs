using System.Linq;
using UnityEngine;

namespace UnscriptedEngine
{
    public class InteractionComponent : UObjectComponent, IInteractionTargetProvider
    {
        [SerializeField] private float interactionRange = 2f;
        
        private UInteractableComponent[] _buttonComponents = System.Array.Empty<UInteractableComponent>();
        
        public UInteractableComponent[] ButtonComponents => _buttonComponents;
        public bool HasAnyButtons => _buttonComponents.Length > 0;
        public UInteractableComponent FirstInteractable => _buttonComponents.FirstOrDefault();
        public UInteractableComponent FocusedInteractable => FirstInteractable;
        
        protected override void Tick(float deltaTime)
        {
            base.Tick(deltaTime);
            
            //check in a sphere around you and see if there are any UButtonComponents
            Collider[] colliders = Physics.OverlapSphere(transform.position, interactionRange);
            _buttonComponents = colliders.Select(c => c.GetComponentInParent<UInteractableComponent>())
                .Where(b => b != null && b.isActiveAndEnabled).Distinct()
                .OrderBy(b => (b.transform.position - transform.position).sqrMagnitude).ToArray();
        }
        
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, interactionRange);
        }
    }
}
