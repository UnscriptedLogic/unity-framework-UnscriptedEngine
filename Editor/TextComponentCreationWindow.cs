#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;
using TMPro;
using UnscriptedEngine;

public class TextComponentCreationWindow : EditorWindow
{
    public const string PATHNAME = "GameObject/UnscriptedEngine/";
    private string componentName = "TextComponent";
    private string text = "";

    [MenuItem(PATHNAME + "UI/Create Text Component")]
    public static void ShowWindow()
    {
        GetWindow<TextComponentCreationWindow>("Create UI Component");
    }

    private void OnGUI()
    {
        GUILayout.Space(10);

        GUILayout.Label("Enter Text:", EditorStyles.boldLabel);
        text = EditorGUILayout.TextArea(text, GUILayout.Height(100));

        GUILayout.Space(10);

        if (GUILayout.Button("Create"))
        {
            CreateTextComponent();
            Close();
        }
    }

    private void CreateTextComponent()
    {
        if (!string.IsNullOrEmpty(text))
        {
            if (text.Length >= 16)
            {
                componentName = "Desc";
            }
            else
            {
                componentName = "";
                for (int i = 0; i < Mathf.Min(text.Length, 16); i++)
                {
                    if (char.IsWhiteSpace(text[i])) continue;

                    componentName += text[i];
                }
            }
        }

        GameObject textComponent = new GameObject(componentName + "TMP");
        GameObjectUtility.SetParentAndAlign(textComponent, Selection.activeGameObject as GameObject);

        textComponent.AddComponent<RectTransform>();
        textComponent.AddComponent<CanvasRenderer>();

        TextMeshProUGUI tmp = textComponent.AddComponent<TextMeshProUGUI>();
        tmp.text = text;

        textComponent.AddComponent<UTextComponent>();

        Undo.RegisterCreatedObjectUndo(textComponent, "Create " + textComponent.name);
        Selection.activeGameObject = textComponent;
    }
}

#endif