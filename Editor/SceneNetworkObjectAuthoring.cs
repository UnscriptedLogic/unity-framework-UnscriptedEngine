using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace UnscriptedEngine.Editor
{
    /// <summary>NGO only validates scene placement for build-listed scenes. Preserve scene identity in editor test scenes too.</summary>
    [InitializeOnLoad]
    public static class SceneNetworkObjectAuthoring
    {
        static SceneNetworkObjectAuthoring()
        {
            EditorSceneManager.sceneSaving += PrepareScene;
            EditorSceneManager.sceneOpened += (scene, mode) => PrepareScene(scene, scene.path);
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.ExitingEditMode) return;
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    Scene scene = SceneManager.GetSceneAt(i);
                    PrepareScene(scene, scene.path);
                }
            };
        }

        public static void PrepareScene(Scene scene, string path)
        {
            if (EditorApplication.isPlaying || !scene.IsValid() || !scene.isLoaded
                || EditorSceneManager.IsPreviewScene(scene) || string.IsNullOrEmpty(path)) return;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var networkObject in root.GetComponentsInChildren<NetworkObject>(true))
                {
                    if (networkObject.IsSpawned || EditorUtility.IsPersistent(networkObject)) continue;
                    var data = new SerializedObject(networkObject);
                    data.FindProperty("m_InScenePlaced").boolValue = true;
                    data.FindProperty("m_InScenePlacedDisabledByDefault").boolValue = !networkObject.gameObject.activeInHierarchy;
                    data.FindProperty("SceneMigrationSynchronization").boolValue = false;
                    var source = PrefabUtility.GetCorrespondingObjectFromSource(networkObject);
                    if (source != null)
                        data.FindProperty("InScenePlacedSourceGlobalObjectIdHash").uintValue =
                            new SerializedObject(source).FindProperty("GlobalObjectIdHash").uintValue;
                    if (!data.ApplyModifiedPropertiesWithoutUndo()) continue;
                    if (PrefabUtility.IsPartOfPrefabInstance(networkObject))
                        PrefabUtility.RecordPrefabInstancePropertyModifications(networkObject);
                }
        }
    }
}
