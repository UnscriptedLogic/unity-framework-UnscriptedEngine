using System.Collections.Generic;
using Framework.Components;
using UnityEngine;

namespace Framework.Objects
{
    [DisallowMultipleComponent]
    public class CameraHandler : MonoBehaviour
    {
        [Header("Follow")]
        [SerializeField] private float followSharpness = 12f;
        [SerializeField] private bool snapWhenMagnetChanges;

        private readonly List<CameraMagnetComponent> magnetQueue = new List<CameraMagnetComponent>();

        public static CameraHandler Instance { get; private set; }

        public CameraMagnetComponent ActiveMagnet { get; private set; }

        public Transform ActiveTarget => ActiveMagnet != null ? ActiveMagnet.TargetTransform : null;

        public int MagnetCount => magnetQueue.Count;

        public bool HasMagnets => magnetQueue.Count > 0;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"Multiple {nameof(CameraHandler)} instances found. Using {Instance.name}.");
                return;
            }

            Instance = this;
        }

        private void Update()
        {
            PruneInvalidMagnets();

            CameraMagnetComponent nextMagnet = GetTopMagnet();

            if (nextMagnet == null)
            {
                ActiveMagnet = null;
                return;
            }

            bool magnetChanged = ActiveMagnet != nextMagnet;
            ActiveMagnet = nextMagnet;
            FollowMagnet(nextMagnet, magnetChanged);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public bool RegisterMagnet(CameraMagnetComponent magnet)
        {
            if (!IsValidMagnet(magnet))
            {
                return false;
            }

            magnetQueue.Remove(magnet);
            magnetQueue.Add(magnet);
            return true;
        }

        public void ReleaseMagnet(CameraMagnetComponent magnet)
        {
            magnetQueue.Remove(magnet);

            if (ActiveMagnet == magnet)
            {
                ActiveMagnet = null;
            }
        }

        public bool IsRegistered(CameraMagnetComponent magnet)
        {
            return magnetQueue.Contains(magnet);
        }

        private void FollowMagnet(CameraMagnetComponent magnet, bool magnetChanged)
        {
            Vector3 targetPosition = magnet.GetCameraPosition();

            if (followSharpness <= 0f || (magnetChanged && snapWhenMagnetChanges))
            {
                transform.position = targetPosition;
                return;
            }

            float lerpAmount = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, targetPosition, lerpAmount);
        }

        private CameraMagnetComponent GetTopMagnet()
        {
            for (int i = magnetQueue.Count - 1; i >= 0; i--)
            {
                CameraMagnetComponent magnet = magnetQueue[i];

                if (IsValidMagnet(magnet))
                {
                    return magnet;
                }
            }

            return null;
        }

        private void PruneInvalidMagnets()
        {
            for (int i = magnetQueue.Count - 1; i >= 0; i--)
            {
                if (!IsValidMagnet(magnetQueue[i]))
                {
                    magnetQueue.RemoveAt(i);
                }
            }
        }

        private static bool IsValidMagnet(CameraMagnetComponent magnet)
        {
            return magnet != null
                && magnet.isActiveAndEnabled
                && magnet.TargetTransform != null
                && magnet.IsPullingCamera;
        }
    }
}
