using System;
using Unity.Netcode;
using UnityEngine;

namespace UnscriptedEngine
{
    public abstract class UObject : NetworkBehaviour
    {
        public event Action<UObject> BeganPlay;
        public event Action<UObject> EndedPlay;
        private bool endingPlay;
        private bool spawnReady;
        private bool updateRegistrationActive;
        private uint updateGeneration;

        public bool HasBegunPlay { get; private set; }

        protected UGameMode GameMode { get; private set; }

        protected UGameState GameState { get; private set; }

        protected virtual bool CanTick => IsSpawned && HasBegunPlay;

        protected virtual void Awake()
        {
            TryBeginPlayFromInitializedGameMode();
        }

        protected virtual void OnEnable()
        {
            RegisterForUpdates();
            UGameState.GameStateInitialized += HandleGameStateInitialized;
            TryBeginPlayFromInitializedGameState();
        }

        protected virtual void Start()
        {
            TryBeginPlayFromInitializedGameState();
        }

        public sealed override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            OnSpawned();
            spawnReady = true;
            RegisterForUpdates();
            TryBeginPlayFromInitializedGameState();
        }

        public sealed override void OnNetworkDespawn()
        {
            spawnReady = false;
            UnregisterFromUpdates();
            try { OnDespawned(); }
            finally
            {
                try { EndPlayInternal(); }
                finally { base.OnNetworkDespawn(); }
            }
        }

        protected virtual void OnSpawned() { }
        protected virtual void OnDespawned() { }
        protected virtual void OnDestroyed() { }
        protected virtual void EndPlay() { }

        private void EndPlayInternal()
        {
            if (!HasBegunPlay) return;
            UnregisterFromUpdates();
            HasBegunPlay = false;
            endingPlay = true;
            try
            {
                try { EndPlay(); }
                finally { EventBus.Notify(EndedPlay, this); }
            }
            finally
            {
                GameMode = null;
                GameState = null;
                endingPlay = false;
            }
        }

        private void DispatchUpdate(float deltaTime)
        {
            if (updateRegistrationActive && CanTick)
            {
                uint generation = updateGeneration;
                Tick(deltaTime);
                if (generation == updateGeneration && updateRegistrationActive && CanTick && IsSpawned && IsServer) ServerTick(deltaTime);
                if (generation == updateGeneration && updateRegistrationActive && CanTick && IsSpawned && IsClient) ClientTick(deltaTime);
            }
        }

        private void DispatchFixedUpdate(float fixedDeltaTime)
        {
            if (updateRegistrationActive && CanTick)
            {
                uint generation = updateGeneration;
                FixedTick(fixedDeltaTime);
                if (generation == updateGeneration && updateRegistrationActive && CanTick && IsSpawned && IsServer) ServerFixedTick(fixedDeltaTime);
                if (generation == updateGeneration && updateRegistrationActive && CanTick && IsSpawned && IsClient) ClientFixedTick(fixedDeltaTime);
            }
        }

        protected virtual void OnDisable()
        {
            UnregisterFromUpdates();
            UGameState.GameStateInitialized -= HandleGameStateInitialized;
        }

        public sealed override void OnDestroy()
        {
            spawnReady = false;
            UnregisterFromUpdates();
            UGameState.GameStateInitialized -= HandleGameStateInitialized;
            try { OnDestroyed(); }
            finally
            {
                try { EndPlayInternal(); }
                finally { base.OnDestroy(); }
            }
        }

        private void UnregisterFromUpdates()
        {
            updateRegistrationActive = false;
            unchecked { updateGeneration++; }
            UpdateManager.Unregister(this);
        }

        protected virtual int TickOrder => 0;

        private void RegisterForUpdates()
        {
            if (!isActiveAndEnabled) return;
            updateRegistrationActive = true;
            UpdateManager.RegisterUpdate(this, DispatchUpdate, TickOrder);
            UpdateManager.RegisterFixedUpdate(this, DispatchFixedUpdate, TickOrder);
        }

        /// <summary>Authority work on servers and hosts, after the shared Tick.</summary>
        protected virtual void ServerTick(float deltaTime) { }
        /// <summary>Presentation/prediction on clients and hosts. Check IsOwner for local-player work.</summary>
        protected virtual void ClientTick(float deltaTime) { }
        protected virtual void ServerFixedTick(float fixedDeltaTime) { }
        protected virtual void ClientFixedTick(float fixedDeltaTime) { }

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
            if (!spawnReady || endingPlay || HasBegunPlay || gameState == null || !IsSpawned || !isActiveAndEnabled)
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
            RegisterForUpdates();
            EventBus.Notify(BeganPlay, this);
        }
    }
}
