using Unity.Netcode;
using UnityEngine;

namespace UnscriptedEngine
{
    public abstract class UObjectComponent : NetworkBehaviour
    {
        public UObject Owner { get; private set; }

        public bool HasBegunPlay { get; private set; }
        private bool endingPlay;
        private bool spawnReady;
        private bool updateRegistrationActive;
        private uint updateGeneration;

        protected virtual bool CanTick => IsSpawned && HasBegunPlay;

        protected virtual void Awake()
        {
            RegisterOwner();
            TryBeginPlayFromOwner();
        }

        protected virtual void OnEnable()
        {
            RegisterForUpdates();
            RegisterOwner();
            TryBeginPlayFromOwner();
        }

        protected virtual void Start()
        {
            TryBeginPlayFromOwner();
        }

        public sealed override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            RegisterOwner();
            OnSpawned();
            spawnReady = true;
            RegisterForUpdates();
            TryBeginPlayFromOwner();
        }

        public sealed override void OnNetworkDespawn()
        {
            spawnReady = false;
            UnregisterFromUpdates();
            try { OnDespawned(); }
            finally
            {
                try { EndPlayInternal(); }
                finally { UnregisterOwner(); base.OnNetworkDespawn(); }
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
            try { EndPlay(); }
            finally { endingPlay = false; }
        }

        private void HandleOwnerEndedPlay(UObject owner) => EndPlayInternal();

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
            // Retain the owner end-of-play subscription while paused.
        }

        public sealed override void OnDestroy()
        {
            spawnReady = false;
            UnregisterFromUpdates();
            try { OnDestroyed(); }
            finally
            {
                try { EndPlayInternal(); }
                finally { UnregisterOwner(); base.OnDestroy(); }
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

        protected virtual void OnOwnerAssigned(UObject owner)
        {
        }

        protected virtual void OnTransformParentChanged()
        {
            RegisterOwner();
            TryBeginPlayFromOwner();
        }

        /// <summary>Call after adding/replacing an owner component without reparenting.</summary>
        public void RefreshOwner()
        {
            RegisterOwner();
            TryBeginPlayFromOwner();
        }

        private void RegisterOwner()
        {
            UObject owner = GetComponent<UObject>();

            if (owner == null)
            {
                owner = GetComponentInParent<UObject>();
            }

            if (Owner == owner)
            {
                return;
            }

            try { EndPlayInternal(); }
            finally { UnregisterOwner(); }
            Owner = owner;

            if (Owner == null)
            {
                return;
            }

            Owner.BeganPlay += HandleOwnerBeganPlay;
            Owner.EndedPlay += HandleOwnerEndedPlay;
            OnOwnerAssigned(Owner);
        }

        private void UnregisterOwner()
        {
            if (Owner != null)
            {
                Owner.BeganPlay -= HandleOwnerBeganPlay;
                Owner.EndedPlay -= HandleOwnerEndedPlay;
                Owner = null;
            }
        }

        private void HandleOwnerBeganPlay(UObject owner)
        {
            BeginPlayInternal(owner);
        }

        private void TryBeginPlayFromOwner()
        {
            RegisterOwner();

            if (IsSpawned && Owner != null && Owner.HasBegunPlay)
            {
                BeginPlayInternal(Owner);
            }
        }

        private void BeginPlayInternal(UObject owner)
        {
            if (!spawnReady || endingPlay || HasBegunPlay || owner == null || !IsSpawned || !isActiveAndEnabled || !owner.HasBegunPlay)
            {
                return;
            }

            HasBegunPlay = true;
            BeginPlay();
            RegisterForUpdates();
        }
    }
}
