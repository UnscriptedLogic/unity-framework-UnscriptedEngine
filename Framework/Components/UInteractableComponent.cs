using System;
using Framework;
using Framework.Components;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

public class UInteractableComponent : UObjectComponent
{
    public event Action<UInteractableComponent> OnButtonPressedServer;
    public event Action<UInteractableComponent> OnButtonPressedClient;

    [Header("Button Events")]
    [SerializeField] private UnityEvent onButtonPressedServer;
    [SerializeField] private UnityEvent onButtonPressedClient;
    [SerializeField] private UnityEvent onButtonInteractedLocally;
    
    public void Interact()
    {
        onButtonInteractedLocally?.Invoke();

        if (IsSpawned)
        {
            InteractServerRpc();
        }
    }
    
    public void InteractClient()
    {
        InteractClientRpc();
    }
    
    public void InteractServer()
    {
        InteractServerRpc();
    }
    
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Server)]
    private void InteractServerRpc()
    {
        InteractClientRpc();
        
        OnButtonPressedServer?.Invoke(this);
        onButtonPressedServer?.Invoke();
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void InteractClientRpc()
    {
        OnButtonPressedClient?.Invoke(this);
        onButtonPressedClient?.Invoke();
    }
}
