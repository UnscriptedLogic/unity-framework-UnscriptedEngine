using System;
using System.Linq;
using Framework.Components;
using UnityEngine;

public class FPInteractionComponent : UObjectComponent
{
    private Camera interactionCamera;
    [SerializeField, Min(0f)] private float interactionRange = 2f;
    [SerializeField] private LayerMask interactionLayers = ~0;
    [SerializeField] private QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal;

    private UInteractableComponent[] _buttonComponents = Array.Empty<UInteractableComponent>();
    private RaycastHit _hitInfo;

    public UInteractableComponent[] ButtonComponents => _buttonComponents;
    public bool HasAnyButtons => _buttonComponents.Length > 0;
    public UInteractableComponent FirstButton => _buttonComponents.FirstOrDefault();
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
        _buttonComponents = button != null
            ? new[] { button }
            : Array.Empty<UInteractableComponent>();
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
