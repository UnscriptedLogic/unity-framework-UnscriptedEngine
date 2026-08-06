using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Framework
{
    public abstract class UGameMode : NetworkBehaviour
    {
        public static event Action<UGameMode> GameModeInitialized;

        public static UGameMode Instance { get; private set; }

        public bool IsInitialized { get; private set; }

        [SerializeField] private UPawn defaultPawnPrefab;
        [SerializeField] private Transform defaultPawnSpawnPoint;

        private readonly HashSet<ulong> pendingPlayerRegistrations = new();
        private readonly Dictionary<ulong, NetworkObject> spawnedDefaultPawns = new();

        protected NetworkManager networkManager;
        protected UGameState gameState;

        protected virtual void Awake()
        {
        }

        protected virtual void Start()
        {
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (!IsServer)
            {
                return;
            }

            if (!RegisterInstance())
            {
                return;
            }

            InitializeGameMode();
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this)
            {
                UninitializeGameMode();
            }

            base.OnNetworkDespawn();
        }

        public override void OnDestroy()
        {
            if (Instance == this)
            {
                UninitializeGameMode();
            }

            base.OnDestroy();
        }

        public static bool TryGetInitializedGameMode(out UGameMode gameMode)
        {
            gameMode = Instance;
            return gameMode != null && gameMode.IsInitialized;
        }

        protected virtual void InitializeGameMode()
        {
            if (IsInitialized)
            {
                return;
            }

            if (!IsServer)
            {
                return;
            }

            if (!RegisterInstance())
            {
                return;
            }

            if (!RegisterGameState() || !RegisterNetworkManager())
            {
                return;
            }

            IsInitialized = true;

            if (!gameState.SetFrameworkInitialized())
            {
                IsInitialized = false;
                return;
            }

            GameModeInitialized?.Invoke(this);
        }

        protected virtual bool RegisterGameState()
        {
            gameState = UGameState.Instance != null
                ? UGameState.Instance
                : FindAnyObjectByType<UGameState>();

            if (gameState == null)
            {
                Debug.LogError($"{nameof(UGameMode)} requires a {nameof(UGameState)} in the scene to track connected players.");
                return false;
            }

            if (!gameState.IsSpawned)
            {
                Debug.LogError($"{nameof(UGameState)} must be on a spawned {nameof(NetworkObject)} before {nameof(UGameMode)} can initialize.");
                return false;
            }

            return true;
        }

        private bool RegisterNetworkManager()
        {
            if (networkManager != null)
            {
                return true;
            }

            NetworkManager activeNetworkManager = NetworkManager != null
                ? NetworkManager
                : NetworkManager.Singleton;

            if (activeNetworkManager == null)
            {
                Debug.LogError("NetworkManager is not present in the scene. Please add a NetworkManager prefab to the scene.");
                return false;
            }

            if (!activeNetworkManager.IsListening)
            {
                Debug.LogError($"{nameof(UGameMode)} cannot initialize before the {nameof(NetworkManager)} is listening.");
                return false;
            }

            if (!activeNetworkManager.IsServer)
            {
                return false;
            }
            
            networkManager = activeNetworkManager;
            networkManager.OnClientConnectedCallback += OnClientConnected;
            networkManager.OnClientDisconnectCallback += OnClientDisconnected;

            RegisterConnectedClients();
            return true;
        }

        private void UnregisterNetworkManager()
        {
            if (networkManager == null)
            {
                return;
            }

            networkManager.OnClientConnectedCallback -= OnClientConnected;
            networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            networkManager = null;
            pendingPlayerRegistrations.Clear();
        }

        protected virtual void OnClientDisconnected(ulong clientId)
        {
            if (!IsServer)
            {
                return;
            }

            DespawnDefaultPawnForClient(clientId);
            gameState?.RemoveConnectedPlayer(clientId);
        }

        protected virtual void OnClientConnected(ulong clientId)
        {
            if (!IsServer)
            {
                return;
            }

            RegisterConnectedClient(clientId);
        }

        protected virtual void RegisterConnectedClients()
        {
            if (networkManager == null)
            {
                return;
            }

            foreach (NetworkClient client in networkManager.ConnectedClientsList)
            {
                RegisterConnectedClient(client.ClientId);
            }
        }

        protected virtual bool RegisterConnectedClient(ulong clientId)
        {
            if (gameState == null)
            {
                RegisterGameState();
            }

            if (networkManager == null || gameState == null)
            {
                return false;
            }

            if (!networkManager.ConnectedClients.TryGetValue(clientId, out NetworkClient client))
            {
                return false;
            }

            NetworkObject playerObject = client.PlayerObject;

            if (playerObject == null || !playerObject.IsSpawned)
            {
                QueueConnectedClientRegistration(clientId);
                return false;
            }

            gameState.AddOrUpdateConnectedPlayer(clientId, playerObject);

            if (!playerObject.TryGetComponent(out UController controller))
            {
                Debug.LogWarning($"Client {clientId} player object does not have a {nameof(UController)} component.");
                return true;
            }

            SpawnAndPossessDefaultPawn(clientId, controller);
            return true;
        }

        protected virtual UPawn GetDefaultPawnPrefab(ulong clientId, UController controller)
        {
            return defaultPawnPrefab;
        }

        protected virtual Vector3 GetDefaultPawnSpawnPosition(ulong clientId, UController controller)
        {
            return defaultPawnSpawnPoint != null
                ? defaultPawnSpawnPoint.position
                : controller.transform.position;
        }

        protected virtual Quaternion GetDefaultPawnSpawnRotation(ulong clientId, UController controller)
        {
            return defaultPawnSpawnPoint != null
                ? defaultPawnSpawnPoint.rotation
                : controller.transform.rotation;
        }

        private bool RegisterInstance()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"Multiple {nameof(UGameMode)} instances found on the server. Keeping the first instance.");
                return false;
            }

            Instance = this;
            return true;
        }

        private void UninitializeGameMode()
        {
            DespawnAllDefaultPawns();
            UnregisterNetworkManager();

            if (Instance == this)
            {
                Instance = null;
                GameModeInitialized = null;
            }

            IsInitialized = false;
        }

        private void QueueConnectedClientRegistration(ulong clientId)
        {
            if (!pendingPlayerRegistrations.Add(clientId))
            {
                return;
            }

            StartCoroutine(RegisterConnectedClientWhenPlayerObjectReady(clientId));
        }

        private IEnumerator RegisterConnectedClientWhenPlayerObjectReady(ulong clientId)
        {
            while (networkManager != null
                   && networkManager.ConnectedClients.TryGetValue(clientId, out NetworkClient client)
                   && (client.PlayerObject == null || !client.PlayerObject.IsSpawned))
            {
                yield return null;
            }

            pendingPlayerRegistrations.Remove(clientId);

            if (networkManager != null && networkManager.ConnectedClients.ContainsKey(clientId))
            {
                RegisterConnectedClient(clientId);
            }
        }

        private bool SpawnAndPossessDefaultPawn(ulong clientId, UController controller)
        {
            if (controller == null)
            {
                return false;
            }

            if (controller.HasPawn)
            {
                return true;
            }

            if (TryGetSpawnedDefaultPawn(clientId, out UPawn existingPawn))
            {
                return controller.Possess(existingPawn);
            }

            UPawn pawnPrefab = GetDefaultPawnPrefab(clientId, controller);

            if (pawnPrefab == null)
            {
                Debug.LogWarning($"{nameof(UGameMode)} has no default pawn prefab assigned for client {clientId}.");
                return false;
            }

            if (!pawnPrefab.TryGetComponent(out NetworkObject _))
            {
                Debug.LogError($"{nameof(defaultPawnPrefab)} must reference a prefab with a {nameof(NetworkObject)} component.");
                return false;
            }

            UPawn pawn = Instantiate(
                pawnPrefab,
                GetDefaultPawnSpawnPosition(clientId, controller),
                GetDefaultPawnSpawnRotation(clientId, controller));

            if (!pawn.TryGetComponent(out NetworkObject pawnNetworkObject))
            {
                Debug.LogError($"Spawned {nameof(UPawn)} instance is missing a {nameof(NetworkObject)} component.");
                Destroy(pawn.gameObject);
                return false;
            }

            pawnNetworkObject.SpawnWithOwnership(clientId, true);
            spawnedDefaultPawns[clientId] = pawnNetworkObject;
            return controller.Possess(pawn);
        }

        private bool TryGetSpawnedDefaultPawn(ulong clientId, out UPawn pawn)
        {
            pawn = null;

            if (!spawnedDefaultPawns.TryGetValue(clientId, out NetworkObject pawnNetworkObject)
                || pawnNetworkObject == null
                || !pawnNetworkObject.IsSpawned)
            {
                spawnedDefaultPawns.Remove(clientId);
                return false;
            }

            return pawnNetworkObject.TryGetComponent(out pawn);
        }

        private void DespawnDefaultPawnForClient(ulong clientId)
        {
            if (!spawnedDefaultPawns.TryGetValue(clientId, out NetworkObject pawnNetworkObject))
            {
                return;
            }

            spawnedDefaultPawns.Remove(clientId);

            if (pawnNetworkObject == null)
            {
                return;
            }

            if (pawnNetworkObject.TryGetComponent(out UPawn pawn))
            {
                pawn.DetachFromController();
            }

            if (pawnNetworkObject.IsSpawned)
            {
                pawnNetworkObject.Despawn(true);
                return;
            }

            Destroy(pawnNetworkObject.gameObject);
        }

        private void DespawnAllDefaultPawns()
        {
            ulong[] clientIds = new ulong[spawnedDefaultPawns.Count];
            spawnedDefaultPawns.Keys.CopyTo(clientIds, 0);

            foreach (ulong clientId in clientIds)
            {
                DespawnDefaultPawnForClient(clientId);
            }
        }
    }
}
