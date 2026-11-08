using System;
using Framework.GameplayTag;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

namespace UnscriptedEngine
{
    /// <summary>
    /// Local float storage that becomes server-authoritative when network spawned.
    /// For replication, place on a NetworkObject or beneath one on the same prefab.
    /// Tags and clamp settings are prefab configuration and must match on all peers.
    /// </summary>
    [AddComponentMenu("Unscripted Engine/Values/Tagged Float Value")]
    public sealed class TaggedFloatValue : NetworkBehaviour
    {
        [Serializable]
        public sealed class FloatChangedEvent : UnityEvent<float> { }

        [SerializeField, Tooltip("Tags identifying this value, such as Stats.Health or Stats.Stamina.")]
        private GameplayTagContainer tags = new GameplayTagContainer();

        [SerializeField, Tooltip("Starting value. Runtime changes should use SetValue or AddValue.")]
        private float initialValue = 100f;

        [SerializeField, Tooltip("Restrict values to the configured minimum and maximum.")]
        private bool clampValue = true;

        [SerializeField, Tooltip("Lowest allowed value when clamping is enabled.")]
        private float minimum = 0f;

        [SerializeField, Tooltip("Highest allowed value when clamping is enabled.")]
        private float maximum = 100f;

        [SerializeField, Tooltip("Invoked locally when the value changes, including received network updates. Also invoked on network spawn to initialize listeners.")]
        private FloatChangedEvent onValueChanged = new FloatChangedEvent();

        private readonly NetworkVariable<float> networkValue = new NetworkVariable<float>(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private float localValue;
        private bool localValueInitialized;

        public GameplayTagContainer Tags => tags;
        public float Value => IsSpawned ? networkValue.Value : localValueInitialized ? localValue : Constrain(initialValue);
        public bool IsClamped => clampValue;
        public float Minimum => minimum;
        public float Maximum => maximum;
        public FloatChangedEvent OnValueChanged => onValueChanged;

        // Child values share their pawn's NetworkObject. Servers may configure prefabs before spawn.
        public bool CanWrite => IsSpawned ? IsServer :
            NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsServer;

        private void Awake()
        {
            ValidateConfiguration();
            if (!localValueInitialized) localValue = Constrain(initialValue);
            localValueInitialized = true;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (IsServer) networkValue.Value = Constrain(localValueInitialized ? localValue : initialValue);
            networkValue.OnValueChanged -= HandleNetworkValueChanged;
            networkValue.OnValueChanged += HandleNetworkValueChanged;
            localValue = networkValue.Value;
            localValueInitialized = true;
            onValueChanged.Invoke(localValue);
        }

        public override void OnNetworkDespawn()
        {
            networkValue.OnValueChanged -= HandleNetworkValueChanged;
            base.OnNetworkDespawn();
        }

        public override void OnDestroy()
        {
            networkValue.OnValueChanged -= HandleNetworkValueChanged;
            base.OnDestroy();
        }

        /// <summary>Sets locally offline, or on the server online. Clients cannot write.</summary>
        public void SetValue(float nextValue)
        {
            TrySetValue(nextValue);
        }

        /// <summary>Returns false for unauthorized writes or non-finite input.</summary>
        public bool TrySetValue(float nextValue)
        {
            if (!CanWrite || !IsFinite(nextValue)) return false;
            nextValue = Constrain(nextValue);
            if (IsSpawned)
            {
                networkValue.Value = nextValue;
            }
            else
            {
                float previousValue = Value;
                localValue = nextValue;
                localValueInitialized = true;
                if (previousValue != nextValue) onValueChanged.Invoke(localValue);
            }
            return true;
        }

        /// <summary>Adds a signed amount; negative amounts subtract.</summary>
        public void AddValue(float amount)
        {
            TrySetValue(Value + amount);
        }

        public void ResetValue()
        {
            SetValue(initialValue);
        }

        private void HandleNetworkValueChanged(float previousValue, float nextValue)
        {
            localValue = nextValue;
            onValueChanged.Invoke(nextValue);
        }

        private float Constrain(float nextValue)
        {
            return clampValue ? Mathf.Clamp(nextValue, minimum, maximum) : nextValue;
        }

        private void OnValidate()
        {
            ValidateConfiguration();
        }

        private void ValidateConfiguration()
        {
            if (!IsFinite(minimum)) minimum = 0f;
            if (!IsFinite(maximum)) maximum = minimum;
            maximum = Mathf.Max(minimum, maximum);
            if (!IsFinite(initialValue)) initialValue = 0f;
            initialValue = Constrain(initialValue);
        }

        private static bool IsFinite(float number)
        {
            return !float.IsNaN(number) && !float.IsInfinity(number);
        }
    }
}
