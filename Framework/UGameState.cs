using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Framework
{
    public abstract class UGameState : NetworkBehaviour
    {
        public static event Action<UGameState> GameStateInitialized;

        public struct ConnectedPlayerState : INetworkSerializeByMemcpy, IEquatable<ConnectedPlayerState>
        {
            public ulong ClientId;
            public ulong PlayerNetworkObjectId;

            public ConnectedPlayerState(ulong clientId, NetworkObject playerObject)
            {
                ClientId = clientId;
                PlayerNetworkObjectId = playerObject.NetworkObjectId;
            }

            public bool Equals(ConnectedPlayerState other)
            {
                return ClientId == other.ClientId && PlayerNetworkObjectId == other.PlayerNetworkObjectId;
            }

            public override bool Equals(object obj)
            {
                return obj is ConnectedPlayerState other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((int)ClientId * 397) ^ PlayerNetworkObjectId.GetHashCode();
                }
            }
        }

        public static UGameState Instance { get; private set; }

        private readonly NetworkVariable<bool> frameworkInitialized =
            new NetworkVariable<bool>(
                false,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private readonly NetworkList<ConnectedPlayerState> connectedPlayers = new();

        private bool hasRaisedGameStateInitialized;

        public bool IsFrameworkInitialized => IsSpawned && frameworkInitialized.Value;

        public NetworkList<ConnectedPlayerState> ConnectedPlayers => connectedPlayers;

        public int ConnectedPlayerCount => connectedPlayers.Count;

        protected virtual void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"Multiple {nameof(UGameState)} instances found. Keeping the first instance.");
                return;
            }

            Instance = this;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            frameworkInitialized.OnValueChanged += HandleFrameworkInitializedChanged;

            if (frameworkInitialized.Value)
            {
                RaiseGameStateInitialized();
            }
        }

        public override void OnNetworkDespawn()
        {
            frameworkInitialized.OnValueChanged -= HandleFrameworkInitializedChanged;
            base.OnNetworkDespawn();
        }

        public override void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                GameStateInitialized = null;
            }

            base.OnDestroy();
        }

        public static bool TryGetInitializedGameState(out UGameState gameState)
        {
            gameState = Instance;
            return gameState != null && gameState.IsFrameworkInitialized;
        }

        public bool SetFrameworkInitialized()
        {
            if (!IsServer)
            {
                Debug.LogWarning($"Only the server can initialize {nameof(UGameState)}.");
                return false;
            }

            if (frameworkInitialized.Value)
            {
                return true;
            }

            frameworkInitialized.Value = true;
            RaiseGameStateInitialized();
            return true;
        }

        public void AddOrUpdateConnectedPlayer(ulong clientId, NetworkObject playerObject)
        {
            if (!IsServer)
            {
                Debug.LogWarning($"Only the server can register connected players in {nameof(UGameState)}.");
                return;
            }

            if (playerObject == null)
            {
                Debug.LogWarning($"Cannot register client {clientId}: player object is null.");
                return;
            }

            if (!playerObject.IsSpawned)
            {
                Debug.LogWarning($"Cannot register client {clientId}: player object is not spawned.");
                return;
            }

            ConnectedPlayerState playerState = new ConnectedPlayerState(clientId, playerObject);
            int playerIndex = IndexOfConnectedPlayer(clientId);

            if (playerIndex >= 0)
            {
                connectedPlayers[playerIndex] = playerState;
                return;
            }

            connectedPlayers.Add(playerState);
        }

        public bool TryGetConnectedPlayer(ulong clientId, out NetworkObject playerObject)
        {
            playerObject = null;

            return TryGetConnectedPlayerState(clientId, out ConnectedPlayerState playerState)
                && TryResolveNetworkObject(playerState.PlayerNetworkObjectId, out playerObject);
        }

        public NetworkObject GetConnectedPlayer(ulong clientId)
        {
            return TryGetConnectedPlayer(clientId, out NetworkObject playerObject)
                ? playerObject
                : null;
        }

        public GameObject GetConnectedPlayerPrefabInstance(ulong clientId)
        {
            return TryGetConnectedPlayer(clientId, out NetworkObject playerObject)
                ? playerObject.gameObject
                : null;
        }

        public bool TryGetConnectedPlayerComponent<T>(ulong clientId, out T component) where T : Component
        {
            component = null;

            if (!TryGetConnectedPlayer(clientId, out NetworkObject playerObject) || playerObject == null)
            {
                return false;
            }

            component = playerObject.GetComponent<T>();
            return component != null;
        }

        public bool IsPlayerConnected(ulong clientId)
        {
            return IndexOfConnectedPlayer(clientId) >= 0;
        }

        public bool RemoveConnectedPlayer(ulong clientId)
        {
            if (!IsServer)
            {
                Debug.LogWarning($"Only the server can remove connected players in {nameof(UGameState)}.");
                return false;
            }

            int playerIndex = IndexOfConnectedPlayer(clientId);

            if (playerIndex < 0)
            {
                return false;
            }

            connectedPlayers.RemoveAt(playerIndex);
            return true;
        }

        public void ClearConnectedPlayers()
        {
            if (!IsServer)
            {
                Debug.LogWarning($"Only the server can clear connected players in {nameof(UGameState)}.");
                return;
            }

            connectedPlayers.Clear();
        }

        public Dictionary<ulong, NetworkObject> GetConnectedPlayersSnapshot()
        {
            Dictionary<ulong, NetworkObject> snapshot = new();

            for (int i = 0; i < connectedPlayers.Count; i++)
            {
                ConnectedPlayerState playerState = connectedPlayers[i];

                if (TryResolveNetworkObject(playerState.PlayerNetworkObjectId, out NetworkObject playerObject))
                {
                    snapshot[playerState.ClientId] = playerObject;
                }
            }

            return snapshot;
        }

        public bool TryGetConnectedPlayerState(ulong clientId, out ConnectedPlayerState playerState)
        {
            int playerIndex = IndexOfConnectedPlayer(clientId);

            if (playerIndex < 0)
            {
                playerState = default;
                return false;
            }

            playerState = connectedPlayers[playerIndex];
            return true;
        }

        private int IndexOfConnectedPlayer(ulong clientId)
        {
            for (int i = 0; i < connectedPlayers.Count; i++)
            {
                if (connectedPlayers[i].ClientId == clientId)
                {
                    return i;
                }
            }

            return -1;
        }

        private bool TryResolveNetworkObject(ulong networkObjectId, out NetworkObject networkObject)
        {
            networkObject = null;
            NetworkManager activeNetworkManager = NetworkManager != null ? NetworkManager : NetworkManager.Singleton;

            return activeNetworkManager != null
                && activeNetworkManager.SpawnManager != null
                && activeNetworkManager.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out networkObject);
        }

        private void HandleFrameworkInitializedChanged(bool previousValue, bool newValue)
        {
            if (newValue)
            {
                RaiseGameStateInitialized();
            }
        }

        private void RaiseGameStateInitialized()
        {
            if (hasRaisedGameStateInitialized)
            {
                return;
            }

            hasRaisedGameStateInitialized = true;
            GameStateInitialized?.Invoke(this);
        }
    }
}
