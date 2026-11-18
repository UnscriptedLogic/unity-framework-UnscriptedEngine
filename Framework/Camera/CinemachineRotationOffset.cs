using Unity.Cinemachine;
using UnityEngine;

namespace UnscriptedEngine
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Unscripted Engine/Camera/Cinemachine Rotation Offset")]
    public sealed class CinemachineRotationOffset : CinemachineExtension
    {
        [SerializeField, Tooltip("Offset pivot. Identity local rotation gives no correction.")]
        private Transform offset;
        [SerializeField, Tooltip("Optional independent rotation composed after the first offset.")]
        private Transform additionalOffset;

        protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase camera,
            CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
        {
            if (stage != CinemachineCore.Stage.Finalize) return;
            if (offset != null) state.OrientationCorrection *= offset.localRotation;
            if (additionalOffset != null) state.OrientationCorrection *= additionalOffset.localRotation;
        }
    }
}
