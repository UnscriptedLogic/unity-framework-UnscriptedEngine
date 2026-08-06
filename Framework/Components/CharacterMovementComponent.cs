using System;
using Unity.Netcode;
using UnityEngine;

namespace Framework.Components
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    [DefaultExecutionOrder(-50)]
    public class CharacterMovementComponent : UObjectComponent
    {
        public struct CharacterMoveInput : INetworkSerializeByMemcpy, IEquatable<CharacterMoveInput>
        {
            public int Tick;
            public float DeltaTime;
            public float MoveX;
            public float MoveY;
            public float ControlYaw;
            public bool JumpPressed;

            public bool Equals(CharacterMoveInput other)
            {
                return Tick == other.Tick
                    && DeltaTime.Equals(other.DeltaTime)
                    && MoveX.Equals(other.MoveX)
                    && MoveY.Equals(other.MoveY)
                    && ControlYaw.Equals(other.ControlYaw)
                    && JumpPressed == other.JumpPressed;
            }

            public override bool Equals(object obj)
            {
                return obj is CharacterMoveInput other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Tick, DeltaTime, MoveX, MoveY, ControlYaw, JumpPressed);
            }
        }

        public struct CharacterMoveState : INetworkSerializeByMemcpy, IEquatable<CharacterMoveState>
        {
            public int Tick;
            public Vector3 Position;
            public Vector3 Velocity;
            public bool IsGrounded;

            public CharacterMoveState(int tick, Vector3 position, Vector3 velocity, bool isGrounded)
            {
                Tick = tick;
                Position = position;
                Velocity = velocity;
                IsGrounded = isGrounded;
            }

            public bool Equals(CharacterMoveState other)
            {
                return Tick == other.Tick
                    && Position.Equals(other.Position)
                    && Velocity.Equals(other.Velocity)
                    && IsGrounded == other.IsGrounded;
            }

            public override bool Equals(object obj)
            {
                return obj is CharacterMoveState other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Tick, Position, Velocity, IsGrounded);
            }
        }

        [Header("Walking")]
        [SerializeField] private float maxWalkSpeed = 6f;
        [SerializeField] private float acceleration = 24f;
        [SerializeField] private float brakingDecelerationWalking = 32f;
        [SerializeField] private float airControl = 0.35f;
        [SerializeField] private bool orientInputToControlYaw = true;

        [Header("Jumping")]
        [SerializeField] private float jumpVelocity = 7.5f;
        [SerializeField] private float gravity = -24f;
        [SerializeField] private float terminalVelocity = 45f;
        [SerializeField] private float groundedStickForce = 2f;

        [Header("Networking")]
        [SerializeField] private int predictionBufferSize = 1024;
        [SerializeField] private float reconciliationPositionTolerance = 0.08f;
        [SerializeField] private float reconciliationVelocityTolerance = 0.15f;
        [SerializeField] private float proxyInterpolationSpeed = 18f;

        [Header("Smoothing")]
        [SerializeField] private bool interpolateOwnerMovement = true;
        [SerializeField] private float ownerInterpolationSnapDistance = 2f;

        [Header("Controller Stability")]
        [SerializeField] private float minimumControllerSkinWidth = 0.01f;
        [SerializeField] private float minimumControllerSkinWidthRadiusRatio = 0.08f;

        private readonly NetworkVariable<CharacterMoveState> authoritativeState =
            new NetworkVariable<CharacterMoveState>(
                default,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private CharacterController characterController;
        private CharacterMoveInput[] inputBuffer;
        private CharacterMoveState[] stateBuffer;
        private Vector2 pendingMoveInput;
        private bool jumpQueued;
        private float controlYaw;
        private Vector3 velocity;
        private bool isGrounded;
        private int localTick;
        private int lastProcessedServerTick = -1;
        private int lastReceivedServerTick = -1;
        private CharacterMoveState proxyTargetState;
        private bool hasProxyTargetState;
        private CharacterMoveState deferredProxyTargetState;
        private bool hasDeferredProxyTargetState;
        private float proxySyncSuppressedUntil;
        private Vector3 previousFixedPosition;
        private Vector3 currentFixedPosition;
        private bool hasFixedPositionSamples;
        private bool hasInterpolatedTransform;

        public Vector3 Velocity => velocity;

        public NetworkVariable<CharacterMoveState> AuthoritativeState => authoritativeState;

        public bool IsGrounded => isGrounded;

        public bool IsFalling => !isGrounded;

        public bool CanJump => isGrounded;

        public float MaxWalkSpeed
        {
            get => maxWalkSpeed;
            set => maxWalkSpeed = Mathf.Max(0f, value);
        }

        public void SetMovementInput(Vector2 input)
        {
            pendingMoveInput = Vector2.ClampMagnitude(input, 1f);
        }

        public void AddMovementInput(Vector3 worldDirection, float scale = 1f)
        {
            Vector3 flatDirection = new Vector3(worldDirection.x, 0f, worldDirection.z);

            if (flatDirection.sqrMagnitude <= Mathf.Epsilon)
            {
                return;
            }

            Vector2 input = new Vector2(flatDirection.normalized.x, flatDirection.normalized.z) * scale;
            pendingMoveInput = Vector2.ClampMagnitude(pendingMoveInput + input, 1f);
        }

        public void SetControlYaw(float yawDegrees)
        {
            controlYaw = yawDegrees;
        }

        public void Jump()
        {
            jumpQueued = true;
        }

        public void StopMovementImmediately()
        {
            velocity = Vector3.zero;
        }

        protected override void Awake()
        {
            base.Awake();
            characterController = GetComponent<CharacterController>();
            ConfigureCharacterController();
            AllocatePredictionBuffers();
        }

        private void OnValidate()
        {
            predictionBufferSize = Mathf.Max(32, predictionBufferSize);
            reconciliationPositionTolerance = Mathf.Max(0f, reconciliationPositionTolerance);
            reconciliationVelocityTolerance = Mathf.Max(0f, reconciliationVelocityTolerance);
            proxyInterpolationSpeed = Mathf.Max(0f, proxyInterpolationSpeed);
            ownerInterpolationSnapDistance = Mathf.Max(0f, ownerInterpolationSnapDistance);
            minimumControllerSkinWidth = Mathf.Max(0f, minimumControllerSkinWidth);
            minimumControllerSkinWidthRadiusRatio = Mathf.Max(0f, minimumControllerSkinWidthRadiusRatio);

            if (characterController == null)
            {
                characterController = GetComponent<CharacterController>();
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            ResetPredictionState();
            authoritativeState.OnValueChanged += HandleAuthoritativeStateChanged;

            if (IsServer)
            {
                authoritativeState.Value = CaptureState(-1);
            }
            else
            {
                HandleAuthoritativeStateChanged(default, authoritativeState.Value);
            }
        }

        public override void OnNetworkDespawn()
        {
            authoritativeState.OnValueChanged -= HandleAuthoritativeStateChanged;
            base.OnNetworkDespawn();
        }

        protected override void FixedTick(float fixedDeltaTime)
        {
            if (!IsSpawned)
            {
                return;
            }

            RestoreSimulationPosition();

            if (IsOwner)
            {
                CharacterMoveInput input = CreateMoveInput();
                StoreInput(input);
                Vector3 positionBeforeMove = transform.position;

                SimulateMove(input);
                StoreFixedPositionSample(positionBeforeMove, transform.position);
                StoreState(CaptureState(input.Tick));

                if (IsServer)
                {
                    lastProcessedServerTick = input.Tick;
                    authoritativeState.Value = CaptureState(input.Tick);
                }
                else
                {
                    SubmitMoveServerRpc(input);
                }

                jumpQueued = false;
            }
        }

        protected override void Tick(float deltaTime)
        {
            if (!IsSpawned)
            {
                return;
            }

            if (IsOwner)
            {
                ApplyOwnerRenderInterpolation();
                return;
            }

            if (IsProxySyncSuppressed)
            {
                SimulateProxyMove(deltaTime);
                return;
            }

            ApplyDeferredProxyTargetState();

            if (hasProxyTargetState)
            {
                transform.position = Vector3.Lerp(
                    transform.position,
                    proxyTargetState.Position,
                    1f - Mathf.Exp(-proxyInterpolationSpeed * deltaTime));
            }
        }

        private CharacterMoveInput CreateMoveInput()
        {
            CharacterMoveInput input = new CharacterMoveInput
            {
                Tick = localTick,
                DeltaTime = Time.fixedDeltaTime,
                MoveX = pendingMoveInput.x,
                MoveY = pendingMoveInput.y,
                ControlYaw = controlYaw,
                JumpPressed = jumpQueued
            };

            localTick++;
            return SanitizeInput(input);
        }

        private CharacterMoveInput SanitizeInput(CharacterMoveInput input)
        {
            Vector2 move = Vector2.ClampMagnitude(new Vector2(input.MoveX, input.MoveY), 1f);
            input.MoveX = move.x;
            input.MoveY = move.y;
            input.DeltaTime = Mathf.Clamp(input.DeltaTime, 1f / 120f, 1f / 15f);
            return input;
        }

        private void SimulateMove(CharacterMoveInput input)
        {
            if (characterController == null || !characterController.enabled)
            {
                return;
            }

            input = SanitizeInput(input);
            isGrounded = characterController.isGrounded;

            if (isGrounded && velocity.y < 0f)
            {
                velocity.y = -groundedStickForce;
            }

            Vector3 desiredDirection = new Vector3(input.MoveX, 0f, input.MoveY);

            if (orientInputToControlYaw)
            {
                desiredDirection = Quaternion.Euler(0f, input.ControlYaw, 0f) * desiredDirection;
            }

            desiredDirection = Vector3.ClampMagnitude(desiredDirection, 1f);
            Vector3 horizontalVelocity = new Vector3(velocity.x, 0f, velocity.z);
            Vector3 desiredHorizontalVelocity = desiredDirection * maxWalkSpeed;
            float horizontalAcceleration = isGrounded ? acceleration : acceleration * airControl;
            float horizontalDeceleration = isGrounded ? brakingDecelerationWalking : brakingDecelerationWalking * airControl;

            horizontalVelocity = desiredDirection.sqrMagnitude > Mathf.Epsilon
                ? Vector3.MoveTowards(horizontalVelocity, desiredHorizontalVelocity, horizontalAcceleration * input.DeltaTime)
                : Vector3.MoveTowards(horizontalVelocity, Vector3.zero, horizontalDeceleration * input.DeltaTime);

            velocity.x = horizontalVelocity.x;
            velocity.z = horizontalVelocity.z;

            if (input.JumpPressed && CanJump)
            {
                velocity.y = jumpVelocity;
                isGrounded = false;
            }
            else
            {
                velocity.y = Mathf.Max(velocity.y + gravity * input.DeltaTime, -terminalVelocity);
            }

            CollisionFlags collisionFlags = characterController.Move(velocity * input.DeltaTime);
            isGrounded = (collisionFlags & CollisionFlags.Below) != 0;

            if (isGrounded && velocity.y < 0f)
            {
                velocity.y = -groundedStickForce;
            }
        }

        private CharacterMoveState CaptureState(int tick)
        {
            return new CharacterMoveState(tick, transform.position, velocity, isGrounded);
        }

        private void StoreInput(CharacterMoveInput input)
        {
            inputBuffer[BufferIndex(input.Tick)] = input;
        }

        private void StoreState(CharacterMoveState state)
        {
            stateBuffer[BufferIndex(state.Tick)] = state;
        }

        [ServerRpc]
        private void SubmitMoveServerRpc(CharacterMoveInput input)
        {
            input = SanitizeInput(input);

            if (input.Tick <= lastProcessedServerTick)
            {
                return;
            }

            SimulateMove(input);
            lastProcessedServerTick = input.Tick;
            authoritativeState.Value = CaptureState(input.Tick);
        }

        private void HandleAuthoritativeStateChanged(CharacterMoveState previousState, CharacterMoveState newState)
        {
            if (IsServer)
            {
                return;
            }

            if (IsOwner)
            {
                ReconcileOwner(newState);
                return;
            }

            if (IsProxySyncSuppressed)
            {
                deferredProxyTargetState = newState;
                hasDeferredProxyTargetState = true;
                return;
            }

            ApplyProxyTargetState(newState);
        }

        private void ReconcileOwner(CharacterMoveState authoritativeState)
        {
            if (authoritativeState.Tick <= lastReceivedServerTick)
            {
                return;
            }

            lastReceivedServerTick = authoritativeState.Tick;
            int bufferIndex = BufferIndex(authoritativeState.Tick);
            CharacterMoveState predictedState = stateBuffer[bufferIndex];
            bool hasMatchingPrediction = predictedState.Tick == authoritativeState.Tick;

            if (hasMatchingPrediction)
            {
                float positionError = Vector3.Distance(predictedState.Position, authoritativeState.Position);
                float velocityError = Vector3.Distance(predictedState.Velocity, authoritativeState.Velocity);

                if (positionError <= reconciliationPositionTolerance && velocityError <= reconciliationVelocityTolerance)
                {
                    return;
                }
            }

            ApplyState(authoritativeState);

            for (int replayTick = authoritativeState.Tick + 1; replayTick < localTick; replayTick++)
            {
                CharacterMoveInput replayInput = inputBuffer[BufferIndex(replayTick)];

                if (replayInput.Tick != replayTick)
                {
                    break;
                }

                SimulateMove(replayInput);
                StoreState(CaptureState(replayTick));
            }

            ResetFixedPositionSamples(transform.position);
        }

        private void ApplyState(CharacterMoveState state)
        {
            bool controllerWasEnabled = characterController != null && characterController.enabled;

            if (controllerWasEnabled)
            {
                characterController.enabled = false;
            }

            transform.position = state.Position;
            velocity = state.Velocity;
            isGrounded = state.IsGrounded;
            ResetFixedPositionSamples(state.Position);

            if (controllerWasEnabled)
            {
                characterController.enabled = true;
            }
        }

        private void AllocatePredictionBuffers()
        {
            predictionBufferSize = Mathf.Max(32, predictionBufferSize);
            inputBuffer = new CharacterMoveInput[predictionBufferSize];
            stateBuffer = new CharacterMoveState[predictionBufferSize];
        }

        private void ResetPredictionState()
        {
            localTick = 0;
            lastProcessedServerTick = -1;
            lastReceivedServerTick = -1;
            hasProxyTargetState = false;
            hasDeferredProxyTargetState = false;
            proxySyncSuppressedUntil = 0f;
            velocity = Vector3.zero;
            isGrounded = characterController != null && characterController.isGrounded;
            ResetFixedPositionSamples(transform.position);
            AllocatePredictionBuffers();
        }

        private int BufferIndex(int tick)
        {
            return tick % predictionBufferSize;
        }

        public void AddKnockback(Vector3 knockbackForce)
        {
            AddKnockback(knockbackForce, 0f);
        }

        public void AddKnockback(Vector3 knockbackForce, float proxySyncSuppressionDuration)
        {
            velocity += knockbackForce;

            if (proxySyncSuppressionDuration > 0f)
            {
                SuppressProxySync(proxySyncSuppressionDuration);
            }
        }

        public void SuppressProxySync(float duration)
        {
            if (duration <= 0f || !IsSpawned || IsServer || IsOwner)
            {
                return;
            }

            proxySyncSuppressedUntil = Mathf.Max(proxySyncSuppressedUntil, Time.time + duration);
        }

        private bool IsProxySyncSuppressed => Time.time < proxySyncSuppressedUntil;

        private void SimulateProxyMove(float deltaTime)
        {
            CharacterMoveInput proxyInput = new CharacterMoveInput
            {
                Tick = -1,
                DeltaTime = deltaTime,
                MoveX = 0f,
                MoveY = 0f,
                ControlYaw = controlYaw,
                JumpPressed = false
            };

            SimulateMove(proxyInput);
        }

        private void ApplyDeferredProxyTargetState()
        {
            if (!hasDeferredProxyTargetState)
            {
                return;
            }

            ApplyProxyTargetState(deferredProxyTargetState);
            hasDeferredProxyTargetState = false;
        }

        private void ApplyProxyTargetState(CharacterMoveState state)
        {
            proxyTargetState = state;
            hasProxyTargetState = true;
            velocity = state.Velocity;
            isGrounded = state.IsGrounded;
        }

        private void ConfigureCharacterController()
        {
            if (characterController == null)
            {
                return;
            }

            float minimumStableSkinWidth = Mathf.Max(
                minimumControllerSkinWidth,
                characterController.radius * minimumControllerSkinWidthRadiusRatio);

            if (characterController.skinWidth < minimumStableSkinWidth)
            {
                characterController.skinWidth = minimumStableSkinWidth;
            }

            if (characterController.minMoveDistance > 0f)
            {
                characterController.minMoveDistance = 0f;
            }
        }

        private void ApplyOwnerRenderInterpolation()
        {
            if (!interpolateOwnerMovement || !hasFixedPositionSamples || Time.fixedDeltaTime <= 0f)
            {
                return;
            }

            if ((currentFixedPosition - previousFixedPosition).sqrMagnitude <= Mathf.Epsilon)
            {
                RestoreSimulationPosition();
                return;
            }

            float interpolationAlpha = Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime);
            transform.position = Vector3.Lerp(previousFixedPosition, currentFixedPosition, interpolationAlpha);
            hasInterpolatedTransform = true;
        }

        private void RestoreSimulationPosition()
        {
            if (!hasInterpolatedTransform)
            {
                return;
            }

            transform.position = currentFixedPosition;
            hasInterpolatedTransform = false;
        }

        private void StoreFixedPositionSample(Vector3 previousPosition, Vector3 newPosition)
        {
            float snapDistanceSqr = ownerInterpolationSnapDistance * ownerInterpolationSnapDistance;
            bool shouldSnap = ownerInterpolationSnapDistance <= 0f
                || hasFixedPositionSamples
                && (newPosition - previousPosition).sqrMagnitude > snapDistanceSqr;

            previousFixedPosition = shouldSnap ? newPosition : previousPosition;
            currentFixedPosition = newPosition;
            hasFixedPositionSamples = true;
            hasInterpolatedTransform = false;
        }

        private void ResetFixedPositionSamples(Vector3 position)
        {
            previousFixedPosition = position;
            currentFixedPosition = position;
            hasFixedPositionSamples = true;
            hasInterpolatedTransform = false;
        }
    }
}
