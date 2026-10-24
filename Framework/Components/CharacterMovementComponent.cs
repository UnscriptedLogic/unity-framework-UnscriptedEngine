using System;
using Unity.Netcode;
using UnityEngine;

namespace UnscriptedEngine
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    [DefaultExecutionOrder(-50)]
    public class CharacterMovementComponent : UObjectComponent
    {
        protected override int TickOrder => -50;
        public struct CharacterMoveInput : INetworkSerializeByMemcpy, IEquatable<CharacterMoveInput>
        {
            public int Tick;
            public float DeltaTime;
            public float MoveX;
            public float MoveY;
            public float MoveZ;
            public float ControlYaw;
            public float BodyYaw;
            public bool JumpPressed;
            public bool IsFlying;

            public bool Equals(CharacterMoveInput other)
            {
                return Tick == other.Tick
                    && DeltaTime.Equals(other.DeltaTime)
                    && MoveX.Equals(other.MoveX)
                    && MoveY.Equals(other.MoveY)
                    && MoveZ.Equals(other.MoveZ)
                    && ControlYaw.Equals(other.ControlYaw)
                    && BodyYaw.Equals(other.BodyYaw)
                    && JumpPressed == other.JumpPressed
                    && IsFlying == other.IsFlying;
            }

            public override bool Equals(object obj)
            {
                return obj is CharacterMoveInput other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(HashCode.Combine(Tick, DeltaTime, MoveX, MoveY, MoveZ, ControlYaw, JumpPressed, IsFlying), BodyYaw);
            }
        }

        public struct CharacterMoveState : INetworkSerializeByMemcpy, IEquatable<CharacterMoveState>
        {
            public int Tick;
            public Vector3 Position;
            public Vector3 Velocity;
            public float BodyYaw;
            public bool IsGrounded;
            public bool IsFlying;

            public CharacterMoveState(int tick, Vector3 position, Vector3 velocity, bool isGrounded, bool isFlying, float bodyYaw = 0f)
            {
                Tick = tick;
                Position = position;
                Velocity = velocity;
                BodyYaw = bodyYaw;
                IsGrounded = isGrounded;
                IsFlying = isFlying;
            }

            public bool Equals(CharacterMoveState other)
            {
                return Tick == other.Tick
                    && Position.Equals(other.Position)
                    && Velocity.Equals(other.Velocity)
                    && BodyYaw.Equals(other.BodyYaw)
                    && IsGrounded == other.IsGrounded
                    && IsFlying == other.IsFlying;
            }

            public override bool Equals(object obj)
            {
                return obj is CharacterMoveState other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Tick, Position, Velocity, IsGrounded, IsFlying, BodyYaw);
            }
        }

        [Header("Walking")]
        [SerializeField] private float maxWalkSpeed = 6f;
        [SerializeField] private float acceleration = 24f;
        [SerializeField] private float brakingDecelerationWalking = 32f;
        [SerializeField] private float airControl = 0.35f;
        [SerializeField] private bool orientInputToControlYaw = true;

        [Header("Collision Speed Loss")]
        [Tooltip("Reduce horizontal velocity on blocking surfaces. Applied in owner prediction and server simulation; walkable ground is ignored.")]
        [SerializeField] private bool reduceSpeedOnCollision;
        [Tooltip("Fraction of speed removed by a head-on hit. 1 stops horizontal movement completely; 0 disables speed loss. Strongest contact is applied once per movement step.")]
        [SerializeField, Range(0f, 1f)] private float collisionSpeedLoss = 1f;
        [Tooltip("Shapes loss by impact angle: loss = strength * opposition^power. Opposition is 1 head-on and 0 parallel. Higher powers preserve more speed on glancing hits.")]
        [SerializeField, Min(0.1f)] private float collisionAnglePower = 2f;
        [Tooltip("Ignore contacts below this opposing-normal dot product (0 = parallel, 1 = head-on). Avoids speed loss from very shallow scrapes.")]
        [SerializeField, Range(0f, 0.99f)] private float minimumCollisionOpposition = 0.05f;
        private float collisionRetention = 1f;
        private bool collectingCollisionLoss;
        public event Action<float> HorizontalCollisionSpeedReduced;

        [Header("Flying")]
        [SerializeField] private bool isFlying;
        [SerializeField] private float maxFlySpeed = 6f;
        [SerializeField] private float flightAcceleration = 24f;
        [SerializeField] private float brakingDecelerationFlying = 32f;

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
        private float pendingVerticalMoveInput;
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
        private readonly NetworkVariable<bool> movementLocked = new NetworkVariable<bool>(false);
        public bool IsMovementLocked => movementLocked.Value;

        /// <summary>Server-authoritative input lock, enforced during simulation and replay.</summary>
        public void SetMovementLocked(bool locked)
        {
            if (!IsServer || !IsSpawned) return;
            movementLocked.Value = locked;
            if (locked)
            {
                pendingMoveInput = Vector2.zero;
                pendingVerticalMoveInput = 0f;
                jumpQueued = false;
                velocity = Vector3.zero;
            }
        }

        public Vector3 SimulationPosition => hasInterpolatedTransform ? currentFixedPosition : transform.position;
        public float WalkAcceleration => acceleration;

        public NetworkVariable<CharacterMoveState> AuthoritativeState => authoritativeState;

        public bool IsGrounded => isGrounded;

        public bool IsFlying => isFlying;

        public bool IsFalling => !isGrounded && !isFlying;

        public bool CanJump => isGrounded && !isFlying;

        public float MaxWalkSpeed
        {
            get => maxWalkSpeed;
            set => maxWalkSpeed = Mathf.Max(0f, value);
        }

        public float MaxFlySpeed
        {
            get => maxFlySpeed;
            set => maxFlySpeed = Mathf.Max(0f, value);
        }

        public void SetMovementInput(Vector2 input)
        {
            pendingMoveInput = Vector2.ClampMagnitude(input, 1f);
        }

        public void SetVerticalMovementInput(float input)
        {
            pendingVerticalMoveInput = Mathf.Clamp(input, -1f, 1f);
        }

        public void AddMovementInput(Vector3 worldDirection, float scale = 1f)
        {
            Vector3 flatDirection = new Vector3(worldDirection.x, 0f, worldDirection.z);

            if (flatDirection.sqrMagnitude > Mathf.Epsilon)
            {
                Vector2 input = new Vector2(flatDirection.normalized.x, flatDirection.normalized.z) * scale;
                pendingMoveInput = Vector2.ClampMagnitude(pendingMoveInput + input, 1f);
            }

            if (Mathf.Abs(worldDirection.y) > Mathf.Epsilon)
            {
                pendingVerticalMoveInput = Mathf.Clamp(
                    pendingVerticalMoveInput + Mathf.Sign(worldDirection.y) * Mathf.Abs(scale),
                    -1f,
                    1f);
            }
        }

        public void SetControlYaw(float yawDegrees)
        {
            controlYaw = yawDegrees;
        }

        public void SetGravity(float gravity)
        {
            this.gravity = gravity;
        }

        public void SetFlying(bool flying)
        {
            if (isFlying == flying)
            {
                return;
            }

            isFlying = flying;

            if (isFlying)
            {
                isGrounded = false;
                velocity.y = 0f;
            }
            else
            {
                pendingVerticalMoveInput = 0f;
            }
        }

        public void ToggleFlying()
        {
            SetFlying(!isFlying);
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
            maxFlySpeed = Mathf.Max(0f, maxFlySpeed);
            flightAcceleration = Mathf.Max(0f, flightAcceleration);
            brakingDecelerationFlying = Mathf.Max(0f, brakingDecelerationFlying);

            if (characterController == null)
            {
                characterController = GetComponent<CharacterController>();
            }
        }

        protected override void OnSpawned()
        {
            base.OnSpawned();
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

        protected override void OnDespawned()
        {
            authoritativeState.OnValueChanged -= HandleAuthoritativeStateChanged;
            base.OnDespawned();
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
                // Notify gameplay only for a fresh owner step, never during replay.
                if (collisionRetention < 1f)
                    HorizontalCollisionSpeedReduced?.Invoke(collisionRetention);
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
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.Euler(0f, proxyTargetState.BodyYaw, 0f),
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
                MoveZ = pendingVerticalMoveInput,
                ControlYaw = controlYaw,
                BodyYaw = transform.eulerAngles.y,
                JumpPressed = jumpQueued,
                IsFlying = isFlying
            };

            localTick++;
            return SanitizeInput(input);
        }

        private CharacterMoveInput SanitizeInput(CharacterMoveInput input)
        {
            if (movementLocked.Value)
            {
                input.MoveX = input.MoveY = input.MoveZ = 0f;
                input.JumpPressed = false;
                input.IsFlying = false;
            }
            Vector2 move = Vector2.ClampMagnitude(new Vector2(input.MoveX, input.MoveY), 1f);
            input.MoveX = move.x;
            input.MoveY = move.y;
            input.MoveZ = Mathf.Clamp(input.MoveZ, -1f, 1f);
            input.DeltaTime = Mathf.Clamp(input.DeltaTime, 1f / 120f, 1f / 15f);
            input.BodyYaw = float.IsNaN(input.BodyYaw) || float.IsInfinity(input.BodyYaw)
                ? transform.eulerAngles.y : Mathf.Repeat(input.BodyYaw, 360f);
            return input;
        }

        private void SimulateMove(CharacterMoveInput input)
        {
            collisionRetention = 1f;
            if (characterController == null || !characterController.enabled)
            {
                return;
            }

            input = SanitizeInput(input);
            if (movementLocked.Value)
            {
                velocity.x = velocity.z = 0f;
                velocity.y = Mathf.Min(velocity.y, 0f);
            }
            isGrounded = characterController.isGrounded;
            isFlying = input.IsFlying;

            if (isFlying)
            {
                SimulateFlyingMove(input);
                return;
            }

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

            collectingCollisionLoss = reduceSpeedOnCollision;
            CollisionFlags collisionFlags;
            try
            {
                collisionFlags = characterController.Move(velocity * input.DeltaTime);
            }
            finally
            {
                collectingCollisionLoss = false;
            }
            velocity.x *= collisionRetention;
            velocity.z *= collisionRetention;
            isGrounded = (collisionFlags & CollisionFlags.Below) != 0;

            if (isGrounded && velocity.y < 0f)
            {
                velocity.y = -groundedStickForce;
            }
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (!collectingCollisionLoss || hit.normal.y >=
                Mathf.Cos(characterController.slopeLimit * Mathf.Deg2Rad)) return;

            Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
            Vector3 normal = Vector3.ProjectOnPlane(hit.normal, Vector3.up);
            if (horizontal.sqrMagnitude < 0.000001f || normal.sqrMagnitude < 0.000001f) return;

            float opposition = Mathf.Clamp01(-Vector3.Dot(horizontal.normalized, normal.normalized));
            if (opposition <= minimumCollisionOpposition) return;
            float loss = Mathf.Clamp01(collisionSpeedLoss) *
                Mathf.Pow(opposition, Mathf.Max(0.1f, collisionAnglePower));
            collisionRetention = Mathf.Min(collisionRetention, 1f - loss);
        }

        private void SimulateFlyingMove(CharacterMoveInput input)
        {
            Vector3 desiredDirection = new Vector3(input.MoveX, input.MoveZ, input.MoveY);

            if (orientInputToControlYaw)
            {
                Vector3 horizontalDirection = Quaternion.Euler(0f, input.ControlYaw, 0f)
                    * new Vector3(input.MoveX, 0f, input.MoveY);
                desiredDirection = new Vector3(horizontalDirection.x, input.MoveZ, horizontalDirection.z);
            }

            desiredDirection = Vector3.ClampMagnitude(desiredDirection, 1f);
            Vector3 desiredVelocity = desiredDirection * maxFlySpeed;

            velocity = desiredDirection.sqrMagnitude > Mathf.Epsilon
                ? Vector3.MoveTowards(velocity, desiredVelocity, flightAcceleration * input.DeltaTime)
                : Vector3.MoveTowards(velocity, Vector3.zero, brakingDecelerationFlying * input.DeltaTime);

            CollisionFlags collisionFlags = characterController.Move(velocity * input.DeltaTime);
            isGrounded = (collisionFlags & CollisionFlags.Below) != 0;
        }

        private CharacterMoveState CaptureState(int tick)
        {
            return new CharacterMoveState(tick, transform.position, velocity, isGrounded, isFlying, transform.eulerAngles.y);
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

            // Body facing is independent of movement/control yaw in free orbit.
            // Apply only on the receiving server, never during owner prediction
            // or reconciliation replay, which must preserve current local look.
            transform.rotation = Quaternion.Euler(0f, input.BodyYaw, 0f);
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
            isFlying = state.IsFlying;
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
            pendingVerticalMoveInput = 0f;
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
                MoveZ = 0f,
                ControlYaw = controlYaw,
                JumpPressed = false,
                IsFlying = isFlying
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
            isFlying = state.IsFlying;
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
