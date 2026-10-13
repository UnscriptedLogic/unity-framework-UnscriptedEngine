using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

namespace UnscriptedEngine
{
    public class UInteractableComponent : UObjectComponent
    {
        public event Action<UInteractArgs> OnButtonPressedServer;
        public event Action<UInteractArgs> OnButtonPressedClient;
        public event Action<UInteractArgs> InteractionStarted;
        public event Action<UInteractArgs> InteractionInterrupted;
        public event Action<UInteractArgs> InteractionCompleted;
        public event Action<float> ProgressChanged;
        public event Action<GameObject, uint> InteractionRejected;

        [Header("Interaction")]
        [SerializeField] private InteractionMode interactionMode = InteractionMode.Instant;
        [SerializeField, Min(0f)] private float duration = 2f;
        [SerializeField] private bool repeatable = true;
        [SerializeField, Min(0f)] private float maximumUseDistance = 3f;
        [Tooltip("Optional range-check point. Defaults to this transform.")]
        [SerializeField] private Transform interactionPoint;
        [Tooltip("Timed sessions expire without keep-alive messages. Must exceed the interactor heartbeat interval.")]
        [SerializeField, Min(0.5f)] private float sessionTimeout = 1.5f;

        [Header("Lifecycle Events")]
        [SerializeField] private UnityEvent<GameObject> onInteractionStarted;
        [SerializeField] private UnityEvent<GameObject> onInteractionInterrupted;
        [SerializeField] private UnityEvent<GameObject> onInteractionCompleted;
        [SerializeField] private UnityEvent<float> onProgressChanged;

        // Keep existing serialized names so prefab wiring survives migration.
        // Button events now mean successful completion, including for timed modes.
        [Header("Button Events")]
        [SerializeField] private UnityEvent onButtonPressedServer;
        [SerializeField] private UnityEvent onButtonPressedClient;
        [SerializeField] private UnityEvent onButtonInteractedLocally;
        [Header("Button Events With Source")]
        [SerializeField] private UnityEvent<GameObject> onButtonPressedServerWithSource;
        [SerializeField] private UnityEvent<GameObject> onButtonPressedClientWithSource;
        [SerializeField] private UnityEvent<GameObject> onButtonInteractedLocallyWithSource;

        private readonly InteractionProcess process = new InteractionProcess();
        private readonly NetworkVariable<InteractionSnapshot> snapshot = new NetworkVariable<InteractionSnapshot>();
        private GameObject activeSource;
        private ulong activeClientId;
        private uint activeSessionId;
        private float lastHeartbeat;
        private bool dispatchingEvents;

