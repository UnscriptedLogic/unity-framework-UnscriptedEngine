using System;
using Unity.Netcode;
using UnityEngine;

namespace UnscriptedEngine
{
    /// <summary>Holds an authored set of rigidbodies in place until an authorized release.</summary>
    [DisallowMultipleComponent]
    public sealed class RigidbodyReleaseGroup : MonoBehaviour
    {
        [SerializeField] private Rigidbody[] bodies = Array.Empty<Rigidbody>();

        public bool IsHolding { get; private set; } = true;

        private void Awake()
        {
            if (!IsHolding)
                return;

            foreach (Rigidbody body in bodies)
            {
                if (body != null)
                    body.isKinematic = true;
            }
        }

        public bool TryRelease()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (!Application.isPlaying || !isActiveAndEnabled || !IsHolding ||
                (manager != null && manager.IsListening && !manager.IsServer))
                return false;

            IsHolding = false;
            ReleaseBodies(bodies);
            return true;
        }

        internal static void ReleaseBodies(Rigidbody[] bodies)
        {
            foreach (Rigidbody body in bodies)
            {
                if (body == null)
                    continue;

                body.isKinematic = false;
                body.WakeUp();
            }
        }
    }
}
