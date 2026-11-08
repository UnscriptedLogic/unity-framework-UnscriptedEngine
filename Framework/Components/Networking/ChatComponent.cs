using System;
using Unity.Netcode;
using UnityEngine;

namespace UnscriptedEngine
{
    public class ChatComponent : UObjectComponent
    {
        public event Action<string> OnChatMessageReceived;

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void SendChatMessageServerRpc(string message)
        {
            BroadcastChatMessageClientRpc(message);
        }

        [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
        public void BroadcastChatMessageClientRpc(string message)
        {
            Debug.Log($"[Chat] {message}", gameObject);

            OnChatMessageReceived?.Invoke(message);
        }
    }

}