using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace UnscriptedEngine.Editor.Scaffolding
{
    public static class ScaffoldAssets
    {
        public static bool Build(ScaffoldPlan plan)
        {
            foreach (string path in plan.OutputPaths().Where(path => !path.EndsWith(".cs")))
                if (File.Exists(path) || File.Exists(path + ".meta"))
                    throw new IOException("Output already exists: " + path);
            UPlayerDefinition definition = null;
            if (!string.IsNullOrEmpty(plan.playerDefinitionPath))
            {
                definition = AssetDatabase.LoadAssetAtPath<UPlayerDefinition>(plan.playerDefinitionPath);
                ValidateDefinition(definition);
            }
            Scene target = default;
            if (!string.IsNullOrEmpty(plan.targetScenePath))
            {
                target = SceneManager.GetSceneByPath(plan.targetScenePath);
                ValidateSceneTarget(target);
            }
            if (plan.scene && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
            plan.EnsureOutputFolders();

            var created = new List<string>();
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Generate UnscriptedEngine assets");
            try
            {
                if (plan.scene) BuildScene(plan, definition, created);
                else BuildPlayer(plan, target, created);
                AssetDatabase.SaveAssets();
                Undo.CollapseUndoOperations(undoGroup);
                return true;
            }
            catch
            {
                Undo.RevertAllDownToGroup(undoGroup);
                for (int i = created.Count - 1; i >= 0; i--) AssetDatabase.DeleteAsset(created[i]);
                throw;
            }
        }

        public static void ValidateSceneTarget(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrEmpty(scene.path)
                || EditorSceneManager.IsPreviewScene(scene))
                throw new InvalidOperationException("Save and open the target scene before generating its player pair.");
            if (Find<UGameMode>(scene).Length != 1 || Find<NetworkManager>(scene).Length != 1)
                throw new InvalidOperationException("Target scene must contain exactly one GameMode and one NetworkManager (including inactive objects).");
        }

        private static T[] Find<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

        public static void ValidateDefinition(UPlayerDefinition definition)
        {
            if (definition == null || definition.controllerPrefab == null || definition.pawnPrefab == null)
                throw new InvalidOperationException("Player definition needs both controller and pawn prefabs.");
            foreach (var component in new Component[] { definition.controllerPrefab, definition.pawnPrefab })
                if (!PrefabUtility.IsPartOfPrefabAsset(component) || component.transform.parent != null
                    || component.GetComponent<NetworkObject>() == null)
                    throw new InvalidOperationException("Controller and pawn must be prefab roots with NetworkObject components.");
        }

        private static GameObject CreateObject(ScaffoldPlan plan, string prefix, bool networked)
        {
            var root = new GameObject(prefix + "_" + plan.name);
            try
            {
                if (networked) root.AddComponent<NetworkObject>();
                root.AddComponent(ScaffoldGeneration.ResolveScript(plan, prefix));
                return root;
            }
            catch { Object.DestroyImmediate(root); throw; }
        }

        private static void BuildPlayer(ScaffoldPlan plan, Scene target, List<string> created)
        {
            GameObject controller = null, pawn = null;
            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                controller = CreateObject(plan, "C", true);
                pawn = CreateObject(plan, "P", true);
                SceneManager.MoveGameObjectToScene(controller, preview);
                SceneManager.MoveGameObjectToScene(pawn, preview);
                ScaffoldGeneration.CreateTemplate(plan.templateType).Configure(controller, pawn);
                var controllerPrefab = SavePrefab(controller, plan.PathFor("C", "prefab"), created);
                var pawnPrefab = SavePrefab(pawn, plan.PathFor("P", "prefab"), created);
                var definition = ScriptableObject.CreateInstance<UPlayerDefinition>();
                definition.controllerPrefab = controllerPrefab.GetComponent<UController>();
                definition.pawnPrefab = pawnPrefab.GetComponent<UPawn>();
                ValidateDefinition(definition);
                CreateAsset(definition, plan.PathFor("PD", "asset"), created);
                if (target.IsValid())
                    WirePlayer(Find<UGameMode>(target).Single(), Find<NetworkManager>(target).Single(), definition,
                        plan.PathFor("NP", "asset"), created);
                Selection.activeObject = definition;
            }
            finally
            {
                if (controller != null) Object.DestroyImmediate(controller);
                if (pawn != null) Object.DestroyImmediate(pawn);
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        private static GameObject SavePrefab(GameObject root, string path, List<string> created)
        {
            created.Add(path);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            if (prefab == null) throw new IOException("Failed to save prefab: " + path);
            return prefab;
        }

        private static void CreateAsset(Object asset, string path, List<string> created)
        {
            created.Add(path);
            AssetDatabase.CreateAsset(asset, path);
        }

        private static void BuildScene(ScaffoldPlan plan, UPlayerDefinition definition, List<string> created)
        {
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            string path = plan.PathFor("S", "unity");
            try
            {
                SceneManager.SetActiveScene(scene);
                var mode = CreateObject(plan, "GM", true).GetComponent<UGameMode>();
                CreateObject(plan, "GI", false);
                CreateObject(plan, "GS", true);
                var manager = CreateObject(plan, "NM", false).GetComponent<NetworkManager>();
                manager.NetworkConfig = new NetworkConfig();
                manager.NetworkConfig.NetworkTransport = manager.gameObject.AddComponent<UnityTransport>();
                manager.NetworkConfig.EnableSceneManagement = true;
                if (definition != null) WirePlayer(mode, manager, definition, plan.PathFor("NP", "asset"), created);
                else
                {
                    var list = ScriptableObject.CreateInstance<NetworkPrefabsList>();
                    CreateAsset(list, plan.PathFor("NP", "asset"), created);
                    manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(list);
                }
                created.Add(path);
                if (!EditorSceneManager.SaveScene(scene, path)) throw new IOException("Failed to save scene: " + path);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        }

        private static void WirePlayer(UGameMode mode, NetworkManager manager, UPlayerDefinition definition,
            string listPath, List<string> created)
        {
            ValidateDefinition(definition);
            var list = ScriptableObject.CreateInstance<NetworkPrefabsList>();
            // A dedicated list avoids mutating shared lists used by other scenes.
            foreach (GameObject prefab in new[] { definition.controllerPrefab.gameObject, definition.pawnPrefab.gameObject })
                if (!manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Any(existing => existing != null && existing.Contains(prefab)))
                    list.Add(new NetworkPrefab { Prefab = prefab });
            CreateAsset(list, listPath, created);
            Undo.RecordObjects(new Object[] { mode, manager }, "Assign generated player pair");
            var serialized = new SerializedObject(mode);
            serialized.FindProperty("defaultPawnPrefab").objectReferenceValue = definition.pawnPrefab;
            serialized.ApplyModifiedProperties();
            manager.NetworkConfig.PlayerPrefab = definition.controllerPrefab.gameObject;
            manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(list);
            PrefabUtility.RecordPrefabInstancePropertyModifications(mode);
            PrefabUtility.RecordPrefabInstancePropertyModifications(manager);
            EditorUtility.SetDirty(manager);
            EditorSceneManager.MarkSceneDirty(mode.gameObject.scene);
        }
    }
}
