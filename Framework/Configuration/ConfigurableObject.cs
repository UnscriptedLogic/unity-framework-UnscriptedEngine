using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace UnscriptedEngine.Configuration
{
    /// <summary>Explicit opt-in for runtime inspection. Edits are instance-only and host-authoritative.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [AddComponentMenu("Unscripted Engine/Configuration/Configurable Object")]
    public sealed class ConfigurableObject : NetworkBehaviour
    {
        [SerializeField]
        [Tooltip("Empty exposes MonoBehaviours on this GameObject. Otherwise exposes only these components, which must belong to this object's hierarchy.")]
        private MonoBehaviour[] components = Array.Empty<MonoBehaviour>();

        private readonly List<string> unsupported = new List<string>();
        private List<ConfigurableField> fields;
        private Dictionary<string, ConfigurableField> byId;
        private NetworkList<ConfigurationValue> replicatedValues;
        public IReadOnlyList<ConfigurableField> Fields { get { EnsureFields(); return fields; } }
        public IReadOnlyList<string> UnsupportedFields { get { EnsureFields(); return unsupported; } }
        public event Action ValuesChanged;
        public string LastError { get; private set; }
        public bool CanEdit => isActiveAndEnabled && (!IsOnline || (IsSpawned && IsServer));
        private bool IsOnline => Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening;

        private void Awake() => replicatedValues = new NetworkList<ConfigurationValue>();

        internal IEnumerable<MonoBehaviour> ConfigurationComponents()
        {
            MonoBehaviour[] candidates = components == null || components.Length == 0 ? GetComponents<MonoBehaviour>() : components;
            HashSet<MonoBehaviour> seen = new HashSet<MonoBehaviour>();
            foreach (MonoBehaviour component in candidates)
                if (component != null && component != this && component is not Unity.Netcode.NetworkObject && component.transform.IsChildOf(transform)
                    && component.GetComponentInParent<ConfigurableObject>() == this && seen.Add(component)) yield return component;
        }

        private void EnsureFields()
        {
            if (fields != null) return;
            fields = ConfigurableField.Discover(this, unsupported);
            byId = fields.ToDictionary(field => field.Definition.Id, StringComparer.Ordinal);
        }

        public bool InRange(Transform observer, float range) => observer != null && range > 0f
            && (observer.position - transform.position).sqrMagnitude <= range * range;

        public bool TrySet(string fieldId, object value, Transform observer, float range)
        {
            LastError = "Configuration requires the host, an active target, and an observer in range.";
            if (!CanEdit || !InRange(observer, range)) return false;
            EnsureFields();
            LastError = "Unknown field or invalid value.";
            if (string.IsNullOrEmpty(fieldId) || !byId.TryGetValue(fieldId, out ConfigurableField field)
                || !field.Definition.TryNormalize(value, out object normalized)) return false;
            string json = field.Definition.SerializeValue(normalized);
            if (IsOnline && (Encoding.UTF8.GetByteCount(fieldId) > FixedString64Bytes.UTF8MaxLengthInBytes
                || Encoding.UTF8.GetByteCount(json) > FixedString512Bytes.UTF8MaxLengthInBytes))
            { LastError = "This value is too large to replicate."; return false; }
            int index = FindReplicatedIndex(fieldId);
            if (IsOnline && index < 0 && replicatedValues.Count >= 128)
            { LastError = "This object has reached its 128 edited-field limit."; return false; }
            if (!field.TryWrite(normalized, out string reason)) { LastError = reason; return false; }
            if (IsOnline)
            {
                ConfigurationValue record = new ConfigurationValue { Id = fieldId, Json = json };
                if (index < 0) replicatedValues.Add(record);
                else replicatedValues[index] = record;
            }
            LastError = null;
            ValuesChanged?.Invoke();
            return true;
        }

        private int FindReplicatedIndex(string id)
        {
            if (replicatedValues != null)
                for (int index = 0; index < replicatedValues.Count; index++)
                    if (replicatedValues[index].Id.ToString() == id) return index;
            return -1;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            EnsureFields();
            replicatedValues.OnListChanged += OnReplicatedValuesChanged;
            if (!IsServer) ApplyReplicatedValues();
        }

        public override void OnNetworkDespawn()
        {
            replicatedValues.OnListChanged -= OnReplicatedValuesChanged;
            base.OnNetworkDespawn();
        }

        private void OnReplicatedValuesChanged(NetworkListEvent<ConfigurationValue> change)
        {
            if (!IsServer) ApplyReplicatedValues();
        }

        private void ApplyReplicatedValues()
        {
            foreach (ConfigurationValue record in replicatedValues)
                if (byId.TryGetValue(record.Id.ToString(), out ConfigurableField field)
                    && field.Definition.TryDeserializeValue(record.Json.ToString(), out object value) && !Equals(field.Read(), value)
                    && !field.TryWrite(value, out string reason))
                    Debug.LogWarning("Configuration replication rejected: " + reason, this);
            ValuesChanged?.Invoke();
        }

        private struct ConfigurationValue : INetworkSerializable, IEquatable<ConfigurationValue>
        {
            public FixedString64Bytes Id;
            public FixedString512Bytes Json;
            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            { serializer.SerializeValue(ref Id); serializer.SerializeValue(ref Json); }
            public bool Equals(ConfigurationValue other) => Id.Equals(other.Id) && Json.Equals(other.Json);
        }
    }
}
