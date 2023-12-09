#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace UnscriptedEngine
{
    public static class UContextMenuExtensions
    {
        public const string PATHNAME = "GameObject/UnscriptedEngine/";

        [MenuItem(PATHNAME + "Camera/Create RTSCamera")]
        public static void CreateLevelObject(MenuCommand command)
        {
            GameObject go = new GameObject("RTSCamera_Parent");
            GameObject rtsCamera = new GameObject("RTSCamera");
            rtsCamera.transform.SetParent(go.transform);
            rtsCamera.transform.SetLocalPositionAndRotation(new Vector3(0, 9, -10), Quaternion.Euler(new Vector3(60, 0, 0)));
            rtsCamera.AddComponent<Camera>();
            rtsCamera.AddComponent<AudioListener>();
            //rtsCamera.AddComponent<UniversalAdditionalCameraData>();
            rtsCamera.AddComponent<URTSCamera>();

            GameObjectUtility.SetParentAndAlign(go, command.context as GameObject);

            Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
            Selection.activeGameObject = go;
        }

        [MenuItem(PATHNAME + "UI/Create Button Component")]
        public static void CreateButtonComponent(MenuCommand command)
        {
            GameObject buttonComponent = new GameObject("ButtonComponent");
            GameObjectUtility.SetParentAndAlign(buttonComponent, command.context as GameObject);

            buttonComponent.AddComponent<RectTransform>();
            buttonComponent.AddComponent<CanvasRenderer>();
            buttonComponent.AddComponent<Image>();
            buttonComponent.AddComponent<Button>();
            buttonComponent.AddComponent<UButtonComponent>();

            Undo.RegisterCreatedObjectUndo(buttonComponent, "Create " + buttonComponent.name);
            Selection.activeGameObject = buttonComponent;
        }
    }
} 

#endif