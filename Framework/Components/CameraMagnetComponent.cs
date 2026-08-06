using Framework.Objects;
using UnityEngine;

namespace Framework.Components
{
    [DisallowMultipleComponent]
    public class CameraMagnetComponent : UObjectComponent
    {
        [SerializeField] private GameObject targetGameObject;
        [SerializeField] private Vector3 followOffset;
        [SerializeField] private bool pullOnBeginPlay;
        [SerializeField] private bool onlyOwnerCanPull = true;

        private CameraHandler cameraHandler;

        public bool IsPullingCamera { get; private set; }

        public GameObject TargetGameObject => targetGameObject != null ? targetGameObject : gameObject;

        public Transform TargetTransform => TargetGameObject != null ? TargetGameObject.transform : null;

        public Vector3 FollowOffset
        {
            get => followOffset;
            set => followOffset = value;
        }

        public void SetTarget(GameObject newTarget)
        {
            targetGameObject = newTarget;
        }

        public bool PullCamera()
        {
            return PullCamera(targetGameObject);
        }

        public bool PullCamera(GameObject newTarget)
        {
            if (newTarget != null)
            {
                targetGameObject = newTarget;
            }

            if (!CanPullCamera())
            {
                return false;
            }

            CameraHandler handler = ResolveCameraHandler();

            if (handler == null)
            {
                Debug.LogWarning($"{nameof(CameraMagnetComponent)} could not find a {nameof(CameraHandler)}.");
                return false;
            }

            IsPullingCamera = true;

            if (!handler.RegisterMagnet(this))
            {
                IsPullingCamera = false;
                return false;
            }

            cameraHandler = handler;
            return true;
        }

        public void ReleaseCamera()
        {
            IsPullingCamera = false;

            if (cameraHandler == null)
            {
                cameraHandler = ResolveCameraHandler();
            }

            if (cameraHandler != null)
            {
                cameraHandler.ReleaseMagnet(this);
            }

            cameraHandler = null;
        }

        public Vector3 GetCameraPosition()
        {
            Transform targetTransform = TargetTransform;
            return targetTransform != null
                ? targetTransform.position + followOffset
                : transform.position + followOffset;
        }

        protected override void BeginPlay()
        {
            base.BeginPlay();

            if (pullOnBeginPlay)
            {
                PullCamera();
            }
        }

        protected override void OnDisable()
        {
            ReleaseCamera();
            base.OnDisable();
        }

        public override void OnNetworkDespawn()
        {
            ReleaseCamera();
            base.OnNetworkDespawn();
        }

        public override void OnDestroy()
        {
            ReleaseCamera();
            base.OnDestroy();
        }

        private bool CanPullCamera()
        {
            return TargetTransform != null
                && (!onlyOwnerCanPull || !IsSpawned || IsOwner);
        }

        private CameraHandler ResolveCameraHandler()
        {
            if (CameraHandler.Instance != null)
            {
                return CameraHandler.Instance;
            }

            return FindAnyObjectByType<CameraHandler>();
        }
    }
}
