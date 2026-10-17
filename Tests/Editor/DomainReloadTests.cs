using System;
using System.Collections;
using System.Reflection;
using Framework.TickSystem;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace UnscriptedEngine.Tests
{
    public class DomainReloadTests
    {
        private bool previousOptionsEnabled;
        private EnterPlayModeOptions previousOptions;
        private SceneSetup[] previousScenes;
        private static int receivedEvents;
        private string temporaryAssetFolder;

        [SetUp]
        public void SetUp()
        {
            previousScenes = null;
            // Do not discard unsaved user edits when this test is run interactively.
            for (int i = 0; i < SceneManager.sceneCount; i++)
                Assert.That(SceneManager.GetSceneAt(i).isDirty, Is.False, "Save scenes before running this test.");

            previousOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            previousOptions = EditorSettings.enterPlayModeOptions;
            previousScenes = EditorSceneManager.GetSceneManagerSetup();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        }

        [UnityTest]
        public IEnumerator NewSessionDropsOldSubscribersAndAcceptsNewSubscribers()
        {
            receivedEvents = 0;
            yield return new EnterPlayMode(false);

            // Both static delegates and closures evade destroyed-Unity-object pruning.
            EventBus.Subscribe<int>(ReceiveEvent);
            int closureCalls = 0;
            EventBus.Subscribe<int>(_ => closureCalls++);
            UGameInstance.GameInstanceInitialized += IgnoreGameInstance;
            UGameInstance.GameInstanceShuttingDown += IgnoreGameInstance;
            UGameMode.GameModeInitialized += IgnoreGameMode;
            UGameState.GameStateInitialized += IgnoreGameState;
            EventBus.Publish(1);
            Assert.That(receivedEvents, Is.EqualTo(1));
            Assert.That(closureCalls, Is.EqualTo(1));
            var camera = new GameObject("Session camera").AddComponent<CameraHandler>();
            Assert.That(CameraHandler.Instance, Is.SameAs(camera));
            var updateProbe = new GameObject("Session update probe").AddComponent<TestUpdateProbe>();
            UpdateManager.RegisterUpdate(updateProbe, _ => { });
            UpdateManager.RegisterFixedUpdate(updateProbe, _ => { });
            Assert.That(UpdateManager.UpdateListenerCount, Is.EqualTo(1));

            yield return new ExitPlayMode();
            yield return new EnterPlayMode(false);

            EventBus.Publish(2);
            Assert.That(receivedEvents, Is.EqualTo(1), "Previous session's static listener survived.");
            Assert.That(closureCalls, Is.EqualTo(1), "Previous session's closure survived.");
            Assert.That(CameraHandler.Instance, Is.Null);
            // Do not claim singleton/ticker reset coverage for systems this test never created.
            Assert.That(UpdateManager.UpdateListenerCount, Is.Zero);
            Assert.That(UpdateManager.FixedUpdateListenerCount, Is.Zero);
            AssertEventEmpty(typeof(UGameInstance), nameof(UGameInstance.GameInstanceInitialized));
            AssertEventEmpty(typeof(UGameInstance), nameof(UGameInstance.GameInstanceShuttingDown));
            AssertEventEmpty(typeof(UGameMode), nameof(UGameMode.GameModeInitialized));
            AssertEventEmpty(typeof(UGameState), nameof(UGameState.GameStateInitialized));

            EventBus.Subscribe<int>(ReceiveEvent);
            EventBus.Publish(3);
            Assert.That(receivedEvents, Is.EqualTo(2));
            EventBus.Unsubscribe<int>(ReceiveEvent);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator ModalPanelsKeepCursorReleasedUntilLastLeaseCloses()
        {
            yield return new EnterPlayMode(false);
            var first = new GameObject("First modal"); var second = new GameObject("Second modal");
            IDisposable a = null, b = null;
            try
            {
                InputFocus.SetGameplayCursorLocked(true);
                Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.Locked));
                Assert.That(Cursor.visible, Is.False);
                a = InputFocus.Acquire(first); b = InputFocus.Acquire(second);
                a.Dispose();
                Assert.That(InputFocus.IsBlocked(null), Is.True);
                Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None));
                Assert.That(Cursor.visible, Is.True);
                // A gameplay request must not steal focus while another panel is open.
                InputFocus.SetGameplayCursorLocked(true);
                Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None));
                b.Dispose();
                Assert.That(InputFocus.IsBlocked(null), Is.False);
                Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.Locked));
                Assert.That(Cursor.visible, Is.False);
            }
            finally
            {
                a?.Dispose(); b?.Dispose(); InputFocus.SetGameplayCursorLocked(false);
                UnityEngine.Object.Destroy(first); UnityEngine.Object.Destroy(second);
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator ValveAnimatesAssignedHandleAndCompletesOnlyAfterTimeAdvances()
        {
            yield return new EnterPlayMode(false);
            var obj = new GameObject("Animated valve"); obj.SetActive(false);
            var handle = new GameObject("Handle"); handle.transform.SetParent(obj.transform);
            var valve = obj.AddComponent<ValveControl>();
            var serialized = new SerializedObject(valve);
            serialized.FindProperty("handle").objectReferenceValue = handle.transform;
            serialized.FindProperty("openLocalEulerAngles").vector3Value = new Vector3(0, 270, 0);
            serialized.FindProperty("animationDuration").floatValue = 0.1f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            int opened = 0;
            valve.OnOpened.AddListener(() => opened++);
            float previousScale = Time.timeScale;
            try
            {
                obj.SetActive(true);
                Time.timeScale = 0;
                valve.SetValue(1);
                yield return null;
                Assert.That(opened, Is.Zero, "Completion must not fire at the start of an animation.");
                Assert.That(Quaternion.Angle(handle.transform.localRotation, Quaternion.identity), Is.LessThan(0.01f));
                Time.timeScale = 1;
                yield return WaitUntilOrFail(() => opened > 0, "Valve animation");
                Assert.That(opened, Is.EqualTo(1));
                Assert.That(Quaternion.Angle(handle.transform.localRotation, Quaternion.Euler(0, 270, 0)), Is.LessThan(0.01f));
                Time.timeScale = 0;
                valve.SetValue(0.5f);
                valve.enabled = false; valve.enabled = true;
                Assert.That(Quaternion.Angle(handle.transform.localRotation, Quaternion.Euler(0, 135, 0)), Is.LessThan(0.01f));
                Assert.That(opened, Is.EqualTo(1));
            }
            finally { Time.timeScale = previousScale; UnityEngine.Object.Destroy(obj); }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator GameInstanceInitializesOnceSurvivesSceneChangeAndShutsDownOnce()
        {
            yield return new EnterPlayMode(false);
            var originScene = SceneManager.CreateScene("Game instance origin");
            SceneManager.SetActiveScene(originScene);
            var instance = new GameObject("Game instance test").AddComponent<TestGameInstance>();
            Assert.That(instance.InitializeCount, Is.EqualTo(1));
            Assert.That(UGameInstance.TryGetGameInstance(out var found), Is.True);
            Assert.That(found, Is.SameAs(instance));

            LogAssert.Expect(LogType.Warning, "Multiple UGameInstance instances found. Keeping Game instance test and removing Duplicate game instance.");
            var duplicate = new GameObject("Duplicate game instance").AddComponent<TestGameInstance>();
            Assert.That(duplicate.InitializeCount, Is.Zero);
            yield return null;
            Assert.That(duplicate == null, Is.True);
            Assert.That(UGameInstance.Instance, Is.SameAs(instance));

            var temporaryScene = SceneManager.CreateScene("Lifecycle test scene");
            SceneManager.SetActiveScene(temporaryScene);
            yield return SceneManager.UnloadSceneAsync(originScene);
            Assert.That(originScene.isLoaded, Is.False);
            Assert.That(instance != null, Is.True, "Instance must survive unloading its creation scene.");
            Assert.That(UGameInstance.Instance, Is.SameAs(instance));
            Assert.That(instance.InitializeCount, Is.EqualTo(1));

            int shutdownEvents = 0;
            System.Action<UGameInstance> handler = _ => shutdownEvents++;
            UGameInstance.GameInstanceShuttingDown += handler;
            try
            {
                instance.SimulateQuit();
                instance.SimulateQuit();
                Assert.That(instance.ShutdownCount, Is.EqualTo(1));
                Assert.That(instance.IsInitialized, Is.False);
                UnityEngine.Object.Destroy(instance.gameObject);
                yield return null;
                Assert.That(shutdownEvents, Is.EqualTo(1));
                Assert.That(UGameInstance.Instance, Is.Null);
            }
            finally
            {
                UGameInstance.GameInstanceShuttingDown -= handler;
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RespawnPairsGameplayLifetimeAndResetsServerState()
            => RunRespawnAndTickRoles(true);

        [UnityTest]
        public IEnumerator DedicatedServerRunsServerTicksWithoutClientTicks()
            => RunRespawnAndTickRoles(false);

        [UnityTest]
        public IEnumerator ConnectedClientRunsSharedThenClientHooksWithoutServerHooks()
        {
            temporaryAssetFolder = "Assets/UnscriptedEngineTests_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", temporaryAssetFolder.Substring("Assets/".Length));
            var prefab = CreateNetworkPrefab("Client tick probe", false);
            yield return new EnterPlayMode(false);
            ushort port;
            using (var socket = new System.Net.Sockets.UdpClient(0))
                port = (ushort)((System.Net.IPEndPoint)socket.Client.LocalEndPoint).Port;
            var server = CreateManager("Tick test server", prefab, port);
            var client = CreateManager("Tick test client", prefab, port);
            GameObject spawned = null;
            float previousScale = Time.timeScale;
            Time.timeScale = 1;
            try
            {
                Assert.That(server.StartServer(), Is.True);
                Assert.That(client.StartClient(), Is.True);
                yield return WaitUntilOrFail(() => client.IsConnectedClient, "Client connection");
                spawned = UnityEngine.Object.Instantiate(prefab);
                var networkObject = spawned.GetComponent<Unity.Netcode.NetworkObject>();
                networkObject.Spawn();
                ulong id = networkObject.NetworkObjectId;
                yield return WaitUntilOrFail(() => client.SpawnManager.SpawnedObjects.ContainsKey(id), "Client replica spawn");
                var replica = client.SpawnManager.SpawnedObjects[id];
                var pawn = replica.GetComponent<TestLifecycleObject>();
                var component = replica.GetComponent<TestLifecycleComponent>();
                Assert.That(pawn.IsClient, Is.True);
                Assert.That(pawn.IsServer, Is.False);
                // Isolate role dispatch from game-mode readiness; network roles remain real NGO state.
                pawn.ForceTick = true; component.ForceTick = true;
                yield return null;
                yield return new WaitForFixedUpdate();
                AssertTickTrace(pawn.FrameTrace, new[] { "shared", "client" });
                AssertTickTrace(pawn.FixedTrace, new[] { "shared", "client" });
                AssertTickTrace(component.FrameTrace, new[] { "shared", "client" });
                AssertTickTrace(component.FixedTrace, new[] { "shared", "client" });
                AssertSingleDispatchPerStep(pawn.TickFrames, pawn.FixedSteps);
                AssertSingleDispatchPerStep(component.TickFrames, component.FixedSteps);
                Assert.That(pawn.ServerTicks + pawn.ServerFixedTicks, Is.Zero);
                Assert.That(component.ServerTicks + component.ServerFixedTicks, Is.Zero);
            }
            finally
            {
                Time.timeScale = previousScale;
                client.Shutdown(); server.Shutdown();
                if (spawned != null) UnityEngine.Object.Destroy(spawned);
                UnityEngine.Object.Destroy(client.gameObject);
                UnityEngine.Object.Destroy(server.gameObject);
            }
            yield return null;
            yield return new ExitPlayMode();
        }

        private static Unity.Netcode.NetworkManager CreateManager(string name, GameObject prefab, ushort port)
        {
            var obj = new GameObject(name);
            var manager = obj.AddComponent<Unity.Netcode.NetworkManager>();
            var transport = obj.AddComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
            transport.SetConnectionData("127.0.0.1", port);
            manager.NetworkConfig = new Unity.Netcode.NetworkConfig { NetworkTransport = transport, EnableSceneManagement = false };
            manager.NetworkConfig.Prefabs.Add(new Unity.Netcode.NetworkPrefab { Prefab = prefab });
            return manager;
        }

        private static IEnumerator WaitUntilOrFail(Func<bool> ready, string operation)
        {
            float deadline = Time.realtimeSinceStartup + 10;
            while (!ready() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(ready(), Is.True, operation + " timed out.");
        }

        private IEnumerator RunRespawnAndTickRoles(bool host)
        {
            // Author prefabs in Edit Mode so NGO receives real, nonzero prefab IDs.
            temporaryAssetFolder = "Assets/UnscriptedEngineTests_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", temporaryAssetFolder.Substring("Assets/".Length));
            var statePrefab = CreateNetworkPrefab("GameState", true);
            var pawnPrefab = CreateNetworkPrefab("Pawn", false);
            yield return new EnterPlayMode(false);
            var managerObject = new GameObject("Lifecycle network manager");
            var manager = managerObject.AddComponent<Unity.Netcode.NetworkManager>();
            var transport = managerObject.AddComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
            transport.SetConnectionData("127.0.0.1", 0);
            manager.NetworkConfig = new Unity.Netcode.NetworkConfig
            {
                NetworkTransport = transport,
                EnableSceneManagement = false
            };
            manager.NetworkConfig.Prefabs.Add(new Unity.Netcode.NetworkPrefab { Prefab = statePrefab });
            manager.NetworkConfig.Prefabs.Add(new Unity.Netcode.NetworkPrefab { Prefab = pawnPrefab });
            GameObject stateObject = null;
            GameObject pawnObject = null;
            float previousTimeScale = Time.timeScale;
            Time.timeScale = 1f;
            try
            {
                Assert.That(host ? manager.StartHost() : manager.StartServer(), Is.True);
                stateObject = UnityEngine.Object.Instantiate(statePrefab);
                var stateNetwork = stateObject.GetComponent<Unity.Netcode.NetworkObject>();
                var state = stateObject.GetComponent<TestGameState>();
                stateNetwork.Spawn();
                Assert.That(state.SetFrameworkInitialized(), Is.True);

                pawnObject = UnityEngine.Object.Instantiate(pawnPrefab);
                var pawnNetwork = pawnObject.GetComponent<Unity.Netcode.NetworkObject>();
                var pawn = pawnObject.GetComponent<TestLifecycleObject>();
                var component = pawnObject.GetComponent<TestLifecycleComponent>();
                pawnNetwork.Spawn();
                Assert.That(pawn.Begins, Is.EqualTo(1));
                Assert.That(component.Begins, Is.EqualTo(1));
                yield return null;
                yield return new WaitForFixedUpdate();
                Assert.That(pawn.SharedTicks, Is.GreaterThan(0));
                Assert.That(pawn.ServerTicks, Is.EqualTo(pawn.SharedTicks));
                Assert.That(pawn.ClientTicks, Is.EqualTo(host ? pawn.SharedTicks : 0));
                Assert.That(component.ServerTicks, Is.EqualTo(component.SharedTicks));
                Assert.That(component.SharedTicks, Is.GreaterThan(0));
                Assert.That(component.ClientTicks, Is.EqualTo(host ? component.SharedTicks : 0));
                Assert.That(pawn.SharedFixedTicks, Is.GreaterThan(0));
                Assert.That(pawn.ServerFixedTicks, Is.EqualTo(pawn.SharedFixedTicks));
                Assert.That(pawn.ClientFixedTicks, Is.EqualTo(host ? pawn.SharedFixedTicks : 0));
                Assert.That(component.ServerFixedTicks, Is.EqualTo(component.SharedFixedTicks));
                Assert.That(component.SharedFixedTicks, Is.GreaterThan(0));
                AssertTickTrace(pawn.FrameTrace, host);
                AssertTickTrace(pawn.FixedTrace, host);
                AssertTickTrace(component.FrameTrace, host);
                AssertTickTrace(component.FixedTrace, host);
                AssertSingleDispatchPerStep(pawn.TickFrames, pawn.FixedSteps);
                AssertSingleDispatchPerStep(component.TickFrames, component.FixedSteps);
                Assert.That(component.ClientFixedTicks, Is.EqualTo(host ? component.SharedFixedTicks : 0));
                Assert.That(UpdateManager.UpdateListenerCount, Is.EqualTo(2));
                Assert.That(UpdateManager.FixedUpdateListenerCount, Is.EqualTo(2));
                int pausedPawnTicks = pawn.SharedTicks;
                int pausedComponentTicks = component.SharedTicks;
                pawn.enabled = false; component.enabled = false;
                Assert.That(UpdateManager.UpdateListenerCount, Is.Zero);
                Assert.That(UpdateManager.FixedUpdateListenerCount, Is.Zero);
                yield return null;
                Assert.That(pawn.SharedTicks, Is.EqualTo(pausedPawnTicks));
                Assert.That(component.SharedTicks, Is.EqualTo(pausedComponentTicks));
                pawn.enabled = true; component.enabled = true;
                Assert.That(UpdateManager.UpdateListenerCount, Is.EqualTo(2));
                Assert.That(pawn.Begins, Is.EqualTo(1));
                Assert.That(component.Begins, Is.EqualTo(1));
                Assert.That(pawn.Ends, Is.Zero);

                int serverTicksBeforeDisable = pawn.ServerTicks;
                int clientTicksBeforeDisable = pawn.ClientTicks;
                pawn.DisableOnNextTick = true;
                yield return null;
                yield return null;
                Assert.That(pawn.enabled, Is.False);
                Assert.That(pawn.ServerTicks, Is.EqualTo(serverTicksBeforeDisable));
                Assert.That(pawn.ClientTicks, Is.EqualTo(clientTicksBeforeDisable));
                pawn.enabled = true;

                state.AddOrUpdateConnectedPlayer(manager.LocalClientId, pawnNetwork);
                Assert.That(state.ConnectedPlayerCount, Is.EqualTo(1));
                pawnNetwork.Despawn(false);
                Assert.That(pawn.Ends, Is.EqualTo(1));
                Assert.That(component.Ends, Is.EqualTo(1));
                Assert.That(pawn.HasBegunPlay, Is.False);
                Assert.That(component.HasBegunPlay, Is.False);
                int previousPawnTicks = pawn.SharedTicks;
                int previousComponentTicks = component.SharedTicks;
                pawn.ForceTick = true; component.ForceTick = true;
                yield return null;
                Assert.That(pawn.SharedTicks, Is.EqualTo(previousPawnTicks));
                Assert.That(component.SharedTicks, Is.EqualTo(previousComponentTicks));
                pawn.ForceTick = false; component.ForceTick = false;

                stateNetwork.Despawn(false);
                stateNetwork.Spawn();
                Assert.That(state.IsFrameworkInitialized, Is.False);
                Assert.That(state.ConnectedPlayerCount, Is.Zero);
                Assert.That(state.SetFrameworkInitialized(), Is.True);
                pawnNetwork.Spawn();
                Assert.That(pawn.Begins, Is.EqualTo(2));
                Assert.That(component.Begins, Is.EqualTo(2));
                pawnNetwork.Despawn(false);
                Assert.That(pawn.Ends, Is.EqualTo(2));
                Assert.That(component.Ends, Is.EqualTo(2));
                UnityEngine.Object.Destroy(pawnObject);
                yield return null;
                Assert.That(pawn.Ends, Is.EqualTo(2));
                Assert.That(component.Ends, Is.EqualTo(2));
            }
            finally
            {
                Time.timeScale = previousTimeScale;
                manager.Shutdown();
                if (pawnObject != null) UnityEngine.Object.Destroy(pawnObject);
                if (stateObject != null) UnityEngine.Object.Destroy(stateObject);
                UnityEngine.Object.Destroy(managerObject);
            }
            yield return null;
            yield return new ExitPlayMode();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (EditorApplication.isPlaying)
                yield return new ExitPlayMode();

            try
            {
                if (!string.IsNullOrEmpty(temporaryAssetFolder))
                {
                    AssetDatabase.DeleteAsset(temporaryAssetFolder);
                    temporaryAssetFolder = null;
                }
            }
            finally
            {
                if (previousScenes != null)
                {
                    EditorSettings.enterPlayModeOptionsEnabled = previousOptionsEnabled;
                    EditorSettings.enterPlayModeOptions = previousOptions;
                    // Test Runner's untitled scene can produce an empty setup. Unity
                    // cannot restore it because restoration requires an active scene.
                    if (previousScenes.Length == 0)
                        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    else
                        EditorSceneManager.RestoreSceneManagerSetup(previousScenes);
                    previousScenes = null;
                }
            }
        }

        private GameObject CreateNetworkPrefab(string name, bool gameState)
        {
            var source = new GameObject(name);
            try
            {
                source.AddComponent<Unity.Netcode.NetworkObject>();
                if (gameState) source.AddComponent<TestGameState>();
                else
                {
                    source.AddComponent<TestLifecycleObject>();
                    source.AddComponent<TestLifecycleComponent>();
                }
                var prefab = PrefabUtility.SaveAsPrefabAsset(source, temporaryAssetFolder + "/" + name + ".prefab");
                Assert.That(prefab, Is.Not.Null);
                return prefab;
            }
            finally { UnityEngine.Object.DestroyImmediate(source); }
        }

        private static void AssertEventEmpty(Type owner, string name)
        {
            var field = owner.GetField(name, BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            Assert.That(field.GetValue(null), Is.Null, name + " retained subscribers.");
        }

        private static void AssertSingleDispatchPerStep(System.Collections.Generic.List<int> frames, System.Collections.Generic.List<float> steps)
        {
            Assert.That(frames, Is.Not.Empty);
            Assert.That(steps, Is.Not.Empty);
            Assert.That(new System.Collections.Generic.HashSet<int>(frames).Count, Is.EqualTo(frames.Count), "Duplicate frame dispatch.");
            Assert.That(new System.Collections.Generic.HashSet<float>(steps).Count, Is.EqualTo(steps.Count), "Duplicate physics-step dispatch.");
        }
        private static void AssertTickTrace(System.Collections.Generic.List<string> trace, bool host)
            => AssertTickTrace(trace, host ? new[] { "shared", "server", "client" } : new[] { "shared", "server" });

        private static void AssertTickTrace(System.Collections.Generic.List<string> trace, string[] expected)
        {
            Assert.That(trace.Count, Is.GreaterThan(0));
            Assert.That(trace.Count % expected.Length, Is.Zero, "Every dispatch must finish all eligible hooks.");
            for (int i = 0; i < trace.Count; i++)
                Assert.That(trace[i], Is.EqualTo(expected[i % expected.Length]), $"Hook {i}");
        }

        private static void ReceiveEvent(int value) => receivedEvents++;
        private static void IgnoreGameInstance(UGameInstance instance) { }
        private static void IgnoreGameMode(UGameMode mode) { }
        private static void IgnoreGameState(UGameState state) { }
    }

    // Reflection checks must not inherit the Play Mode fixture's scene/settings lifecycle.
    public class LifecycleContractTests
    {
        [TestCase(typeof(UObject))]
        [TestCase(typeof(UObjectComponent))]
        public void FrameworkLifecycleEntryPointsCannotBeOverridden(Type type)
        {
            foreach (var name in new[] { "OnNetworkSpawn", "OnNetworkDespawn", "OnDestroy" })
                Assert.That(type.GetMethod(name).IsFinal, Is.True, name);
        }
    }
}
