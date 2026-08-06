using System;
using Unity.Netcode;
using UnityEngine;

namespace Framework
{
    public abstract class UObject : NetworkBehaviour
    {
        public event Action<UObject> BeganPlay;

        public bool HasBegunPlay { get; private set; }

        protected UGameMode GameMode { get; private set; }

        protected UGameState GameState { get; private set; }

        protected virtual bool CanTick => HasBegunPlay;

        protected virtual void Awake()
        {
            TryBeginPlayFromInitializedGameMode();
        }

        protected virtual void OnEnable()
        {
            UGameState.GameStateInitialized += HandleGameStateInitialized;
            TryBeginPlayFromInitializedGameState();
        }

        protected virtual void Start()
        {
            TryBeginPlayFromInitializedGameState();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            TryBeginPlayFromInitializedGameState();
        }

        protected virtual void Update()
        {
            if (CanTick)
            {
                Tick(Time.deltaTime);
            }
        }

        protected virtual void FixedUpdate()
        {
            if (CanTick)
            {
                FixedTick(Time.fixedDeltaTime);
            }
        }

        protected virtual void OnDisable()
        {
            UGameState.GameStateInitialized -= HandleGameStateInitialized;
        }

        public override void OnDestroy()
        {
            UGameState.GameStateInitialized -= HandleGameStateInitialized;
            base.OnDestroy();
        }

        protected virtual void BeginPlay()
        {
        }

        protected virtual void Tick(float deltaTime)
        {
        }

        protected virtual void FixedTick(float fixedDeltaTime)
        {
        }

        protected virtual void OnGameModeInitialized(UGameMode gameMode)
        {
        }

        protected virtual void OnGameStateInitialized(UGameState gameState)
        {
        }

        private void HandleGameStateInitialized(UGameState gameState)
        {
            BeginPlayInternal(gameState);
        }

        private void TryBeginPlayFromInitializedGameMode()
        {
            TryBeginPlayFromInitializedGameState();
        }

        private void TryBeginPlayFromInitializedGameState()
        {
            if (UGameState.TryGetInitializedGameState(out UGameState initializedGameState))
            {
                BeginPlayInternal(initializedGameState);
            }
        }

        private void BeginPlayInternal(UGameState gameState)
        {
            if (HasBegunPlay || gameState == null || !IsSpawned)
            {
                return;
            }

            GameState = gameState;

            if (UGameMode.TryGetInitializedGameMode(out UGameMode initializedGameMode))
            {
                GameMode = initializedGameMode;
            }

            HasBegunPlay = true;
            OnGameStateInitialized(gameState);

            if (GameMode != null)
            {
                OnGameModeInitialized(GameMode);
            }

            BeginPlay();
            BeganPlay?.Invoke(this);
        }
    }
}
