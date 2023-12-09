#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;
using TMPro;
using UnscriptedEngine;

public class TextComponentCreationWindow : EditorWindow
{
    public const string PATHNAME = "GameObject/UnscriptedEngine/";
    private string componentName = "TextComponent";

    [MenuItem(PATHNAME + "UI/Create Text Component")]
    public static void ShowWindow()
    {
        GetWindow<TextComponentCreationWindow>("Create UI Component");
    }

    private void OnGUI()
    {
        GUILayout.Space(10);

        GUILayout.Label("Enter Text Component Name:", EditorStyles.boldLabel);
        componentName = EditorGUILayout.TextField("Name", componentName);

        GUILayout.Space(10);

        if (GUILayout.Button("Create"))
        {
            CreateTextComponent();
            Close();
        }
    }

    private void CreateTextComponent()
    {
        GameObject textComponent = new GameObject(componentName);
        GameObjectUtility.SetParentAndAlign(textComponent, Selection.activeGameObject as GameObject);

        textComponent.AddComponent<RectTransform>();
        textComponent.AddComponent<CanvasRenderer>();

        TextMeshProUGUI tmp = textComponent.AddComponent<TextMeshProUGUI>();
        tmp.text = componentName;

        textComponent.AddComponent<UTextComponent>();

        Undo.RegisterCreatedObjectUndo(textComponent, "Create " + textComponent.name);
        Selection.activeGameObject = textComponent;
    }
}

#endif