using Unity.Netcode;
using UnityEngine;

namespace Framework.Components
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public class PlatformMovementComponent : UObjectComponent
    {
        [Header("Path")]
        [SerializeField] private Transform startPoint;
        [SerializeField] private Transform endPoint;
        [SerializeField] private Vector3 localStartOffset;
        [SerializeField] private Vector3 localEndOffset = Vector3.up * 3f;

        [Header("Movement")]
        [SerializeField] private float moveSpeed = 2f;
        [SerializeField] private float returnMoveSpeed = 2f;
        [SerializeField] private float waitAtPointsDuration;
        [SerializeField] private bool playOnSpawn = true;
        [SerializeField] private bool pingPong = true;

        [Header("Networking")]
        [SerializeField] private float clientInterpolationSpeed = 18f;

        private readonly NetworkVariable<Vector3> replicatedPosition =
            new NetworkVariable<Vector3>(
                default,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private Vector3 initialPosition;
        private Vector3 resolvedStartPosition;
        private Vector3 resolvedEndPosition;
        private Rigidbody platformRigidbody;
        private bool isMoving;
        private bool movingTowardEnd = true;
        private bool stopAtNextPoint;
        private float waitTimer;

        protected override bool CanTick => IsSpawned;

        protected override void Awake()
        {
            base.Awake();
            initialPosition = transform.position;
            platformRigidbody = GetComponent<Rigidbody>();
        }

        private void OnValidate()
        {
            moveSpeed = Mathf.Max(0f, moveSpeed);
            returnMoveSpeed = Mathf.Max(0f, returnMoveSpeed);
            waitAtPointsDuration = Mathf.Max(0f, waitAtPointsDuration);
            clientInterpolationSpeed = Mathf.Max(0f, clientInterpolationSpeed);
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            ResolvePathPositions();

            if (IsServer)
            {
                isMoving = playOnSpawn;
                movingTowardEnd = true;
                waitTimer = 0f;
                SetPlatformPosition(resolvedStartPosition);
                replicatedPosition.Value = resolvedStartPosition;
            }
            else
            {
                SetPlatformPosition(replicatedPosition.Value);
            }
        }

        protected override void FixedTick(float fixedDeltaTime)
        {
            if (!IsServer || !isMoving || moveSpeed <= 0f)
            {
                return;
            }

            if (waitTimer > 0f)
            {
                waitTimer = Mathf.Max(0f, waitTimer - fixedDeltaTime);
                return;
            }

            Vector3 targetPosition = movingTowardEnd ? resolvedEndPosition : resolvedStartPosition;
            float finalSpeed = movingTowardEnd ? moveSpeed : returnMoveSpeed;
            Vector3 nextPosition = Vector3.MoveTowards(
                transform.position,
                targetPosition,
                finalSpeed * fixedDeltaTime);

            SetPlatformPosition(nextPosition);
            replicatedPosition.Value = nextPosition;

            if (Vector3.SqrMagnitude(nextPosition - targetPosition) > Mathf.Epsilon)
            {
                return;
            }

            waitTimer = waitAtPointsDuration;

            if (stopAtNextPoint)
            {
                isMoving = false;
                stopAtNextPoint = false;
            }
            else if (pingPong)
            {
                movingTowardEnd = !movingTowardEnd;
            }
            else
            {
                isMoving = false;
            }
        }

        protected override void Tick(float deltaTime)
        {
            if (IsServer)
            {
                return;
            }

            float interpolationAlpha = clientInterpolationSpeed <= 0f
                ? 1f
                : 1f - Mathf.Exp(-clientInterpolationSpeed * deltaTime);

            SetPlatformPosition(Vector3.Lerp(transform.position, replicatedPosition.Value, interpolationAlpha));
        }

        public void StartMovement()
        {
            if (IsServer)
            {
                isMoving = true;
                stopAtNextPoint = false;
                return;
            }

            StartMovementServerRpc();
        }

        public void StopMovement()
        {
            if (IsServer)
            {
                isMoving = false;
                return;
            }

            StopMovementServerRpc();
        }

        public void ResetToStart()
        {
            if (IsServer)
            {
                ResetToStartInternal();
                return;
            }

            ResetToStartServerRpc();
        }

        public void SetPath(Transform newStartPoint, Transform newEndPoint, bool snapToStart = false)
        {
            startPoint = newStartPoint;
            endPoint = newEndPoint;

            if (IsServer)
            {
                ResolvePathPositions();

                if (snapToStart)
                {
                    ResetToStartInternal();
                }
            }
        }

        public void MoveToStart()
        {
            if (IsServer)
            {
                MoveToStartInternal();
                return;
            }

            MoveToStartServerRpc();
        }

        public void MoveToEnd()
        {
            if (IsServer)
            {
                MoveToEndInternal();
                return;
            }

            MoveToEndServerRpc();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void StartMovementServerRpc()
        {
            isMoving = true;
            stopAtNextPoint = false;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void StopMovementServerRpc()
        {
            isMoving = false;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void ResetToStartServerRpc()
        {
            ResetToStartInternal();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void MoveToStartServerRpc()
        {
            MoveToStartInternal();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void MoveToEndServerRpc()
        {
            MoveToEndInternal();
        }

        private void MoveToStartInternal()
        {
            ResolvePathPositions();
            movingTowardEnd = false;
            stopAtNextPoint = true;
            waitTimer = 0f;
            isMoving = true;
        }

        private void MoveToEndInternal()
        {
            ResolvePathPositions();
            movingTowardEnd = true;
            stopAtNextPoint = true;
            waitTimer = 0f;
            isMoving = true;
        }

        private void ResetToStartInternal()
        {
            ResolvePathPositions();
            movingTowardEnd = true;
            stopAtNextPoint = false;
            waitTimer = 0f;
            SetPlatformPosition(resolvedStartPosition);
            replicatedPosition.Value = resolvedStartPosition;
        }

        private void ResolvePathPositions()
        {
            resolvedStartPosition = startPoint != null
                ? startPoint.position
                : initialPosition + localStartOffset;

            resolvedEndPosition = endPoint != null
                ? endPoint.position
                : initialPosition + localEndOffset;
        }

        private void SetPlatformPosition(Vector3 position)
        {
            if (platformRigidbody != null && platformRigidbody.isKinematic && Time.inFixedTimeStep)
            {
                platformRigidbody.MovePosition(position);
                return;
            }

            transform.position = position;
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 previewStartPosition = startPoint != null
                ? startPoint.position
                : transform.position + localStartOffset;

            Vector3 previewEndPosition = endPoint != null
                ? endPoint.position
                : transform.position + localEndOffset;

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(previewStartPosition, 0.2f);
            Gizmos.DrawWireSphere(previewEndPosition, 0.2f);
            Gizmos.DrawLine(previewStartPosition, previewEndPosition);
        }
    }
}
