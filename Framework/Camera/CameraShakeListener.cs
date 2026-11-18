using UnityEngine;

namespace UnscriptedEngine
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Unscripted Engine/Camera/Item Shake Listener")]
    public sealed class CameraShakeListener : MonoBehaviour
    {
        [SerializeField] private UPawn pawn;
        [SerializeField] private ObjectAnimBrain brain;
        [SerializeField] private ObjectAnimRotate rotation;

        private float defaultDuration;
        private Vector3 defaultRotation;
        private AnimationCurve defaultEnvelope;

        private void Awake()
        {
            if (pawn == null || brain == null || rotation == null)
            {
                enabled = false;
                return;
            }

            defaultDuration = brain.Duration;
            defaultRotation = rotation.Degrees;
            defaultEnvelope = rotation.Curve;
        }

        private void OnEnable() => EventBus.Subscribe<CameraShakeRequest>(HandleRequest);

        private void OnDisable()
        {
            EventBus.Unsubscribe<CameraShakeRequest>(HandleRequest);
            if (brain != null) brain.ResetPose();
        }

        private void HandleRequest(CameraShakeRequest request)
        {
            if (request.Controller == null || !request.Controller.IsOwner ||
                request.Controller != pawn.Controller || request.Controller.Pawn != pawn) return;

            CameraShakeProfile profile = request.Profile;
            brain.Duration = profile != null ? profile.Duration : defaultDuration;
            rotation.Degrees = profile != null ? profile.RotationDegrees : defaultRotation;
            rotation.Curve = profile != null ? profile.Envelope : defaultEnvelope;
            brain.Play();
        }
    }
}
