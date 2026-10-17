using System;
using UnityEngine;
using Unity.Cinemachine;
namespace UnscriptedEngine
{
    /// <summary>Camera mode and look state, independent of input bindings and game-specific pawns.</summary>
    public sealed class PawnViewRig
    {
        private readonly Transform transform, anchor;
        private readonly CinemachineCamera firstPersonCamera, thirdPersonCamera, thirdPersonCameraAim;
        private readonly CinemachineOrbitalFollow _thirdPersonOrbit;
        private readonly CinemachineInputAxisController _orbitInput;
        private readonly Func<bool> rotateBody;
        private float _cameraPitch, _seatedCameraYaw;
        private bool _isFirstPerson, _isThirdPersonAiming;
        private bool inputBlocked;
        public float mouseSensitivity, gamepadLookSpeed, maxLookAngle;
        public bool IsFirstPerson => _isFirstPerson;
        public bool IsAiming => _isThirdPersonAiming;
        public void SetInputBlocked(bool blocked)
        {
            inputBlocked = blocked;
            UpdateCameraMode();
        }
        private bool RotateBodyWithLook => rotateBody();
        public PawnViewRig(Transform body, Transform cameraAnchor, CinemachineCamera first, CinemachineCamera third,
            CinemachineCamera aim, Func<bool> shouldRotateBody, float pointerSensitivity, float stickSpeed, float pitchLimit)
        {
            transform = body; anchor = cameraAnchor;
            firstPersonCamera = first; thirdPersonCamera = third; thirdPersonCameraAim = aim;
            rotateBody = shouldRotateBody;
            mouseSensitivity = pointerSensitivity; gamepadLookSpeed = stickSpeed; maxLookAngle = pitchLimit;
            _thirdPersonOrbit = third.GetComponent<CinemachineOrbitalFollow>();
            _orbitInput = third.GetComponent<CinemachineInputAxisController>();
            aim.BlendHint |= CinemachineCore.BlendHints.FreezeWhenBlendingOut;
        }
        public void SetFirstPerson()
    {
        Quaternion rotation = GetActiveCameraRotation();
        _isFirstPerson = true;
        ApplyLookRotation(rotation);
        UpdateCameraMode();
    }

public void SetThirdPerson()
    {
        Quaternion rotation = GetActiveCameraRotation();
        _isFirstPerson = false;
        if (!_isThirdPersonAiming)
            AlignThirdPersonOrbit(rotation);
        UpdateCameraMode();
    }

public Quaternion GetActiveCameraRotation()
    {
        CinemachineCamera camera = _isThirdPersonAiming
            ? thirdPersonCameraAim
            : (_isFirstPerson ? firstPersonCamera : thirdPersonCamera);
        return camera.transform.rotation;
    }

public void UpdateCameraMode()
    {
        firstPersonCamera.Priority.Value = _isFirstPerson && !_isThirdPersonAiming ? 1 : 0;
        thirdPersonCamera.Priority.Value = !_isFirstPerson && !_isThirdPersonAiming ? 1 : 0;
        thirdPersonCameraAim.Priority.Value = _isThirdPersonAiming ? 10 : 0;

        if (_orbitInput != null)
                _orbitInput.enabled = !inputBlocked && !_isFirstPerson && !_isThirdPersonAiming;
    }

public void ApplyLookRotation(Quaternion rotation)
    {
        Vector3 forward = rotation * Vector3.forward;
        Vector3 flatForward = Vector3.ProjectOnPlane(forward, Vector3.up);
        if (RotateBodyWithLook && flatForward.sqrMagnitude > Mathf.Epsilon)
            transform.rotation = Quaternion.LookRotation(flatForward, Vector3.up);

        _cameraPitch = Mathf.Clamp(
            -Mathf.Asin(Mathf.Clamp(forward.y, -1f, 1f)) * Mathf.Rad2Deg,
            -maxLookAngle, maxLookAngle);
        _seatedCameraYaw = RotateBodyWithLook ? 0f
            : Mathf.DeltaAngle(transform.eulerAngles.y, rotation.eulerAngles.y);
        anchor.rotation =
            transform.rotation * Quaternion.Euler(_cameraPitch, _seatedCameraYaw, 0f);
    }

public void AlignThirdPersonOrbit(Quaternion rotation)
    {
        Vector3 position = thirdPersonCamera.transform.position;
        if (_thirdPersonOrbit != null && thirdPersonCamera.Follow != null)
        {
            Transform follow = thirdPersonCamera.Follow;
            bool worldSpaceSphere = _thirdPersonOrbit.TrackerSettings.BindingMode ==
                Unity.Cinemachine.TargetTracking.BindingMode.WorldSpace &&
                _thirdPersonOrbit.OrbitStyle == CinemachineOrbitalFollow.OrbitStyles.Sphere;
            Vector3 center = follow.position + (worldSpaceSphere
                ? _thirdPersonOrbit.TargetOffset : follow.rotation * _thirdPersonOrbit.TargetOffset);
            Vector3 forward = rotation * Vector3.forward;
            float radius = _thirdPersonOrbit.Radius * _thirdPersonOrbit.RadialAxis.Value;
            Vector3 lookAt = center;
            var hardLookAt = thirdPersonCamera.GetComponent<CinemachineHardLookAt>();
            if (hardLookAt != null && thirdPersonCamera.LookAt != null)
                lookAt = thirdPersonCamera.LookAt.TransformPoint(hardLookAt.LookAtOffset);

            // Intersect the desired look ray with the orbit sphere.  The look-at
            // point and orbit center differ in the default pawn's camera rig.
            Vector3 offset = lookAt - center;
            float projection = Vector3.Dot(offset, forward);
            float distance = projection + Mathf.Sqrt(Mathf.Max(0f,
                projection * projection + radius * radius - offset.sqrMagnitude));
            position = lookAt - forward * distance;

            if (worldSpaceSphere)
            {
                // ForceCameraPosition preserves the old camera-to-target offset.
                // Solve the default world-space sphere directly so its different
                // look-at point cannot reintroduce the old pitch on the next frame.
                Vector3 angles = Quaternion.LookRotation(center - position, Vector3.up).eulerAngles;
                float pitch = Mathf.DeltaAngle(0f, angles.x);
                _thirdPersonOrbit.HorizontalAxis.Value =
                    _thirdPersonOrbit.HorizontalAxis.ClampValue(Mathf.DeltaAngle(0f, angles.y));
                // Aim can reach beyond the free orbit's original vertical limits.
                // Include the handoff angle instead of snapping at those limits.
                _thirdPersonOrbit.VerticalAxis.Range = new Vector2(
                    Mathf.Min(_thirdPersonOrbit.VerticalAxis.Range.x, pitch),
                    Mathf.Max(_thirdPersonOrbit.VerticalAxis.Range.y, pitch));
                _thirdPersonOrbit.VerticalAxis.Value = pitch;
                thirdPersonCamera.PreviousStateIsValid = false;
                thirdPersonCamera.InternalUpdateCameraState(Vector3.up, -1f);
                return;
            }
        }

        thirdPersonCamera.PreviousStateIsValid = false;
        // Updates Cinemachine's orbit axes, not just its temporary transform.
        thirdPersonCamera.ForceCameraPosition(position, rotation);
        thirdPersonCamera.InternalUpdateCameraState(Vector3.up, -1f);
    }

public void HandleLookInput(Vector2 input, bool isPointerDelta)
    {
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            return;
        }

