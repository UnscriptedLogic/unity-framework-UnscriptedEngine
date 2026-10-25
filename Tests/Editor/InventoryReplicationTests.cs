using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnscriptedEngine.Editor;
using UnscriptedEngine.Item;
using Object = UnityEngine.Object;

namespace UnscriptedEngine.Tests
{
    public class InventoryReplicationTests
    {
        private string folder;
        private SceneSetup[] previousScenes;
        private bool previousOptionsEnabled;
        private EnterPlayModeOptions previousOptions;
        private EditorBuildSettingsScene[] previousBuildScenes;
        [SetUp] public void Setup()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
                Assert.That(SceneManager.GetSceneAt(i).isDirty, Is.False, "Save scenes before running network tests.");
            previousScenes = EditorSceneManager.GetSceneManagerSetup();
            previousBuildScenes = EditorBuildSettings.scenes;
            previousOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            previousOptions = EditorSettings.enterPlayModeOptions;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        }
        [UnityTearDown] public IEnumerator CleanupAssets()
        {
            if (EditorApplication.isPlaying) yield return new ExitPlayMode();
            if (!string.IsNullOrEmpty(folder)) { AssetDatabase.DeleteAsset(folder); folder = null; }
            ItemNetworkCatalogBuilder.Rebuild();
            if (previousBuildScenes != null) EditorBuildSettings.scenes = previousBuildScenes;
            EditorSettings.enterPlayModeOptionsEnabled = previousOptionsEnabled;
            EditorSettings.enterPlayModeOptions = previousOptions;
            if (previousScenes != null && previousScenes.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(previousScenes);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [UnityTest] public IEnumerator HostAndClientTransferWithoutCloning() => Transfer(true);
        [UnityTest] public IEnumerator DedicatedServerAndClientTransferWithoutCloning() => Transfer(false);

        [UnityTest]
        public IEnumerator AuthoredPickupKeepsSceneIdentityWhileFalling()
        {
            folder = "Assets/ScenePickupTests_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", folder.Substring(7));
            var source = new GameObject("Scene pickup");
            source.AddComponent<UWorldItem>();
            source.AddComponent<Rigidbody>();
            source.AddComponent<Unity.Netcode.Components.NetworkTransform>();
            source.AddComponent<Unity.Netcode.Components.NetworkRigidbody>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(source, folder + "/Pickup.prefab");
            Object.DestroyImmediate(source);
            var placed = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            placed.transform.position = Vector3.up * 5;
            string scenePath = folder + "/UnlistedScene.unity";
            Assert.That(EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), scenePath), Is.True);
            Assert.That(Array.Exists(EditorBuildSettings.scenes, entry => entry.path == scenePath), Is.False);
            var sceneObject = placed.GetComponent<NetworkObject>();
            Assert.That(sceneObject.InScenePlaced, Is.True, "Authored pickups must be marked before runtime scene processing.");
            Assert.That(prefab.GetComponent<NetworkObject>().InScenePlaced, Is.False, "Prefab must remain dynamically spawnable.");
            var placedData = new SerializedObject(sceneObject);
            uint prefabHash = new SerializedObject(prefab.GetComponent<NetworkObject>()).FindProperty("GlobalObjectIdHash").uintValue;
            Assert.That(placedData.FindProperty("InScenePlacedSourceGlobalObjectIdHash").uintValue, Is.EqualTo(prefabHash));
            // NGO also requires scene registration for server/client scene synchronization.
            EditorBuildSettings.scenes = previousBuildScenes.Concat(new[] { new EditorBuildSettingsScene(scenePath, true) }).ToArray();
            yield return new EnterPlayMode(false);
            Scene loaded = SceneManager.GetSceneByPath(scenePath);
            if (!loaded.IsValid())
                loaded = EditorSceneManager.LoadSceneInPlayMode(scenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return Wait(() => loaded.isLoaded, "Authored test scene load");
            SceneManager.SetActiveScene(loaded);
            placed = loaded.GetRootGameObjects().Single(root => root.GetComponent<UWorldItem>() != null);
            sceneObject = placed.GetComponent<NetworkObject>();
            var manager = Manager("Scene pickup host", 0, prefab);
            manager.NetworkConfig.EnableSceneManagement = true;
            try
            {
                Assert.That(manager.StartHost(), Is.True);
                yield return Wait(() => sceneObject.IsSpawned, "Scene pickup spawn");
                Assert.That(sceneObject.InScenePlaced, Is.True, "NGO must not convert scene pickup into a dynamic spawn.");
                yield return Wait(() => placed.transform.position.y < 4.9f, "Scene pickup physics movement");
                var serialize = typeof(NetworkObject).GetMethod("SerializeSpawnedObject",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                object packet = serialize.Invoke(sceneObject, new object[] { (ulong)1, false });
                Assert.That((bool)packet.GetType().GetField("IsSceneObject").GetValue(packet), Is.True,
                    "Clients must bind existing scene object, not instantiate another prefab.");
                Assert.That(WorldCount(manager), Is.EqualTo(1));
            }
            finally { manager.Shutdown(); Object.Destroy(manager.gameObject); }
            yield return null;
            yield return new ExitPlayMode();
        }

        [Test]
        public void SceneAuthoringPreservesInactivePickupState()
        {
            folder = "Assets/InactivePickupTests_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", folder.Substring(7));
            var inactive = new GameObject("Inactive pickup");
            var networkObject = inactive.AddComponent<UWorldItem>().NetworkObject;
            inactive.SetActive(false);
            Assert.That(EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), folder + "/Inactive.unity"), Is.True);
            Assert.That(networkObject.InScenePlaced, Is.True);
            Assert.That(new SerializedObject(networkObject).FindProperty("m_InScenePlacedDisabledByDefault").boolValue, Is.True);
            Assert.That(inactive.activeSelf, Is.False);
        }

        private IEnumerator Transfer(bool host)
        {
            folder = "Assets/InventoryNetworkTests_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", folder.Substring(7));
            var item = ScriptableObject.CreateInstance<ItemDataSO>();
            var stack = new UItemFragStack();
            Set(stack, "_stackSize", 4);
            item.fragments.Add(stack);
            AssetDatabase.CreateAsset(item, folder + "/Item.asset");
            var pickupObject = new GameObject("Pickup");
            var pickup = pickupObject.AddComponent<UWorldItem>();
            Set(pickup, "itemData", item);
            var pickupPrefab = PrefabUtility.SaveAsPrefabAsset(pickupObject, folder + "/Pickup.prefab");
            Object.DestroyImmediate(pickupObject);
            var drop = new UItemFragDrop();
            Set(drop, "worldPrefab", pickupPrefab.GetComponent<UWorldItem>());
            item.fragments.Add(drop);
            EditorUtility.SetDirty(item);
            var pawnObject = new GameObject("Inventory owner");
            pawnObject.AddComponent<NetworkObject>();
            var inventory = pawnObject.AddComponent<UInventoryComponent>();
            inventory.SetLoggingEnabled(false);
            var equipment = pawnObject.AddComponent<ItemEquipComponent>();
            Set(equipment, "inventoryComponent", inventory);
            var pawnPrefab = PrefabUtility.SaveAsPrefabAsset(pawnObject, folder + "/Owner.prefab");
            Object.DestroyImmediate(pawnObject);
            ItemNetworkCatalogBuilder.Rebuild();
            yield return new EnterPlayMode(false);
            ushort port;
            using (var socket = new System.Net.Sockets.UdpClient(0))
                port = (ushort)((System.Net.IPEndPoint)socket.Client.LocalEndPoint).Port;
            var server = Manager("Inventory server", port, pawnPrefab, pickupPrefab);
            var client = Manager("Inventory client", port, pawnPrefab, pickupPrefab);
            NetworkManager late = null;
            try
            {
                Assert.That(host ? server.StartHost() : server.StartServer(), Is.True);
                Assert.That(client.StartClient(), Is.True);
                yield return Wait(() => client.IsConnectedClient, "Client connection");
                var owner = Object.Instantiate(pawnPrefab).GetComponent<NetworkObject>();
                owner.SpawnWithOwnership(client.LocalClientId);
                ulong ownerId = owner.NetworkObjectId;
                yield return Wait(() => client.SpawnManager.SpawnedObjects.ContainsKey(ownerId), "Inventory spawn");
                var replica = client.SpawnManager.SpawnedObjects[ownerId];
                var serverInventory = owner.GetComponent<UInventoryComponent>();
                var clientInventory = replica.GetComponent<UInventoryComponent>();
                var clientEquipment = replica.GetComponent<ItemEquipComponent>();
                var world = Object.Instantiate(pickupPrefab).GetComponent<UWorldItem>();
                world.Initialize(item, 2);
                world.NetworkObject.Spawn();
                ulong pickupId = world.NetworkObjectId;
                yield return Wait(() => client.SpawnManager.SpawnedObjects.ContainsKey(pickupId), "Pickup spawn");
                var clientPickup = client.SpawnManager.SpawnedObjects[pickupId].GetComponent<UWorldItem>();
                yield return Wait(() => clientPickup.Quantity == 2, "Pickup quantity replication");
                Assert.That(clientPickup.GetComponent<UInteractableComponent>().TryBeginInteraction(replica.gameObject), Is.True);
                yield return Wait(() => Total(clientInventory) == 2 && !client.SpawnManager.SpawnedObjects.ContainsKey(pickupId), "Client pickup transfer");
                Assert.That(Total(serverInventory), Is.EqualTo(2));
                Assert.That(WorldCount(server), Is.Zero);
                Assert.That(serverInventory.TrySplitStack(0, 1).Success, Is.True);
                yield return Wait(() => clientInventory.Items.Count == 2, "Split replication");
                Assert.That(clientEquipment.SelectSlot(1), Is.True);
                yield return Wait(() => owner.GetComponent<ItemEquipComponent>().SelectedSlotIndex == 1, "Selected slot replication");
                Assert.That(clientEquipment.Drop(Vector3.forward), Is.True);
                yield return Wait(() => Total(clientInventory) == 1 && WorldCount(client) == 1, "Client drop transfer");
                Assert.That(Total(serverInventory), Is.EqualTo(1));
                Assert.That(WorldCount(server), Is.EqualTo(1));
                Assert.That(clientEquipment.SelectSlot(0), Is.True);
                yield return Wait(() => owner.GetComponent<ItemEquipComponent>().SelectedSlotIndex == 0, "Selection restored");

                late = Manager("Late inventory client", port, pawnPrefab, pickupPrefab);
                Assert.That(late.StartClient(), Is.True);
                yield return Wait(() => late.IsConnectedClient && late.SpawnManager.SpawnedObjects.ContainsKey(ownerId), "Late join");
                var lateInventory = late.SpawnManager.SpawnedObjects[ownerId].GetComponent<UInventoryComponent>();
                yield return Wait(() => Total(lateInventory) == 1 && WorldCount(late) == 1, "Late join inventory and pickup");
                Assert.That(late.SpawnManager.SpawnedObjects[ownerId].GetComponent<ItemEquipComponent>().Drop(Vector3.forward), Is.False,
                    "An observer cannot drop another player's inventory.");
                serverInventory.SetDropsAllowed(false);
                yield return Wait(() => !clientInventory.DropsAllowed, "Drop permission replication");
                Assert.That(clientEquipment.Drop(Vector3.forward), Is.False);
                Assert.That(WorldCount(server), Is.EqualTo(1));
                serverInventory.SetDropsAllowed(true);
                yield return Wait(() => clientInventory.DropsAllowed, "Drop permission restored");
                Assert.That(clientEquipment.Drop(Vector3.forward), Is.True);
                yield return Wait(() => Total(clientInventory) == 0 && Total(lateInventory) == 0
                    && WorldCount(client) == 2 && WorldCount(late) == 2, "Final stack drop");
                Assert.That(clientEquipment.HeldItem, Is.Null);
                Assert.That(WorldCount(server), Is.EqualTo(2));
                // A server pickup consumes exactly one of those drops on every peer.
                var dropped = server.SpawnManager.SpawnedObjects.Values.Select(obj => obj.GetComponent<UWorldItem>()).First(value => value != null);
                Assert.That(dropped.TryPickup(owner.gameObject), Is.True);
                yield return Wait(() => Total(clientInventory) == 1 && Total(lateInventory) == 1
                    && WorldCount(client) == 1 && WorldCount(late) == 1, "Server pickup replication");
                Assert.That(WorldCount(server), Is.EqualTo(1));
            }
            finally
            {
                if (late != null) late.Shutdown();
                client.Shutdown();
                server.Shutdown();
                if (late != null) Object.Destroy(late.gameObject);
                Object.Destroy(client.gameObject);
                Object.Destroy(server.gameObject);
            }
            yield return null;
            yield return new ExitPlayMode();
        }

        private static int Total(UInventoryComponent inventory) => inventory.Items?.Values.Sum(slot => slot.Quantity) ?? 0;
        private static int WorldCount(NetworkManager manager) => manager.SpawnManager.SpawnedObjects.Values.Count(obj => obj.GetComponent<UWorldItem>() != null);
        private static void Set(object target, string name, object value) => target.GetType()
            .GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(target, value);
        private static NetworkManager Manager(string name, ushort port, params GameObject[] prefabs)
        {
            var obj = new GameObject(name);
            var manager = obj.AddComponent<NetworkManager>();
            var transport = obj.AddComponent<UnityTransport>();
            transport.SetConnectionData("127.0.0.1", port);
            manager.NetworkConfig = new NetworkConfig { NetworkTransport = transport, EnableSceneManagement = false };
            foreach (var prefab in prefabs) manager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = prefab });
            return manager;
        }
        private static IEnumerator Wait(Func<bool> ready, string operation)
        {
            float deadline = Time.realtimeSinceStartup + 10;
            while (!ready() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(ready(), Is.True, operation + " timed out.");
        }
    }
}
