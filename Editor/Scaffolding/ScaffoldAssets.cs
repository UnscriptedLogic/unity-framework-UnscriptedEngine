using System;
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
            if (!plan.ConfirmConflicts(plan.OutputPaths().Where(path => !path.EndsWith(".cs")))) return false;
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
            if (plan.scene && !plan.ShouldSkip(plan.PathFor("S", "unity"))
                && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
            plan.EnsureOutputFolders();

            using var files = new ScaffoldFileTransaction();
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Generate UnscriptedEngine assets");
            try
            {
                if (plan.scene) BuildScene(plan, definition, files);
                else BuildPlayer(plan, target, files);
                AssetDatabase.SaveAssets();
                files.Commit();
                Undo.CollapseUndoOperations(undoGroup);
                return true;
            }
            catch
            {
                Undo.RevertAllDownToGroup(undoGroup);
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

        private static void BuildPlayer(ScaffoldPlan plan, Scene target, ScaffoldFileTransaction files)
        {
            GameObject controller = null, pawn = null;
            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var template = ScaffoldGeneration.CreateTemplate(plan.templateType);
                if (template is IPlayerPrefabScaffoldTemplate source)
                {
                    PlayerPrefabScaffold.Validate(source);
                    controller = PlayerPrefabScaffold.Clone(source.ControllerPrefab, source.ControllerBaseType,
                        ScaffoldGeneration.ResolveScript(plan, "C"), "C_" + plan.name, preview);
                    pawn = PlayerPrefabScaffold.Clone(source.PawnPrefab, source.PawnBaseType,
                        ScaffoldGeneration.ResolveScript(plan, "P"), "P_" + plan.name, preview);
                }
                else
                {
                    controller = CreateObject(plan, "C", true);
                    pawn = CreateObject(plan, "P", true);
                }
                SceneManager.MoveGameObjectToScene(controller, preview);
                SceneManager.MoveGameObjectToScene(pawn, preview);
                template.Configure(controller, pawn);
                var controllerPrefab = SavePrefab(plan, controller, plan.PathFor("C", "prefab"), files);
                var pawnPrefab = SavePrefab(plan, pawn, plan.PathFor("P", "prefab"), files);
                var definition = ScriptableObject.CreateInstance<UPlayerDefinition>();
                definition.controllerPrefab = controllerPrefab.GetComponent<UController>();
                definition.pawnPrefab = pawnPrefab.GetComponent<UPawn>();
                ValidateDefinition(definition);
                definition = SaveAsset(plan, definition, plan.PathFor("PD", "asset"), files);
                ValidateDefinition(definition);
                if (target.IsValid())
                    WirePlayer(Find<UGameMode>(target).Single(), Find<NetworkManager>(target).Single(), definition,
                        plan, files);
                Selection.activeObject = definition;
            }
            finally
            {
                if (controller != null) Object.DestroyImmediate(controller);
                if (pawn != null) Object.DestroyImmediate(pawn);
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        private static GameObject SavePrefab(ScaffoldPlan plan, GameObject root, string path, ScaffoldFileTransaction files)
        {
            if (plan.ShouldSkip(path)) return LoadExisting<GameObject>(path);
            files.Capture(plan, path);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            if (prefab == null) throw new IOException("Failed to save prefab: " + path);
            return prefab;
        }

        private static T LoadExisting<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new IOException("Existing output is missing or incompatible: " + path);
            return asset;
        }

        private static T SaveAsset<T>(ScaffoldPlan plan, T asset, string path, ScaffoldFileTransaction files) where T : ScriptableObject
        {
            try
            {
                if (plan.ShouldSkip(path)) return LoadExisting<T>(path);
                files.Capture(plan, path);
                if (File.Exists(path))
                {
                    var existing = LoadExisting<T>(path);
                    EditorUtility.CopySerialized(asset, existing);
                    EditorUtility.SetDirty(existing);
                    return existing;
                }
                AssetDatabase.CreateAsset(asset, path);
                return asset;
            }
            finally { if (!EditorUtility.IsPersistent(asset)) Object.DestroyImmediate(asset); }
        }

        private static void BuildScene(ScaffoldPlan plan, UPlayerDefinition definition, ScaffoldFileTransaction files)
        {
            string path = plan.PathFor("S", "unity");
            if (plan.ShouldSkip(path))
            {
                LoadExisting<SceneAsset>(path);
                SaveNetworkList(plan, definition, files);
                return;
            }
            files.Capture(plan, path);
            Scene previous = SceneManager.GetActiveScene();
            Scene existingScene = SceneManager.GetSceneByPath(path);
            bool wasLoaded = existingScene.IsValid() && existingScene.isLoaded;
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            if (wasLoaded) EditorSceneManager.CloseScene(existingScene, true);
            bool saved = false;
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
                if (definition != null) WirePlayer(mode, manager, definition, plan, files);
                else
                {
                    var list = SaveNetworkList(plan, null, files);
                    manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(list);
                }
                if (!EditorSceneManager.SaveScene(scene, path)) throw new IOException("Failed to save scene: " + path);
                saved = true;
            }
            catch
            {
                files.Dispose();
                throw;
            }
            finally
            {
                if (wasLoaded && !saved) EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        }

        private static NetworkPrefabsList SaveNetworkList(ScaffoldPlan plan, UPlayerDefinition definition, ScaffoldFileTransaction files)
        {
            var list = ScriptableObject.CreateInstance<NetworkPrefabsList>();
            if (definition != null)
                foreach (GameObject prefab in new[] { definition.controllerPrefab.gameObject, definition.pawnPrefab.gameObject })
                    list.Add(new NetworkPrefab { Prefab = prefab });
            return SaveAsset(plan, list, plan.PathFor("NP", "asset"), files);
        }

        private static void WirePlayer(UGameMode mode, NetworkManager manager, UPlayerDefinition definition,
            ScaffoldPlan plan, ScaffoldFileTransaction files)
        {
            ValidateDefinition(definition);
            var list = SaveNetworkList(plan, definition, files);
            if (!list.Contains(definition.pawnPrefab.gameObject)
                && !manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Any(existing => existing != null && existing.Contains(definition.pawnPrefab.gameObject)))
                throw new InvalidOperationException("Skipped network prefab list does not register the selected pawn. Register it manually or choose Replace.");
            Undo.RecordObjects(new Object[] { mode, manager }, "Assign generated player pair");
            var serialized = new SerializedObject(mode);
            serialized.FindProperty("defaultPawnPrefab").objectReferenceValue = definition.pawnPrefab;
            serialized.ApplyModifiedProperties();
            manager.NetworkConfig.PlayerPrefab = definition.controllerPrefab.gameObject;
            if (!manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Contains(list))
                manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(list);
            PrefabUtility.RecordPrefabInstancePropertyModifications(mode);
            PrefabUtility.RecordPrefabInstancePropertyModifications(manager);
            EditorUtility.SetDirty(manager);
            EditorSceneManager.MarkSceneDirty(mode.gameObject.scene);
        }
    }
}
