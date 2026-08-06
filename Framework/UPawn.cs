using Unity.Netcode;

namespace Framework
{
    public abstract class UPawn : UObject
    {
        private readonly NetworkVariable<NetworkObjectReference> controllerReference =
            new NetworkVariable<NetworkObjectReference>(
                new NetworkObjectReference((NetworkObject)null),
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        public UController Controller { get; private set; }

        public bool IsPossessed => Controller != null;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            controllerReference.OnValueChanged += HandleControllerReferenceChanged;
            ApplyControllerReference(controllerReference.Value);
        }

        public override void OnNetworkDespawn()
        {
            controllerReference.OnValueChanged -= HandleControllerReferenceChanged;
            base.OnNetworkDespawn();
        }

        public virtual void DetachFromController()
        {
            if (IsServer)
            {
                Controller?.UnPossess();
            }
        }

        public override void OnDestroy()
        {
            DetachFromController();
            base.OnDestroy();
        }

        protected virtual void OnPossessed(UController newController)
        {
        }

        protected virtual void OnUnPossessed(UController oldController)
        {
        }

        internal void SetControllerFromPossession(UController controller)
        {
            if (Controller == controller)
            {
                return;
            }

            UController previousController = Controller;
            Controller = controller;

            if (previousController != null)
            {
                OnUnPossessed(previousController);
            }

            if (Controller != null)
            {
                OnPossessed(Controller);
            }
        }

        internal void SetControllerReferenceFromPossession(UController controller)
        {
            if (!IsServer)
            {
                return;
            }

            controllerReference.Value = controller != null
                ? controller.NetworkObject
                : new NetworkObjectReference((NetworkObject)null);
        }

        private void HandleControllerReferenceChanged(NetworkObjectReference previousReference, NetworkObjectReference newReference)
        {
            ApplyControllerReference(newReference);
        }

        private void ApplyControllerReference(NetworkObjectReference reference)
        {
            if (reference.TryGet(out NetworkObject controllerObject) && controllerObject.TryGetComponent(out UController controller))
            {
                SetControllerFromPossession(controller);
                return;
            }

            SetControllerFromPossession(null);
        }
    }
}
