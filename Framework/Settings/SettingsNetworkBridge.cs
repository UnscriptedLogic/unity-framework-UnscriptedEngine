using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace UnscriptedEngine.Settings
{
    /// <summary>One server-owned, session-persistent bridge. Schema providers do not need to be enabled.</summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class SettingsNetworkBridge : NetworkBehaviour
    {
        [SerializeField, Tooltip("Providers declaring the complete server-setting schema, including catalogs used by later scenes. Use disabled provider components on a persistent object for schema only. Matching catalogs must exist on every peer. Reading these references does not activate settings.")]
        private SettingsProvider[] catalogProviders = Array.Empty<SettingsProvider>();
        private readonly Dictionary<string, SettingDefinition> definitions =
            new Dictionary<string, SettingDefinition>(StringComparer.Ordinal);
        private SettingsService service;
        private bool bound;

        public override void OnNetworkSpawn()
        {
            CollectServerDefinitions();
            BindService();
            if (!IsServer) RequestSnapshotServerRpc();
        }

        private void CollectServerDefinitions()
        {
            definitions.Clear();
            foreach (SettingsProvider provider in catalogProviders)
            {
                if (provider == null) throw new ArgumentException("Network catalog provider is missing.");
                foreach (SettingDefinition definition in provider.GetDefinitions())
                {
                    if (definition.Scope != SettingScope.ServerSession) continue;
                    if (definitions.TryGetValue(definition.Id, out SettingDefinition existing) &&
                        !ReferenceEquals(existing, definition))
                        throw new ArgumentException($"Duplicate network setting ID '{definition.Id}'.");
                    definitions[definition.Id] = definition;
                }
            }
        }

        private void BindService()
        {
            service = SettingsRuntime.Service;
            service.SetRole(IsServer ? SettingsRole.Server : SettingsRole.Client);
            foreach (SettingDefinition definition in definitions.Values) service.Get(definition);
            if (IsServer) service.ValueChanged += HandleValueChanged;
            bound = true;
        }

        public override void OnNetworkDespawn()
        {
            if (!bound) return;
            service.ValueChanged -= HandleValueChanged;
            service.SetRole(SettingsRole.Standalone);
            bound = false;
        }

        private void HandleValueChanged(SettingDefinition definition, object value)
        {
            if (!IsSpawned || definition.Scope != SettingScope.ServerSession || !definitions.ContainsKey(definition.Id)) return;
            ReceiveValueClientRpc(definition.Id, definition.SerializeValue(value));
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestSnapshotServerRpc(ServerRpcParams rpc = default)
        {
            ClientRpcParams target = new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { rpc.Receive.SenderClientId } }
            };
            foreach (SettingDefinition definition in definitions.Values)
                ReceiveValueClientRpc(definition.Id, definition.SerializeValue(service.Get(definition)), target);
        }

        [ClientRpc]
        private void ReceiveValueClientRpc(string id, string json, ClientRpcParams rpc = default)
        {
            if (IsServer || service == null || !definitions.TryGetValue(id, out SettingDefinition definition)) return;
            if (definition.TryDeserializeValue(json, out object value)) service.ApplyServerValue(definition, value);
        }
    }
}