        public InteractionMode Mode => interactionMode;
        public float Progress01 => IsSpawned ? snapshot.Value.Progress : process.Progress01;
        public InteractionState State => IsSpawned ? snapshot.Value.State : process.State;
        public bool IsActive => State == InteractionState.Active;
        public bool IsCompleted => State == InteractionState.Completed;
        public GameObject ActiveInteractor => IsSpawned ? ResolveSource(snapshot.Value.Source) : activeSource;
        protected override bool CanTick => !IsSpawned || IsServer;
        private bool CanRunAuthoritativeLogic => !IsSpawned || IsServer;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            snapshot.OnValueChanged += HandleSnapshotChanged;
            if (IsServer) PublishSnapshot();
            NotifyProgress(Progress01);
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer) InterruptAuthoritative();
            snapshot.OnValueChanged -= HandleSnapshotChanged;
            process.Reset();
            activeSource = null;
            base.OnNetworkDespawn();
        }

        protected override void OnDisable()
        {
            if (CanRunAuthoritativeLogic) InterruptAuthoritative();
            base.OnDisable();
        }

        /// <summary>Returns local acceptance or whether a request was queued. Server acceptance is asynchronous.</summary>
        public bool TryBeginInteraction(GameObject interactor, uint sessionId = 0)
        {
            if (!isActiveAndEnabled || dispatchingEvents) return false;
            if (CanRunAuthoritativeLogic) return BeginAuthoritative(interactor, Unity.Netcode.NetworkManager.ServerClientId, sessionId);
            if (!TryCreateSourceReference(interactor, out NetworkObjectReference sourceReference)) return false;
            BeginInteractionRpc(sourceReference, sessionId);
            return true;
        }

        public void EndInteraction(GameObject interactor, uint sessionId = 0)
        {
            if (CanRunAuthoritativeLogic)
            {
                if (activeSource == NormalizeSource(interactor) && activeSessionId == sessionId) InterruptAuthoritative();
                return;
            }
            if (TryCreateSourceReference(interactor, out NetworkObjectReference sourceReference))
                EndInteractionRpc(sourceReference, sessionId);
        }

        /// <summary>Call while held. Timed sessions expire if their interactor stops updating.</summary>
        public void KeepInteractionAlive(GameObject interactor, uint sessionId = 0)
        {
            if (CanRunAuthoritativeLogic)
            {
                if (activeSource == NormalizeSource(interactor) && activeSessionId == sessionId) lastHeartbeat = Time.unscaledTime;
                return;
            }
            if (TryCreateSourceReference(interactor, out NetworkObjectReference sourceReference))
                KeepInteractionAliveRpc(sourceReference, sessionId);
        }

        /// <summary>Server/offline reset. Non-authoritative clients cannot reset another player's progress.</summary>
        public void ResetProgress()
        {
            if (!CanRunAuthoritativeLogic || dispatchingEvents) return;
            InterruptAuthoritative();
            process.Reset();
            PublishSnapshot();
        }

        // Legacy entry points enter the same lifecycle rather than bypassing timed interactions.
        public void Interact(GameObject interactorSource = null) => TryBeginInteraction(interactorSource);
        public void InteractServer(GameObject interactorSource = null) => TryBeginInteraction(interactorSource);
        public void InteractClient(GameObject interactorSource = null)
        {
            if (CanRunAuthoritativeLogic) TryBeginInteraction(interactorSource);
        }

        /// <summary>Override for permissions, inventory requirements, or other gameplay rules.</summary>
        protected virtual bool CanInteract(GameObject interactor)
        {
            if (!isActiveAndEnabled) return false;
            // Null sources remain supported for server/offline instant prototyping.
            if (interactor == null) return interactionMode == InteractionMode.Instant;
            if (!interactor.activeInHierarchy) return false;
            if (IsSpawned && interactor.TryGetComponent(out UPawn pawn) && !pawn.IsPossessed) return false;
            Vector3 point = interactionPoint != null ? interactionPoint.position : transform.position;
            return (interactor.transform.position - point).sqrMagnitude <= maximumUseDistance * maximumUseDistance;
        }

        protected override void Tick(float deltaTime)
        {
            if (process.State != InteractionState.Active) return;
            if (!IsSessionValid())
            {
                InterruptAuthoritative();
                return;
            }
            AdvanceAuthoritative(deltaTime);
        }

        private bool BeginAuthoritative(GameObject interactor, ulong clientId, uint sessionId)
        {
            if (dispatchingEvents || !CanInteract(interactor) ||
                !process.TryBegin(interactionMode, duration, repeatable)) return false;
            activeSource = NormalizeSource(interactor);
            activeClientId = clientId;
            activeSessionId = sessionId;
            lastHeartbeat = Time.unscaledTime;
            PublishSnapshot();
            BroadcastLifecycle(InteractionState.Active, activeSource);
            AdvanceAuthoritative(0f);
            return true;
        }

        private void AdvanceAuthoritative(float deltaTime)
        {
            bool completed = process.Advance(deltaTime);
            GameObject source = activeSource;
            if (completed) activeSource = null;
            PublishSnapshot();
            if (!completed) return;
            BroadcastLifecycle(InteractionState.Completed, source);
            InvokeServerCompletion(source);
        }

        private void InterruptAuthoritative()
        {
            if (process.State != InteractionState.Active) return;
            GameObject source = activeSource;
            process.Interrupt();
            activeSource = null;
            PublishSnapshot();
            BroadcastLifecycle(InteractionState.Paused, source);
        }

        private bool IsSessionValid()
        {
            if (activeSource == null || !CanInteract(activeSource)) return false;
            if (Time.unscaledTime - lastHeartbeat > Mathf.Max(0.5f, sessionTimeout)) return false;
            if (!IsSpawned) return true;
            NetworkObject sourceObject = activeSource.GetComponentInParent<NetworkObject>();
            if (sourceObject == null || !sourceObject.IsSpawned) return false;
            return activeClientId == Unity.Netcode.NetworkManager.ServerClientId ||
                (NetworkManager.ConnectedClients.ContainsKey(activeClientId) &&
                 sourceObject.OwnerClientId == activeClientId);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void BeginInteractionRpc(NetworkObjectReference sourceReference, uint sessionId, RpcParams rpcParams = default)
        {
            bool accepted = TryValidateSender(sourceReference, rpcParams.Receive.SenderClientId,
                out GameObject source) && BeginAuthoritative(source, rpcParams.Receive.SenderClientId, sessionId);
            if (!accepted) RejectInteractionRpc(sourceReference, rpcParams.Receive.SenderClientId, sessionId);
        }

        [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
        private void RejectInteractionRpc(NetworkObjectReference sourceReference, ulong requestingClient, uint sessionId)
        {
            if (NetworkManager.LocalClientId == requestingClient)
                InteractionRejected?.Invoke(ResolveSource(sourceReference), sessionId);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void EndInteractionRpc(NetworkObjectReference sourceReference, uint sessionId, RpcParams rpcParams = default)
        {
            if (TryValidateSessionSender(sourceReference, rpcParams.Receive.SenderClientId, sessionId))
                InterruptAuthoritative();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void KeepInteractionAliveRpc(NetworkObjectReference sourceReference, uint sessionId, RpcParams rpcParams = default)
        {
            if (TryValidateSessionSender(sourceReference, rpcParams.Receive.SenderClientId, sessionId))
                lastHeartbeat = Time.unscaledTime;
        }

        private bool TryValidateSender(NetworkObjectReference reference, ulong sender, out GameObject source)
        {
            source = null;
            if (!reference.TryGet(out NetworkObject sourceObject) || !sourceObject.IsSpawned ||
                sourceObject.OwnerClientId != sender) return false;
            source = sourceObject.gameObject;
            return source.activeInHierarchy;
        }

        private bool TryValidateSessionSender(NetworkObjectReference reference, ulong sender, uint sessionId)
        {
            return sessionId == activeSessionId && sender == activeClientId && TryValidateSender(reference, sender, out GameObject source) &&
                source == activeSource;
        }

        private void PublishSnapshot()
        {
            if (IsSpawned)
            {
                NetworkObjectReference sourceReference = default;
                TryCreateSourceReference(activeSource, out sourceReference);
                snapshot.Value = new InteractionSnapshot
                {
                    Progress = process.Progress01, State = process.State, Source = sourceReference
                };
            }
            else NotifyProgress(process.Progress01);
        }

        private void HandleSnapshotChanged(InteractionSnapshot previous, InteractionSnapshot current)
        {
            if (previous.Progress != current.Progress) NotifyProgress(current.Progress);
        }

        private void NotifyProgress(float progress)
        {
            bool wasDispatching = dispatchingEvents;
            dispatchingEvents = true;
            try
            {
                ProgressChanged?.Invoke(progress);
                onProgressChanged?.Invoke(progress);
            }
            finally { dispatchingEvents = wasDispatching; }
        }

        private void BroadcastLifecycle(InteractionState state, GameObject source)
        {
            if (!IsSpawned)
            {
                InvokeLifecycle(state, source, true, activeSessionId);
                return;
            }
            TryCreateSourceReference(source, out NetworkObjectReference reference);
            LifecycleRpc(state, reference, activeSessionId);
        }

        [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
        private void LifecycleRpc(InteractionState state, NetworkObjectReference sourceReference, uint sessionId)
        {
            GameObject source = ResolveSource(sourceReference);
            bool localInteractor = sourceReference.TryGet(out NetworkObject sourceObject) && sourceObject.IsOwner;
            InvokeLifecycle(state, source, localInteractor, sessionId);
        }

        private void InvokeLifecycle(InteractionState state, GameObject source, bool localInteractor, uint sessionId)
        {
            dispatchingEvents = true;
            try
            {
                UInteractArgs args = new UInteractArgs { Interactable = this, InteractorSource = source, SessionId = sessionId };
                switch (state)
                {
                    case InteractionState.Active:
                        InteractionStarted?.Invoke(args);
                        onInteractionStarted?.Invoke(source);
                        break;
                    case InteractionState.Paused:
                        InteractionInterrupted?.Invoke(args);
                        onInteractionInterrupted?.Invoke(source);
                        break;
                    case InteractionState.Completed:
                        InteractionCompleted?.Invoke(args);
                        onInteractionCompleted?.Invoke(source);
                        OnButtonPressedClient?.Invoke(args);
                        onButtonPressedClient?.Invoke();
                        onButtonPressedClientWithSource?.Invoke(source);
                        if (localInteractor)
                        {
                            onButtonInteractedLocally?.Invoke();
                            onButtonInteractedLocallyWithSource?.Invoke(source);
                        }
                        break;
                }
            }
            finally { dispatchingEvents = false; }
        }

        private void InvokeServerCompletion(GameObject source)
        {
            dispatchingEvents = true;
            try
            {
                OnButtonPressedServer?.Invoke(new UInteractArgs { Interactable = this, InteractorSource = source, SessionId = activeSessionId });
                onButtonPressedServer?.Invoke();
                onButtonPressedServerWithSource?.Invoke(source);
            }
            finally { dispatchingEvents = false; }
        }

        private static GameObject NormalizeSource(GameObject source)
        {
            if (source == null) return null;
            NetworkObject sourceObject = source.GetComponentInParent<NetworkObject>();
            return sourceObject != null ? sourceObject.gameObject : source;
        }

        private static bool TryCreateSourceReference(GameObject source, out NetworkObjectReference reference)
        {
            reference = default;
            NetworkObject sourceObject = source != null ? source.GetComponentInParent<NetworkObject>() : null;
            if (sourceObject == null || !sourceObject.IsSpawned) return false;
            reference = new NetworkObjectReference(sourceObject);
            return true;
        }

        private static GameObject ResolveSource(NetworkObjectReference reference)
        {
            return reference.TryGet(out NetworkObject sourceObject) ? sourceObject.gameObject : null;
        }

        public struct InteractionSnapshot : INetworkSerializable, IEquatable<InteractionSnapshot>
        {
            public float Progress;
            public InteractionState State;
            public NetworkObjectReference Source;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref Progress);
                serializer.SerializeValue(ref State);
                serializer.SerializeValue(ref Source);
            }

            public bool Equals(InteractionSnapshot other)
            {
                return Progress.Equals(other.Progress) && State == other.State && Source.Equals(other.Source);
            }
        }

        public class UInteractArgs : EventArgs
        {
            public uint SessionId;
            public GameObject InteractorSource;
            public UInteractableComponent Interactable;
        }
    }
}


