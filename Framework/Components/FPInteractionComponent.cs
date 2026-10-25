using System;
using UnityEngine;

namespace UnscriptedEngine
{
    public class FPInteractionComponent : UObjectComponent, IInteractionTargetProvider
    {
        private Camera interactionCamera;
        [SerializeField, Min(0f)] private float interactionRange = 2f;
        [SerializeField] private LayerMask interactionLayers = ~0;
        [SerializeField] private QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal;

        private UInteractableComponent[] _buttonComponents = Array.Empty<UInteractableComponent>();
        private RaycastHit _hitInfo;
        private UInteractableComponent target;
        private bool hasTarget;

        // Legacy arrays materialize on demand and remain snapshots of this tick.
        public UInteractableComponent[] ButtonComponents => _buttonComponents ??= new[] { target };
        public bool HasAnyButtons => hasTarget;
        public UInteractableComponent FirstButton => _buttonComponents != null
            ? (_buttonComponents.Length > 0 ? _buttonComponents[0] : null) : target;
        public UInteractableComponent FocusedInteractable => FirstButton;
        public RaycastHit HitInfo => _hitInfo;

        protected override void Tick(float deltaTime)
        {
            base.Tick(deltaTime);

            Camera sourceCamera = ResolveCamera();
            if (sourceCamera == null)
            {
                ClearHit();
                return;
            }

            Ray ray = sourceCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (!Physics.Raycast(ray, out _hitInfo, interactionRange, interactionLayers, queryTriggerInteraction))
            {
                ClearHit();
                return;
            }

            UInteractableComponent button = _hitInfo.collider.GetComponentInParent<UInteractableComponent>();
            hasTarget = button != null && button.isActiveAndEnabled;
            target = hasTarget ? button : null;
            _buttonComponents = hasTarget ? null : Array.Empty<UInteractableComponent>();
        }

        private Camera ResolveCamera()
        {
            if (interactionCamera == null)
            {
                interactionCamera = Camera.main;
                return interactionCamera;
            }

            return Camera.main;
        }

        private void ClearHit()
        {
            _hitInfo = default;
            target = null;
            hasTarget = false;
            _buttonComponents = Array.Empty<UInteractableComponent>();
        }

        private void OnDrawGizmosSelected()
        {
            Camera sourceCamera = ResolveCamera();
            if (sourceCamera == null)
            {
                return;
            }

            Gizmos.color = Color.green;
            Ray ray = sourceCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            Gizmos.DrawRay(ray.origin, ray.direction * interactionRange);
        }
    }

}
