using System;
using Unity.VisualScripting;
using UnityEngine;

public class PlayerCameraAnimations : PlayerBaseComponent
{
    private IMovingPawn moveableContext;

    //[SerializeField] private CinemachineInputAxisController axisController;
    //[SerializeField] private CinemachineCamera cinemachineCamera;
    //[SerializeField] private CinemachineCameraOffset camOffset;

    //[Header("Head Bob")]
    //[SerializeField] private float amplitude;
    //[SerializeField] private float frequency;

    //[Header("Head Wiggle")]
    //[SerializeField] private float wiggleAmplitude;
    //[SerializeField] private float wiggleFrequency;

    //[Header("Head Tilt")]
    //[SerializeField] private float angle = 10f;
    //[SerializeField] private float tiltLerp = 10f;

    //[Header("Dyamic FOV")]
    //[SerializeField] private float fovMultiplier = 10f;
    //[SerializeField] private float fovLerp = 5f;
    //private float orignialFOV;

    //private Camera mainCamera;

    //public Camera CurrentCam => mainCamera;

    //public override void Initialize(P_PlayerPawn context)
    //{
    //    base.Initialize(context);

    //    cinemachineCamera.Priority.Value = 10;

    //    moveableContext = context as IMovingPawn;

    //    mainCamera = Camera.main;
    //    cinemachineCamera.transform.SetParent(null);

    //    orignialFOV = cinemachineCamera.Lens.FieldOfView;

    //    if (moveableContext == null) return;

    //    initialized = true;
    
    //    MonoBehaviourExtensions.OnToggledMouse += OnToggledMouse;
    //}

    //private void OnToggledMouse(bool value)
    //{
    //    axisController.enabled = !value;
    //}

    //public override void UpdateTick(out bool swallowTick)
    //{
    //    swallowTick = false;

    //    if (!initialized) return;

    //    Quaternion quaternion = Quaternion.Euler(0, mainCamera.transform.rotation.eulerAngles.y, 0);
    //    context.transform.rotation = quaternion;

    //    if (moveableContext.IsMoving)
    //    {
    //        //head bob
    //        camOffset.Offset.y = Mathf.Sin(Time.time * frequency) * amplitude;
    //        camOffset.Offset.x = Mathf.Cos(Time.time * wiggleFrequency) * wiggleAmplitude;

    //        //head tilt
    //        cinemachineCamera.Lens.Dutch = Mathf.Lerp(cinemachineCamera.Lens.Dutch, -moveableContext.MoveSettings.InputDir.x * angle, Time.deltaTime * tiltLerp);
    //    }
    //    else
    //    {
    //        //reset head bob
    //        camOffset.Offset.y = Mathf.Lerp(camOffset.Offset.y, 0, Time.deltaTime * 5);
    //        camOffset.Offset.x = Mathf.Lerp(camOffset.Offset.x, 0, Time.deltaTime * 5);

    //        //reset head tilt
    //        cinemachineCamera.Lens.Dutch = Mathf.Lerp(cinemachineCamera.Lens.Dutch, 0, Time.deltaTime * tiltLerp);
    //    }

    //    DynamicFOV();
    //}

    //private void DynamicFOV()
    //{
    //    float speed = moveableContext.MoveSettings.speed;
    //    float newFOV = Mathf.Lerp(cinemachineCamera.Lens.FieldOfView, orignialFOV + speed * fovMultiplier, Time.deltaTime * fovLerp);
    //    newFOV = Mathf.Clamp(newFOV, orignialFOV, 90f);
    //    cinemachineCamera.Lens.FieldOfView = newFOV;
    //}

    //public override void DeInitialize(P_PlayerPawn context)
    //{
    //    MonoBehaviourExtensions.OnToggledMouse -= OnToggledMouse;

    //    cinemachineCamera.Priority.Value = -1;

    //    base.DeInitialize(context);
    //}
}
