using InteractionSystem;
using UnityEngine;
using static UnscriptedEngine.UObject;

public class PlayerInteractComponent : PlayerBaseComponent
{
    protected IUsesCamera cameraContext;

    [SerializeField] private float interactDistance = 10f;

    public Bindable<GameObject> CurrentlyLookingAt;
    public Bindable<Vector3> CurrentlyLookingPoint;
    public Bindable<RaycastHit> CurrentlyLookingHit;
    public Bindable<Vector3> CurrentlyLookingPointLimitless;

    public override void Initialize(P_PlayerPawn context)
    {
        cameraContext = context as IUsesCamera;

        base.Initialize(context);

        if (cameraContext == null)
        {
            Debug.LogError("PlayerInteractComponent requires a camera context to function properly.");
            return;
        }

        initialized = true;
    }

    public override void UpdateTick(out bool swallowTick)
    {
        swallowTick = false;

        if (!initialized) return;

        Vector3 cameraPos = cameraContext.CurrentCam.transform.position;
        Vector3 cameraForward = cameraContext.CurrentCam.transform.forward;

        GameObject lookingAtGO = null;
        Vector3 lookingAtPoint = cameraPos + cameraForward * interactDistance;

        Ray ray = new Ray(cameraPos + cameraForward * 0.5f, cameraForward);
        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance))
        {
            lookingAtGO = hit.collider.gameObject;
            lookingAtPoint = hit.point;

            CurrentlyLookingHit.Value = hit;
        }

        if (Physics.Raycast(ray, out RaycastHit limitlesshit))
        {
            CurrentlyLookingPointLimitless.Value = limitlesshit.point;
        }

        CurrentlyLookingAt.Value = lookingAtGO;
        CurrentlyLookingPoint.Value = lookingAtPoint;
    }

    public override void OnInteract(bool pressed, out bool swallowInput)
    {
        swallowInput = false;

        if (!pressed) return;

        if (CurrentlyLookingAt.Value == null) return;

        Interactable interactable = CurrentlyLookingAt.Value.GetComponent<Interactable>();
        if (interactable == null) return;

        interactable.OnInteract?.Invoke(gameObject);
    }

    public override void DeInitialize(P_PlayerPawn context)
    {
        base.DeInitialize(context);
    }
}
