using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UnscriptedEngine.Editor
{
    /// <summary>Creates a minimal interaction target using the framework's existing components.</summary>
    public static class InteractableCubeMenu
    {
        private const string MenuPath = "Tools/UnscriptedEngine/Gameplay/Create Interactable Cube";

        [MenuItem(MenuPath)]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Create interactable cube");

            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Interactable Cube";
            StageUtility.PlaceGameObjectInCurrentStage(cube);
            if (SceneView.lastActiveSceneView != null)
                cube.transform.position = SceneView.lastActiveSceneView.pivot;

            Undo.RegisterCreatedObjectUndo(cube, "Create interactable cube");
            Undo.AddComponent<NetworkObject>(cube);
            Undo.AddComponent<UInteractableComponent>(cube);
            InteractionPromptInfo prompt = Undo.AddComponent<InteractionPromptInfo>(cube);
            SerializedObject promptSettings = new SerializedObject(prompt);
            promptSettings.FindProperty("displayName").stringValue = cube.name;
            promptSettings.ApplyModifiedProperties();

            Selection.activeGameObject = cube;
            EditorGUIUtility.PingObject(cube);
            Undo.CollapseUndoOperations(undoGroup);
        }

        [MenuItem(MenuPath, true)]
        private static bool CanCreate() => !EditorApplication.isPlayingOrWillChangePlaymode;
    }
}
