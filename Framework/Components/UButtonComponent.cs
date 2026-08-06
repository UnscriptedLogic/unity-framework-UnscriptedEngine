using System;
using Framework;
using Framework.Components;
using Unity.Netcode;
using UnityEngine;

public class UButtonComponent : UObjectComponent
{
    public event Action<UButtonComponent> OnButtonPressedServer;
    public event Action<UButtonComponent> OnButtonPressedClient;
    
    public void Interact()
    {
        InteractServerRpc();
    }
    
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Server)]
    private void InteractServerRpc()
    {
        InteractClientRpc();
        
        OnButtonPressedServer?.Invoke(this);
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void InteractClientRpc()
    {
        OnButtonPressedClient?.Invoke(this);
    }
}