        Vector2 lookDelta = isPointerDelta
            ? input * mouseSensitivity
            : input * (gamepadLookSpeed * Time.deltaTime);

        if (!_isFirstPerson && !_isThirdPersonAiming)
            return; // Cinemachine owns free-orbit input and pitch.

        _cameraPitch = Mathf.Clamp(_cameraPitch - lookDelta.y, -maxLookAngle, maxLookAngle);

        if (!RotateBodyWithLook)
        {

            anchor.rotation = Quaternion.Euler(_cameraPitch, anchor.eulerAngles.y + lookDelta.x, 0f);
            return;
        }

        if (_isFirstPerson || _isThirdPersonAiming)
        {
            transform.Rotate(Vector3.up, lookDelta.x, Space.World);
        }

        _seatedCameraYaw = 0f;

        if (anchor != null)
        {
            anchor.localRotation =
                Quaternion.Euler(_cameraPitch, _seatedCameraYaw, 0f);
        }
    }

public void ToggleThirdPersonAim(bool isEnabled)
    {
        if (_isThirdPersonAiming == isEnabled)
            return;

        Quaternion rotation = GetActiveCameraRotation();
        _isThirdPersonAiming = isEnabled;
        if (isEnabled)
        {
            ApplyLookRotation(rotation);
            thirdPersonCameraAim.PreviousStateIsValid = false;
            thirdPersonCameraAim.InternalUpdateCameraState(Vector3.up, -1f);
        }
        else if (_isFirstPerson)
        {
            ApplyLookRotation(rotation);
        }
        else
        {
            AlignThirdPersonOrbit(rotation);
        }
        UpdateCameraMode();
    }
    }
}
