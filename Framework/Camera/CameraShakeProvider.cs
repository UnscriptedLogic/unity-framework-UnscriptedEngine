using UnityEngine;

namespace UnscriptedEngine
{
    public sealed class CameraShakeRequest
    {
        public UController Controller { get; }
        public CameraShakeProfile Profile { get; }

        public CameraShakeRequest(UController controller, CameraShakeProfile profile)
        {
            Controller = controller;
            Profile = profile;
        }
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Unscripted Engine/Camera/Item Shake Provider")]
    public sealed class CameraShakeProvider : MonoBehaviour
    {
        [SerializeField, Tooltip("Empty uses the wielder's default item shake.")]
        private CameraShakeProfile profile;

        public void RequestShake()
        {
            if (!isActiveAndEnabled) return;
            var pawn = GetComponentInParent<UPawn>();
            if (pawn == null || pawn.Controller is not { IsOwner: true } controller ||
                controller.Pawn != pawn) return;

            EventBus.Publish(new CameraShakeRequest(controller, profile));
        }
    }
}
