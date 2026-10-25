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
            var heldObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            heldObject.name = "Held toggle";
            Object.DestroyImmediate(heldObject.GetComponent<Collider>());
            heldObject.AddComponent<Light>();
            var heldPrefab = PrefabUtility.SaveAsPrefabAsset(heldObject, folder + "/Held.prefab");
            Object.DestroyImmediate(heldObject);
            var equipFragment = new UItemFragEquip();
            Set(equipFragment, "visualPrefab", heldPrefab);
            Set(equipFragment, "anchorPosition", new Vector3(0.1f, -0.1f, 0.3f));
            Set(equipFragment, "anchorRotation", new Vector3(10, 20, 0));
            item.fragments.Add(equipFragment);
            item.fragments.Add(new TestReplicatedItemFragment());
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
            var equipment = pawnObject.AddComponent<TestItemEquipComponent>();
            Set(equipment, "inventoryComponent", inventory);
            var cameraRig = new GameObject("Owner-only camera rig");
            cameraRig.transform.SetParent(pawnObject.transform, false);
            var handAnchor = new GameObject("Hand anchor").transform;
            handAnchor.SetParent(cameraRig.transform, false);
            handAnchor.localPosition = new Vector3(0.2f, 0.4f, 0.5f);
            Set(equipment, "equipAnchor", handAnchor);
            Set(pawnObject.AddComponent<DisableOnProxy>(), "targets", new[] { cameraRig });
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
                var serverEquipment = owner.GetComponent<ItemEquipComponent>();
                yield return Wait(() => State(clientEquipment).Session == State(serverEquipment).Session, "Equipment session");
                Assert.That(clientEquipment.Use("unknown", true), Is.False);
                Assert.That(clientEquipment.Use("primary", true), Is.True);
                yield return Wait(() => IsOn(serverEquipment) && IsOn(clientEquipment), "Owner use and light replication");
                Assert.That(clientEquipment.Use("primary", false), Is.True);
                yield return null;
                Assert.That(IsOn(serverEquipment), Is.True, "Release must not toggle again.");
                Assert.That(clientEquipment.HeldVisual.activeInHierarchy, Is.True);
                Assert.That(serverEquipment.HeldVisual.activeInHierarchy, Is.True,
                    "A remote player's item must stay visible on the host/server despite disabled camera rig.");
                Assert.That(owner.transform.Find("Owner-only camera rig").gameObject.activeSelf, Is.False);
                var ownerHand = replica.transform.Find("Owner-only camera rig/Hand anchor");
                ownerHand.localPosition = new Vector3(0.45f, 0.6f, 0.3f);
                ownerHand.localRotation = Quaternion.Euler(-25, 60, 5);
                yield return Wait(() => PoseMatches(clientEquipment, serverEquipment), "Owner hand pose on server proxy");

                late = Manager("Late inventory client", port, pawnPrefab, pickupPrefab);
                Assert.That(late.StartClient(), Is.True);
                yield return Wait(() => late.IsConnectedClient && late.SpawnManager.SpawnedObjects.ContainsKey(ownerId), "Late join");
                var lateInventory = late.SpawnManager.SpawnedObjects[ownerId].GetComponent<UInventoryComponent>();
                yield return Wait(() => Total(lateInventory) == 1 && WorldCount(late) == 1, "Late join inventory and pickup");
                var observerEquipment = late.SpawnManager.SpawnedObjects[ownerId].GetComponent<ItemEquipComponent>();
                yield return Wait(() => IsOn(observerEquipment), "Late join behaviour state");
                Assert.That(observerEquipment.HeldVisual.activeInHierarchy, Is.True,
                    "A late observer must see the held mesh under an active pawn hierarchy.");
                Assert.That(observerEquipment.HeldVisual.GetComponent<MeshRenderer>().enabled, Is.True);
                Assert.That(observerEquipment.transform.Find("Owner-only camera rig").gameObject.activeSelf, Is.False);
                Assert.That(observerEquipment.HeldVisual.transform.IsChildOf(observerEquipment.transform.Find("Owner-only camera rig")), Is.False);
                yield return Wait(() => PoseMatches(clientEquipment, observerEquipment), "Late join hand pose");
                observerEquipment.transform.SetPositionAndRotation(new Vector3(12, 1, -3), Quaternion.Euler(0, 90, 0));
                ownerHand.localRotation = Quaternion.Euler(30, -45, 0);
                yield return Wait(() => PoseMatches(clientEquipment, observerEquipment) && PoseMatches(clientEquipment, serverEquipment),
                    "Moving beam follows owner hand relative to each proxy pawn");
                Assert.That(observerEquipment.Use("primary", true), Is.False, "Observer cannot use another player's item.");
                var stale = State(clientEquipment);
                Assert.That(clientEquipment.SelectSlot(1), Is.True);
                yield return Wait(() => serverEquipment.HeldItem == null, "Unequip");
                Assert.That(clientEquipment.SelectSlot(0), Is.True);
                yield return Wait(() => serverEquipment.HeldItem != null && State(clientEquipment).Session == State(serverEquipment).Session,
                    "New equipment session");
                Assert.That(IsOn(clientEquipment), Is.False, "Re-equipping starts a fresh session.");
                typeof(ItemEquipComponent).GetMethod("RequestUseRpc", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .Invoke(clientEquipment, new object[] { stale.Slot, stale.ItemId, stale.Session,
                        new Unity.Collections.FixedString64Bytes("primary"), true, false, default(RpcParams) });
                // Wait for a subsequent valid request to pass the stale request on the same reliable channel.
                Assert.That(clientEquipment.Use("primary", false), Is.True);
                yield return new WaitForSeconds(0.2f);
                Assert.That(IsOn(serverEquipment), Is.False, "Stale session request must be rejected.");
                Assert.That(clientEquipment.Use("primary", true), Is.True);
                yield return Wait(() => IsOn(serverEquipment) && IsOn(clientEquipment) && IsOn(observerEquipment), "Toggle after re-equip");
                Assert.That(clientEquipment.SelectSlot(1), Is.True);
                yield return Wait(() => serverEquipment.HeldItem == null, "Unequip before queued input");
                Assert.That(clientEquipment.SelectSlot(0), Is.True);
                Assert.That(clientEquipment.Use("primary", true), Is.True);
                Assert.That(clientEquipment.Use("primary", false), Is.True);
                yield return Wait(() => serverEquipment.SelectedSlotIndex == 0 && IsOn(serverEquipment)
                    && IsOn(clientEquipment) && IsOn(observerEquipment), "Input queued during selection synchronization");
                Assert.That(clientEquipment.SelectSlot(1), Is.True);
                yield return Wait(() => serverEquipment.HeldItem == null, "Unequip before cancelled queued input");
                Assert.That(clientEquipment.SelectSlot(0), Is.True);
                Assert.That(clientEquipment.Use("primary", true), Is.True);
                clientEquipment.CancelUse();
                yield return Wait(() => serverEquipment.SelectedSlotIndex == 0 && State(clientEquipment).Session == State(serverEquipment).Session,
                    "Cancelled selection synchronization");
                Assert.That(IsOn(serverEquipment), Is.False, "Focus cancellation must discard unsent input.");
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
        private static bool IsOn(ItemEquipComponent equipment) => equipment.HeldVisual != null
            && equipment.HeldVisual.GetComponent<Light>().enabled;
        private static bool PoseMatches(ItemEquipComponent owner, ItemEquipComponent proxy)
        {
            if (owner.HeldVisual == null || proxy.HeldVisual == null) return false;
            Transform source = owner.HeldVisual.transform, target = proxy.HeldVisual.transform;
            Transform sourceRoot = owner.NetworkObject.transform, targetRoot = proxy.NetworkObject.transform;
            return Vector3.Distance(sourceRoot.InverseTransformPoint(source.position), targetRoot.InverseTransformPoint(target.position)) < 0.005f
                && Quaternion.Angle(Quaternion.Inverse(sourceRoot.rotation) * source.rotation,
                    Quaternion.Inverse(targetRoot.rotation) * target.rotation) < 0.2f;
        }
        private static ItemEquipComponent.BehaviourSnapshot State(ItemEquipComponent equipment) =>
            ((NetworkVariable<ItemEquipComponent.BehaviourSnapshot>)typeof(ItemEquipComponent)
                .GetField("behaviourState", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .GetValue(equipment)).Value;
        private static int WorldCount(NetworkManager manager) => manager.SpawnManager.SpawnedObjects.Values.Count(obj => obj.GetComponent<UWorldItem>() != null);
        private static void Set(object target, string name, object value)
        {
            var type = target.GetType();
            while (type != null)
            {
                var field = type.GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (field != null) { field.SetValue(target, value); return; }
                type = type.BaseType;
            }
            throw new MissingFieldException(name);
        }
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
