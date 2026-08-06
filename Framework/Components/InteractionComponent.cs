using System.Linq;
using UnityEngine;

namespace Framework.Components
{
    public class InteractionComponent : UObjectComponent
    {
        [SerializeField] private float interactionRange = 2f;
        
        private UButtonComponent[] _buttonComponents;
        
        public UButtonComponent[] ButtonComponents => _buttonComponents;
        public bool HasAnyButtons => _buttonComponents.Length > 0;
        public UButtonComponent FirstButton => _buttonComponents.FirstOrDefault();
        
        protected override void Tick(float deltaTime)
        {
            base.Tick(deltaTime);
            
            //check in a sphere around you and see if there are any UButtonComponents
            Collider[] colliders = Physics.OverlapSphere(transform.position, interactionRange);
            _buttonComponents = colliders.Select(c => c.GetComponent<UButtonComponent>()).Where(b => b != null).ToArray();
        }
        
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, interactionRange);
        }
    }
}