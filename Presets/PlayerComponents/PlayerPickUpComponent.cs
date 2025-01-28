using DG.Tweening;
using InteractionSystem;
using System.Collections.Generic;
using UnityEngine;
using static UnscriptedEngine.UObject;

public class PlayerPickUpComponent : PlayerBaseComponent
{
    private IUsesCamera cameraContext;

    [Header("Telekenisis")]
    [SerializeField, Tooltip("Whether or not to allow objects without a Rigidbody to be picked up by this system")] 
    private bool allowNonRigidbody = false;
    [SerializeField] private float holdDistance = 2f;
    [SerializeField] private float rotationForce;
    [SerializeField] private float pickupForce = 60;
    [SerializeField] private float lerpTime = 20f;
    [SerializeField] private float stamina = 100f;
    [SerializeField] private float restoreStaminaSpeed = 10f;
    [SerializeField] private float weightFactor;
    [SerializeField, Tooltip("The weight factor of the player if attempting to object surf")] private float playerFactor;

    public Bindable<float> currentStamina;
    private bool isKinematic;

    [Header("Pick Up")]
    [SerializeField] private Transform holdParent;
    private Holdable holdable;

    private bool shiftPressed;
    private bool isAltInteracting;

    private Rigidbody rb;
    private bool usingRB;
    private GameObject heldObject;
    private HashSet<Rigidbody> connectedRigidbodies;

    private Vector3 holdParentOffset;

    private void Awake()
    {
        holdParentOffset = holdParent.localPosition;
    }

    public override void Initialize(P_PlayerPawn context)
    {
        base.Initialize(context);

        cameraContext = context as IUsesCamera;

        holdParent.parent = Camera.main.transform;
        holdParent.localPosition = holdParentOffset;
        holdParent.localRotation = Quaternion.identity;
        currentStamina = new Bindable<float>(stamina);
        currentStamina.Value = stamina;
        InputPriority.ShiftPriority = 1;

        if (cameraContext == null)
        {
            Debug.LogError("PlayerPickUpComponent requires a camera context to function");
            return;
        }

        initialized = true;
    }

    public override void OnDefaultLeftMouseDown(out bool swallowInput)
    {
        StartTelekenisis(cameraContext.CurrentlyLookingAt.Value);
        swallowInput = heldObject != null;
    }

    public override void OnDefaultLeftMouseUp(out bool swallowInput)
    {
        EndTelekenisis();
        swallowInput = heldObject != null;
    }

    public void StartTelekenisis(GameObject target)
    {
        if (!initialized) return;

        if (holdParent.childCount > 0)
        {
            holdable.Interact();
            return;
        }

        if (target == null) return;
        if (target.GetComponent<UnInteractable>()) return;

        rb = target.GetComponent<Rigidbody>();

        if (!allowNonRigidbody) {
            if (rb == null) return;
        };

        usingRB = rb != null;
        heldObject = target;

        if (usingRB)
        {
            isKinematic = rb.isKinematic;
            rb.isKinematic = false;
        }


        holdDistance = Vector3.Distance(cameraContext.CurrentCam.transform.position, target.transform.position);
        target.transform.rotation = Quaternion.identity;

        TelekenisisListener listener = target.GetComponent<TelekenisisListener>();
        if (listener != null)
        {
            listener.OnTelekenisisStarted(context.gameObject);
        }
    }

    public void AttemptPickUp(GameObject target, bool keyDown)
    {
        if (!initialized) return;
        if (target == null) return;

        if (keyDown)
        {
            if (holdParent.childCount > 0)
            {
                LetGo();
                return;
            }

            holdable = target.GetComponent<Holdable>();
            if (holdable == null) return;

            PickUp(holdable);
        }
    }

    private void PickUp(Holdable holdable)
    {
        if (!initialized) return;
        if (holdable == null) return;

        holdable.OnHold(context.gameObject);

        holdable.transform.SetParent(holdParent);
        holdable.transform.localPosition = Vector3.zero;
        holdable.transform.localRotation = Quaternion.Euler(Vector3.zero);
        holdable.transform.DOLocalMove(Vector3.zero, 0.1f).SetEase(Ease.OutCubic);
        isKinematic = holdable.GetComponent<Rigidbody>().isKinematic;
        holdable.GetComponent<Rigidbody>().isKinematic = true;
    }

