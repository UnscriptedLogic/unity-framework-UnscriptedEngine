using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace UnscriptedEngine
{
    public abstract class UGameMode : NetworkBehaviour
    {
        public static event Action<UGameMode> GameModeInitialized;

        public static UGameMode Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSessionState()
        {
            Instance = null;
            GameModeInitialized = null;
        }

        public bool IsInitialized { get; private set; }

        [SerializeField] private UPawn defaultPawnPrefab;
        [SerializeField] private Transform defaultPawnSpawnPoint;

        private readonly HashSet<ulong> pendingPlayerRegistrations = new();
        private readonly Dictionary<ulong, NetworkObject> spawnedDefaultPawns = new();

        private Coroutine initializationCoroutine;
        private uint registrationGeneration;

        protected NetworkManager networkManager;
        protected UGameState gameState;

        protected virtual void Awake()
        {
        }

        protected virtual void Start()
        {
        }

        /// <summary>
        /// Starts a local editor session: the first caller hosts, subsequent instances join.
        /// Returns true when startup is queued or networking is already running, not when connected.
        /// Returns false in player builds. Call from Start after base.Start().
        /// </summary>
        protected bool TryStartEditorSession()
        {
#if UNITY_EDITOR
            var manager = NetworkManager != null ? NetworkManager : NetworkManager.Singleton;
            if (!Application.isPlaying || !isActiveAndEnabled || manager == null || manager.ShutdownInProgress)
                return false;
            if (editorSessionCoroutine != null || manager.IsListening)
                return true;
            if (!(manager.NetworkConfig.NetworkTransport is Unity.Netcode.Transports.UTP.UnityTransport transport)
                || transport.Protocol != Unity.Netcode.Transports.UTP.UnityTransport.ProtocolType.UnityTransport)
            {
                Debug.LogError("Editor sessions require direct Unity Transport (no Relay).", this);
                return false;
            }

            UnityEditor.EditorApplication.playModeStateChanged += OnEditorSessionPlayModeChanged;
            editorSessionCoroutine = StartCoroutine(StartEditorSession(manager, transport));
            return true;
#else
            return false;
#endif
        }

#if UNITY_EDITOR
        private Coroutine editorSessionCoroutine;
        private System.IO.FileStream editorHostLock;

        private IEnumerator StartEditorSession(NetworkManager manager,
            Unity.Netcode.Transports.UTP.UnityTransport transport)
        {
            // Let all scene Start methods finish, and assign the coroutine handle before completion.
            yield return null;
            try
            {
                if (manager == null || manager.IsListening || manager.ShutdownInProgress)
                    yield break;

                ushort port = transport.ConnectionData.Port;
                // Product identity stays the same in Multiplayer Play Mode and cloned projects.
                string key = Hash128.Compute(Application.companyName + "/" + Application.productName + "/" + port).ToString();
                string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "UnscriptedEngine-" + key + ".lock");
                bool host;
                try
                {
                    editorHostLock = new System.IO.FileStream(path, System.IO.FileMode.OpenOrCreate,
                        System.IO.FileAccess.ReadWrite, System.IO.FileShare.None);
                    host = true;
                }
                catch (System.IO.IOException exception)
                {
                    // Only sharing/lock violations mean another instance owns this session.
                    int error = exception.HResult & 0xffff;
                    if (error != 32 && error != 33 && error != 11)
                        throw;
                    host = false;
                }

                transport.SetConnectionData("127.0.0.1", port, "127.0.0.1");
                if (host)
                {
                    if (!manager.StartHost())
                    {
                        Debug.LogError("Editor host failed to start. Check transport configuration and port availability.", this);
                        yield break;
                    }
                    Debug.Log($"Editor session hosting on 127.0.0.1:{port}.", this);
                    while (manager != null && (manager.IsListening || manager.ShutdownInProgress))
                        yield return null;
                }
                else
                {
                    float deadline = Time.realtimeSinceStartup + 15f;
                    while (manager != null && Time.realtimeSinceStartup < deadline)
                    {
                        if (!manager.ShutdownInProgress && !manager.IsListening)
                            manager.StartClient();
                        if (manager.IsConnectedClient)
                        {
                            Debug.Log($"Editor session joined 127.0.0.1:{port}.", this);
                            yield break;
                        }
                        yield return new WaitForSecondsRealtime(0.25f);
                    }
                    if (manager != null)
                        manager.Shutdown();
                    Debug.LogWarning("Editor client could not connect within 15 seconds. Call TryStartEditorSession again to retry.", this);
                }
            }
            finally
            {
                UnityEditor.EditorApplication.playModeStateChanged -= OnEditorSessionPlayModeChanged;
                ReleaseEditorHostLock();
                editorSessionCoroutine = null;
            }
        }

        private void OnEditorSessionPlayModeChanged(UnityEditor.PlayModeStateChange state)
        {
            if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode)
                StopEditorSessionStartup();
        }

        private void StopEditorSessionStartup()
        {
            UnityEditor.EditorApplication.playModeStateChanged -= OnEditorSessionPlayModeChanged;
            if (editorSessionCoroutine != null)
                StopCoroutine(editorSessionCoroutine);
            editorSessionCoroutine = null;
            ReleaseEditorHostLock();
        }

        private void ReleaseEditorHostLock()
        {
            editorHostLock?.Dispose();
            editorHostLock = null;
            // Keep the empty file: deleting it introduces a race with another instance opening it.
        }
