using UnityEngine;
using Unity.Netcode;
using UnscriptedEngine;

namespace Framework.Generators
{
    /// <summary>Releases the generated rigidbodies when the framework interaction completes.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class FractureInteraction : UInteractableComponent
    {
        [SerializeField] private RigidbodyReleaseGroup releaseGroup;
        [SerializeField, HideInInspector] private Rigidbody[] pieces = System.Array.Empty<Rigidbody>();
        private bool released;

        protected override void OnEnable()
        {
            base.OnEnable();
            OnButtonPressedServer += HandleInteraction;
        }

        protected override void OnDisable()
        {
            OnButtonPressedServer -= HandleInteraction;
            base.OnDisable();
        }

        protected override bool CanInteract(GameObject interactor) =>
            !released && (releaseGroup == null || releaseGroup.IsHolding) && base.CanInteract(interactor);

        /// <summary>Releases physics once. Gameplay code can also call this directly.</summary>
        public void Release()
        {
            if (!Application.isPlaying || released || (IsSpawned && !IsServer))
                return;

            if (releaseGroup != null)
            {
                releaseGroup.TryRelease();
                return;
            }

            // Bakes made before release groups stored the piece references on this component.
            released = true;
            RigidbodyReleaseGroup.ReleaseBodies(pieces);
        }

        private void HandleInteraction(UInteractArgs args) => Release();
    }
}
