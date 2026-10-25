using Unity.Netcode;
using UnityEngine;

namespace UnscriptedEngine
{
    /// <summary>Smooths a render-only child while the network root collider receives unsmoothed snapshots.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1000)]
    public sealed class NetworkPhysicsVisualSmoothing : MonoBehaviour
    {
        [SerializeField] private Transform visualRoot;
        [SerializeField, Min(0f)] private float smoothingSpeed = 20f;
        [SerializeField, Min(0f)] private float snapDistance = 2f;

        private NetworkObject networkObject;
        private Vector3 localPosition;
        private Quaternion localRotation;
        private Vector3 displayedPosition;
        private Quaternion displayedRotation;
        private bool wasSmoothing;

        private void Awake()
        {
            networkObject = GetComponent<NetworkObject>();
            if (visualRoot == null || visualRoot == transform)
            {
                enabled = false;
                return;
            }

            localPosition = visualRoot.localPosition;
            localRotation = visualRoot.localRotation;
        }

        private void LateUpdate()
        {
            bool shouldSmooth = networkObject != null && networkObject.IsSpawned &&
                networkObject.NetworkManager != null && !networkObject.NetworkManager.IsServer;
            Vector3 targetPosition = transform.TransformPoint(localPosition);
            Quaternion targetRotation = transform.rotation * localRotation;

            if (!shouldSmooth || !wasSmoothing || smoothingSpeed <= 0f ||
                Vector3.Distance(displayedPosition, targetPosition) > snapDistance)
            {
                displayedPosition = targetPosition;
                displayedRotation = targetRotation;
            }
            else
            {
                float blend = 1f - Mathf.Exp(-smoothingSpeed * Time.deltaTime);
                displayedPosition = Vector3.Lerp(displayedPosition, targetPosition, blend);
                displayedRotation = Quaternion.Slerp(displayedRotation, targetRotation, blend);
            }

            visualRoot.SetPositionAndRotation(displayedPosition, displayedRotation);
            wasSmoothing = shouldSmooth;
        }

        private void OnDisable()
        {
            if (visualRoot != null && visualRoot != transform)
                visualRoot.SetLocalPositionAndRotation(localPosition, localRotation);

            wasSmoothing = false;
        }
    }
}