#endif

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

            initializationCoroutine = StartCoroutine(InitializeGameModeWhenReady());
        }

        public override void OnNetworkDespawn()
        {
            StopInitializationCoroutine();

            if (Instance == this)
            {
                UninitializeGameMode();
            }

            base.OnNetworkDespawn();
        }

        public override void OnDestroy()
        {
#if UNITY_EDITOR
            StopEditorSessionStartup();
#endif
            StopInitializationCoroutine();

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

        private IEnumerator InitializeGameModeWhenReady()
        {
            while (!IsInitialized && isActiveAndEnabled && IsServer)
            {
                InitializeGameMode();

                if (IsInitialized)
                {
                    initializationCoroutine = null;
                    yield break;
                }

                // Scene NetworkObjects do not necessarily receive OnNetworkSpawn in
                // hierarchy order. Give the GameState and NetworkManager a chance to
                // finish spawning before trying again.
                yield return null;
            }

            initializationCoroutine = null;
        }

        private void StopInitializationCoroutine()
        {
            if (initializationCoroutine == null)
            {
                return;
            }

            StopCoroutine(initializationCoroutine);
            initializationCoroutine = null;
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

            if (!gameState.IsSpawned || !gameState.IsNetworkReady)
            {
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
            unchecked { registrationGeneration++; }
            pendingPlayerRegistrations.Clear();
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
            StopInitializationCoroutine();
            DespawnAllDefaultPawns();
            UnregisterNetworkManager();

            if (Instance == this)
            {
                Instance = null;
                // Subscribers own their subscriptions for the rest of this session.
            }

            IsInitialized = false;
        }

        private void QueueConnectedClientRegistration(ulong clientId)
        {
            if (!pendingPlayerRegistrations.Add(clientId))
            {
                return;
            }

            StartCoroutine(RegisterConnectedClientWhenPlayerObjectReady(clientId, registrationGeneration));
        }

        private IEnumerator RegisterConnectedClientWhenPlayerObjectReady(ulong clientId, uint generation)
        {
            while (generation == registrationGeneration && networkManager != null
                   && networkManager.ConnectedClients.TryGetValue(clientId, out NetworkClient client)
                   && (client.PlayerObject == null || !client.PlayerObject.IsSpawned))
            {
                yield return null;
            }

            if (generation != registrationGeneration) yield break;
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

        protected bool RespawnDefaultPawnForClient(ulong clientId)
        {
            if (!IsServer || networkManager == null || !networkManager.ConnectedClients.ContainsKey(clientId))
                return false;
            DespawnDefaultPawnForClient(clientId);
            return RegisterConnectedClient(clientId);
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
