using Unity.Cinemachine;

namespace UnscriptedEngine
{
    public class MotionCameraExtension : CinemachineExtension
    {
        public MotionCameraEffect Source { get; set; }

        protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase camera,
            CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
        {
            if (stage == CinemachineCore.Stage.Finalize && Source != null)
                Source.Apply(ref state);
        }
    }
}
