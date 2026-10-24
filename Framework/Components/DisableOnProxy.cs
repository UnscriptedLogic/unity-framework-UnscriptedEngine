using Unity.Netcode;
using UnityEngine;

namespace UnscriptedEngine
{
    /// <summary>
    /// Keeps presentation objects active only on the owning client.
    /// Attach to the active network root and assign camera children, not the network root itself.
    /// </summary>
    [AddComponentMenu("Unscripted Engine/Disable On Proxy")]
    [DisallowMultipleComponent]
    public sealed class DisableOnProxy : NetworkBehaviour
    {
        [Tooltip("Child presentation objects enabled only for the local owner. Do not include objects with networking components.")]
        [SerializeField] private GameObject[] targets = System.Array.Empty<GameObject>();

        private bool[] validTargets;

        private void Awake()
        {
            validTargets = new bool[targets.Length];
            for (int i = 0; i < targets.Length; i++)
            {
                GameObject target = targets[i];
                if (target == null)
                    continue;

                // The listener must stay active to receive future ownership changes.
                // Inactive NetworkBehaviours can also be excluded from spawn registration.
                if (target == gameObject || !target.transform.IsChildOf(transform)
                    || target.GetComponentInChildren<NetworkObject>(true) != null
                    || target.GetComponentInChildren<NetworkBehaviour>(true) != null)
                {
                    Debug.LogError($"{nameof(DisableOnProxy)} target '{target.name}' must be a child presentation object without networking components.", this);
                    continue;
                }

                validTargets[i] = true;
            }

            // Prevent cameras competing before ownership is known, including pooled objects.
            SetTargetsActive(false);
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            RefreshOwnership();
        }

        public override void OnGainedOwnership()
        {
            base.OnGainedOwnership();
            RefreshOwnership();
        }

        public override void OnLostOwnership()
        {
            base.OnLostOwnership();
            SetTargetsActive(false);
        }

        public override void OnNetworkDespawn()
        {
            SetTargetsActive(false);
            base.OnNetworkDespawn();
        }

        private void RefreshOwnership()
        {
            // A dedicated server can own objects, but must not activate local presentation.
            SetTargetsActive(IsSpawned && IsClient && IsOwner);
        }

        private void SetTargetsActive(bool active)
        {
            if (validTargets == null)
                return;

            for (int i = 0; i < targets.Length; i++)
            {
                if (validTargets[i] && targets[i] != null && targets[i].activeSelf != active)
                    targets[i].SetActive(active);
            }
        }
    }
}
