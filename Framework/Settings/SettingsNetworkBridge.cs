using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace UnscriptedEngine.Settings
{
    /// <summary>
    /// Optional Netcode for GameObjects adapter. Spawn one server-owned persistent NetworkObject.
    /// Catalogs identify known server settings; they do not activate them or expose them in the UI.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class SettingsNetworkBridge : NetworkBehaviour
    {
        [SerializeField, Tooltip("Schema of all ServerSession settings this bridge synchronizes, including nested groups. Assign matching definitions on server and clients. These references do not activate settings; modules do. Use one server-owned bridge per session.")]
        private List<SettingsGroup> catalogs = new();
        private readonly Dictionary<string, SettingDefinition> definitions = new(StringComparer.Ordinal);
        private SettingsService service;
        private bool bound;

        public override void OnNetworkSpawn()
        {
            definitions.Clear();
            foreach (var catalog in catalogs)
            foreach (var definition in catalog.Flatten())
            {
                if (definition.Scope != SettingScope.ServerSession) continue;
                if (definitions.TryGetValue(definition.Id, out var existing) && existing != definition)
                    throw new ArgumentException($"Duplicate network setting ID '{definition.Id}'.");
                definitions[definition.Id] = definition;
            }
            service = SettingsRuntime.Service;
            service.SetRole(IsServer ? SettingsRole.Server : SettingsRole.Client);
            foreach (var definition in definitions.Values) service.Get(definition);
            if (IsServer) service.ValueChanged += HandleValueChanged;
            bound = true;
            if (!IsServer) RequestSnapshotServerRpc();
        }

        public override void OnNetworkDespawn()
        {
            if (!bound) return;
            if (service != null)
            {
                service.ValueChanged -= HandleValueChanged;
                service.SetRole(SettingsRole.Standalone);
            }
            bound = false;
        }

        private void HandleValueChanged(SettingDefinition definition, object value)
        {
            if (IsSpawned && definition.Scope == SettingScope.ServerSession && definitions.ContainsKey(definition.Id))
                ReceiveValueClientRpc(definition.Id, definition.SerializeValue(value));
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestSnapshotServerRpc(ServerRpcParams rpc = default)
        {
            var target = new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { rpc.Receive.SenderClientId } }
            };
            // Values, including defaults for inactive settings, are sent to each joining client.
            foreach (var definition in definitions.Values)
                ReceiveValueClientRpc(definition.Id, definition.SerializeValue(service.Get(definition)), target);
        }

        [ClientRpc]
        private void ReceiveValueClientRpc(string id, string json, ClientRpcParams rpc = default)
        {
            if (IsServer || service == null || !definitions.TryGetValue(id, out var definition)) return;
            if (definition.TryDeserializeValue(json, out var value)) service.ApplyServerValue(definition, value);
        }
    }
}
