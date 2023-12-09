#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class CreateObjectWindow : EditorWindow
{
    public const string PATHNAME = "GameObject/UnscriptedEngine/";

    [MenuItem(PATHNAME + "Create Object")]
    public static void ShowCreateObjectWindow()
    {
        GetWindow<CreateObjectWindow>("New UnscriptedEngine Object");
    }

    private void OnGUI()
    {
        float padding = 10;
        Rect area = new Rect(padding, padding,
             position.width - padding * 2f, position.height - padding * 2f);

        GUILayout.BeginArea(area);

        int buttonHeight = 70;

        if (GUILayout.Button("Create ULevelObject", GUILayout.Height(buttonHeight)))
        {
            GetWindow<CreateULevelObjectWindow>("Create New ULevelObject");
        }

        if (GUILayout.Button("Create ULevelPawn", GUILayout.Height(buttonHeight)))
        {
            GetWindow<CreateULevelPawnWindow>("Create New ULevelPawn");
        }

        if (GUILayout.Button("Create UController", GUILayout.Height(buttonHeight)))
        {
            GetWindow<CreateUControllerWindow>("Create New ULevelPawn");
        }

        if (GUILayout.Button("Create UGameModeManager", GUILayout.Height(buttonHeight)))
        {
            GetWindow<CreateUGameModeWindow>("Create New UGameModeManager");
        }

        if (GUILayout.Button("Create UCanvasController", GUILayout.Height(buttonHeight)))
        {
            GetWindow<CreateUCanvasControllerWindow>("Create New UCanvasController");
        }

        GUILayout.EndArea();
    }

    public static void AttachScript(string scriptName, GameObject gameObject, string prefix, string classExtension)
    {
        string scriptTemplate = $"using UnityEngine;\nusing UnscriptedEngine;\n\npublic class {prefix}_{scriptName} : {classExtension}\n{{\n    // Your script code here\n}}";

        string scriptsFolder = "Assets/Scripts/";

        System.IO.File.WriteAllText($"{scriptsFolder}{prefix}_{scriptName}.cs", scriptTemplate);

        AssetDatabase.Refresh();
    }
}

public class CreateULevelObjectWindow : EditorWindow
{
    private string componentName = "NewULevelObject";

    private void OnGUI()
    {
        float padding = 10;
        Rect area = new Rect(padding, padding,
             position.width - padding * 2f, position.height - padding * 2f);

        GUILayout.BeginArea(area);

        GUILayout.Label("Enter Text Component Name:", EditorStyles.boldLabel);
        componentName = EditorGUILayout.TextField("Name", componentName);

        GUILayout.Space(10);

        GUILayout.BeginHorizontal();

        if (GUILayout.Button("Create"))
        {
            Close();

            GameObject gameObject = new GameObject(componentName);
            CreateObjectWindow.AttachScript(componentName, gameObject, "O", "ULevelObject");
        }

        if (GUILayout.Button("Cancel"))
        {
            Close();
        }

        GUILayout.EndHorizontal();

        GUILayout.EndArea();
    }
}

public class CreateULevelPawnWindow : EditorWindow
{
    private string componentName = "NewULevelPawn";

    private void OnGUI()
    {
        float padding = 10;
        Rect area = new Rect(padding, padding,
             position.width - padding * 2f, position.height - padding * 2f);

        GUILayout.BeginArea(area);

        GUILayout.Label("Enter Script Name:", EditorStyles.boldLabel);
        componentName = EditorGUILayout.TextField("Name", componentName);

        GUILayout.Space(10);

        GUILayout.BeginHorizontal();

        if (GUILayout.Button("Create"))
        {
            Close();

            GameObject gameObject = new GameObject(componentName);
            CreateObjectWindow.AttachScript(componentName, gameObject, "P", "ULevelPawn");
        }

        if (GUILayout.Button("Cancel"))
        {
            Close();
        }

        GUILayout.EndHorizontal();

        GUILayout.EndArea();
    }
}

public class CreateUControllerWindow : EditorWindow
{
    private string componentName = "NewUController";

    private void OnGUI()
    {
        float padding = 10;
        Rect area = new Rect(padding, padding,
             position.width - padding * 2f, position.height - padding * 2f);

        GUILayout.BeginArea(area);

        GUILayout.Label("Enter Script Name:", EditorStyles.boldLabel);
        componentName = EditorGUILayout.TextField("Name", componentName);

        GUILayout.Space(10);

        GUILayout.BeginHorizontal();

        if (GUILayout.Button("Create"))
        {
            Close();

            GameObject gameObject = new GameObject(componentName);
            CreateObjectWindow.AttachScript(componentName, gameObject, "C", "UController");
        }

        if (GUILayout.Button("Cancel"))
        {
            Close();
        }

        GUILayout.EndHorizontal();

        GUILayout.EndArea();
    }
}

public class CreateUGameModeWindow : EditorWindow
{
    private string componentName = "NewUGameMode";

    private void OnGUI()
    {
        float padding = 10;
        Rect area = new Rect(padding, padding,
             position.width - padding * 2f, position.height - padding * 2f);

        GUILayout.BeginArea(area);

        GUILayout.Label("Enter Script Name:", EditorStyles.boldLabel);
        componentName = EditorGUILayout.TextField("Name", componentName);

        GUILayout.Space(10);

        GUILayout.BeginHorizontal();

        if (GUILayout.Button("Create"))
        {
            Close();

            GameObject gameObject = new GameObject(componentName);
            CreateObjectWindow.AttachScript(componentName, gameObject, "GM", "UGameModeBase");
        }

        if (GUILayout.Button("Cancel"))
        {
            Close();
        }

        GUILayout.EndHorizontal();

        GUILayout.EndArea();
    }
}

public class CreateUCanvasControllerWindow : EditorWindow
{
    private string componentName = "NewUCanvasController";

    private void OnGUI()
    {
        float padding = 10;
        Rect area = new Rect(padding, padding,
             position.width - padding * 2f, position.height - padding * 2f);

        GUILayout.BeginArea(area);

        GUILayout.Label("Enter Script Name:", EditorStyles.boldLabel);
        componentName = EditorGUILayout.TextField("Name", componentName);

        GUILayout.Space(10);

        GUILayout.BeginHorizontal();

        if (GUILayout.Button("Create"))
        {
            Close();

            GameObject gameObject = new GameObject(componentName);

            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler canvasScaler = gameObject.AddComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            canvasScaler.matchWidthOrHeight = 1f;
            canvasScaler.referenceResolution = new Vector2(1920, 1080);

            gameObject.AddComponent<GraphicRaycaster>();
            CreateObjectWindow.AttachScript(componentName, gameObject, "UIC", "UCanvasController");
        }

        if (GUILayout.Button("Cancel"))
        {
            Close();
        }

        GUILayout.EndHorizontal();

        GUILayout.EndArea();
    }
}

#endif