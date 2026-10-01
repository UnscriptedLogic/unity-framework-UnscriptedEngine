using System;
using Framework;
using Framework.Components;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

namespace UnscriptedEngine
{
    public class UInteractableComponent : UObjectComponent
    {
        public event Action<UInteractArgs> OnButtonPressedServer;
        public event Action<UInteractArgs> OnButtonPressedClient;

        /*
     * The whole reason we are separating the "with source and without source"
     * is for prototyping, debugging and quick access to testing.
     * Most of the time we'd just want to wire up to empty functions.
     * The interaction with source is for those actual intended implementations
     */

        [Header("Button Events")] 
        [SerializeField] private UnityEvent onButtonPressedServer;
        [SerializeField] private UnityEvent onButtonPressedClient;
        [SerializeField] private UnityEvent onButtonInteractedLocally;

        [Header("Button Events With Source")] 
        [SerializeField] private UnityEvent<GameObject> onButtonPressedServerWithSource;
        [SerializeField] private UnityEvent<GameObject> onButtonPressedClientWithSource;
        [SerializeField] private UnityEvent<GameObject> onButtonInteractedLocallyWithSource;

        public void Interact(GameObject interactorSource = null)
        {
            onButtonInteractedLocally?.Invoke();
            onButtonInteractedLocallyWithSource?.Invoke(interactorSource);

            if (IsSpawned)
            {
                InteractServerRpc(CreateInteractorReference(interactorSource));
            }
        }

        public void InteractClient(GameObject interactorSource = null)
        {
            InteractClientRpc(CreateInteractorReference(interactorSource));
        }

        public void InteractServer(GameObject interactorSource = null)
        {
            InteractServerRpc(CreateInteractorReference(interactorSource));
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Server)]
        private void InteractServerRpc(NetworkObjectReference interactorReference)
        {
            GameObject interactorSource = ResolveInteractorSource(interactorReference);
            InteractClientRpc(interactorReference);

            OnButtonPressedServer?.Invoke(new UInteractArgs()
            {
                Interactable = this,
                InteractorSource = interactorSource
            });

            onButtonPressedServer?.Invoke();
            onButtonPressedServerWithSource?.Invoke(interactorSource);
        }

        [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
        private void InteractClientRpc(NetworkObjectReference interactorReference)
        {
            GameObject interactorSource = ResolveInteractorSource(interactorReference);

            OnButtonPressedClient?.Invoke(new UInteractArgs()
            {
                Interactable = this,
                InteractorSource = interactorSource
            });

            onButtonPressedClient?.Invoke();
            onButtonPressedClientWithSource?.Invoke(interactorSource);
        }

        private static NetworkObjectReference CreateInteractorReference(GameObject interactorSource)
        {
            NetworkObject interactorNetworkObject = interactorSource != null
                ? interactorSource.GetComponentInParent<NetworkObject>()
                : null;

            return interactorNetworkObject != null
                ? new NetworkObjectReference(interactorNetworkObject)
                : new NetworkObjectReference((NetworkObject)null);
        }

        private static GameObject ResolveInteractorSource(NetworkObjectReference interactorReference)
        {
            return interactorReference.TryGet(out NetworkObject interactorNetworkObject) &&
                   interactorNetworkObject != null
                ? interactorNetworkObject.gameObject
                : null;
        }

        public class UInteractArgs : EventArgs
        {
            public GameObject InteractorSource;
            public UInteractableComponent Interactable;
        }
    }

}