    private void LetGo()
    {
        if (!initialized) return;

        GameObject holdableGO = holdParent.GetChild(0).gameObject;
        holdableGO.transform.SetParent(null);
        holdableGO.transform.SetPositionAndRotation(cameraContext.CurrentlyLookingPoint.Value, Quaternion.Euler(Vector3.zero));
        holdable.GetComponent<Rigidbody>().isKinematic = isKinematic;

        holdable.OnLetGo(context.gameObject);
    }

    public override void UpdateTick(out bool swallowTick)
    {
        swallowTick = false;

        if (!initialized) return;

        if (!heldObject) return;
        if (usingRB) return;

        DistanceCheck();

        if (heldObject == null)
        {
            EndTelekenisis();
            return;
        }

        Vector3 targetPos = GetTargetPosition();
        heldObject.transform.position = Vector3.Lerp(heldObject.transform.position, targetPos, Time.deltaTime * lerpTime);
        if (isAltInteracting)
        {
            heldObject.transform.forward = Vector3.Lerp(heldObject.transform.forward, cameraContext.CurrentCam.transform.forward, Time.deltaTime * rotationForce);
        }
    }

    private void Update()
    {
        if (!initialized) return;

        currentStamina.Value = Mathf.Clamp(currentStamina.Value, 0f, stamina);
        currentStamina.Value += Time.fixedDeltaTime * restoreStaminaSpeed;
    }

    public override void FixedUpdateTick(out bool swallowTick)
    {
        swallowTick = false;

        if (!initialized) return;
        if (!heldObject) return;
        if (!usingRB) return;

        connectedRigidbodies = rb.GetConnectedObjectsByBounds(0.5f);

        //deduct stamina based on the weight of all connected objects
        float weight = 0f;
        foreach (Rigidbody rb in connectedRigidbodies)
        {
            if (rb == context.Rb)
            {
                weight += rb.mass * playerFactor;
                continue;
            }

            weight += rb.mass * weightFactor;
        }

        currentStamina.Value -= weight * Time.fixedDeltaTime;

        DistanceCheck();

        Vector3 targetPos = GetTargetPosition();
        rb.velocity = (targetPos - heldObject.transform.position) * pickupForce;

        if (isAltInteracting)
        {
            rb.transform.forward = Vector3.Lerp(rb.transform.forward, cameraContext.CurrentCam.transform.forward, Time.deltaTime * rotationForce);
        }

        if (currentStamina.Value <= 0f)
        {
            EndTelekenisis();
            return;
        }
    }

    public void EndTelekenisis()
    {
        if (!initialized) return;
        if (heldObject == null) return;

        if (usingRB)
        {
            rb.isKinematic = isKinematic;
        }

        TelekenisisListener listener = heldObject.GetComponent<TelekenisisListener>();
        if (listener != null)
        {
            listener.OnTelekenisisEnded(context.gameObject);
        }

        heldObject = null;
        rb = null;
        usingRB = false;
    }

    public override void OnShift(bool pressed, out bool swallowInput)
    {
        shiftPressed = pressed;
        swallowInput = heldObject != null;
    }

    private void DistanceCheck()
    {
        float distance = Vector3.Distance(cameraContext.CurrentCam.transform.position, heldObject.transform.position);
        if (distance > 11f)
        {
            EndTelekenisis();
        }
    }

    private Vector3 GetTargetPosition()
    {
        Vector3 targetPosition = cameraContext.CurrentCam.transform.position + cameraContext.CurrentCam.transform.forward * holdDistance;

        if (shiftPressed)
        {
            //lock position to a grid
            targetPosition.x = Mathf.Round(targetPosition.x);
            targetPosition.y = Mathf.Round(targetPosition.y);
            targetPosition.z = Mathf.Round(targetPosition.z); 
        }

        return targetPosition;
    }

    public void AlternateInteractStart()
    {
        isAltInteracting = true;

        if (holdable == null) return;

        holdable.AlternateInteractStart();
    }

    public void AlternateInteractEnd()
    {
        isAltInteracting = false;

        if (holdable == null) return;

        holdable.AlternateInteractEnd();
    }
}
