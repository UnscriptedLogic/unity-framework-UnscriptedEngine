using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnscriptedEngine.Editor.Scaffolding
{
    public abstract class ScaffoldWindow : EditorWindow
    {
        [SerializeField] private string scaffoldName = "Example";
        [SerializeField] private DefaultAsset folder;
        private Vector2 scroll;
        protected abstract bool GeneratesScene { get; }

        protected virtual void OnEnable()
        {
            minSize = new Vector2(440, 380);
            if (folder == null) folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>("Assets");
        }

        protected virtual void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField(GeneratesScene ? "Scene setup" : "Controller and pawn", EditorStyles.boldLabel);
            scaffoldName = EditorGUILayout.TextField("Name", scaffoldName);
            folder = (DefaultAsset)EditorGUILayout.ObjectField("Output folder", folder, typeof(DefaultAsset), false);
            if (GUILayout.Button("Browse folder..."))
            {
                string selected = EditorUtility.OpenFolderPanel("Choose a folder inside Assets", Application.dataPath, "");
                if (!string.IsNullOrEmpty(selected))
                {
                    string relative = FileUtil.GetProjectRelativePath(selected);
                    if (AssetDatabase.IsValidFolder(relative) && (relative == "Assets" || relative.StartsWith("Assets/")))
                        folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(relative);
                    else EditorUtility.DisplayDialog("Invalid folder", "Choose an existing folder inside this project's Assets folder.", "OK");
                }
            }
            var plan = new ScaffoldPlan { name = scaffoldName, folder = AssetDatabase.GetAssetPath(folder), scene = GeneratesScene };
            DrawOptions(plan);
            EditorGUILayout.HelpBox(string.Join("\n", plan.OutputPaths()), MessageType.None);
            if (ScaffoldGeneration.HasPending)
            {
                EditorGUILayout.HelpBox("Generation pending. Unity must compile the new scripts before assets can be created. Fix Console errors, then finish. Cancel retains generated source files.", MessageType.Info);
                if (GUILayout.Button("Finish Pending Generation")) ScaffoldGeneration.TryComplete();
                if (GUILayout.Button("Cancel Pending Generation")) ScaffoldGeneration.Cancel();
            }
            using (new EditorGUI.DisabledScope(ScaffoldGeneration.HasPending || EditorApplication.isCompiling
                || EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("Generate"))
                {
                    try { ValidateOptions(plan); ScaffoldGeneration.Start(plan); }
                    catch (Exception exception) { EditorUtility.DisplayDialog("Cannot generate", exception.Message, "OK"); }
                }
            }
            EditorGUILayout.EndScrollView();
        }

        protected abstract void DrawOptions(ScaffoldPlan plan);
        protected virtual void ValidateOptions(ScaffoldPlan plan) { }
    }

    public sealed class SceneScaffoldWindow : ScaffoldWindow
    {
        [SerializeField] private UPlayerDefinition player;
        protected override bool GeneratesScene => true;

        [MenuItem("Tools/UnscriptedEngine/Create Scene Setup...")]
        public static void Open() { GetWindow<SceneScaffoldWindow>("Scene Setup"); }

        protected override void DrawOptions(ScaffoldPlan plan)
        {
            player = (UPlayerDefinition)EditorGUILayout.ObjectField("Player pair (optional)", player, typeof(UPlayerDefinition), false);
            plan.playerDefinitionPath = AssetDatabase.GetAssetPath(player);
            EditorGUILayout.HelpBox("Creates and opens a new scene after compilation, with a save prompt for current scenes. Includes GameMode, GameInstance, GameState, NetworkManager, and Unity Transport. Start Host/Client through your own UI or code. Scene is not added to build settings.", MessageType.Info);
        }

        protected override void ValidateOptions(ScaffoldPlan plan)
        {
            if (player != null) ScaffoldAssets.ValidateDefinition(player);
        }
    }

    public sealed class PlayerScaffoldWindow : ScaffoldWindow
    {
        [SerializeField] private bool wireActiveScene;
        [SerializeField] private int templateIndex;
        private Type[] templates;
        protected override bool GeneratesScene => false;

        [MenuItem("Tools/UnscriptedEngine/Create Controller and Pawn...")]
        public static void Open() { GetWindow<PlayerScaffoldWindow>("Player Setup"); }

        protected override void OnEnable()
        {
            base.OnEnable();
            templates = TypeCache.GetTypesDerivedFrom<IPlayerScaffoldTemplate>()
                .Where(type => !type.IsAbstract && !type.IsInterface && !type.ContainsGenericParameters
                    && type.GetConstructor(Type.EmptyTypes) != null)
                .OrderBy(type => type.FullName).ToArray();
        }

        protected override void DrawOptions(ScaffoldPlan plan)
        {
            if (templates.Length == 0) return;
            templateIndex = Mathf.Clamp(templateIndex, 0, templates.Length - 1);
            templateIndex = EditorGUILayout.Popup("Template", templateIndex, templates.Select(type =>
                ScaffoldGeneration.CreateTemplate(type.AssemblyQualifiedName).DisplayName).ToArray());
            plan.templateType = templates[templateIndex].AssemblyQualifiedName;
            wireActiveScene = EditorGUILayout.Toggle("Wire active scene", wireActiveScene);
            if (wireActiveScene) plan.targetScenePath = SceneManager.GetActiveScene().path;
            EditorGUILayout.HelpBox("Creates C_ and P_ scripts/prefabs plus a PD_ player definition linking them. Optional scene wiring sets NetworkManager's player prefab, GameMode's pawn, and network prefab registration. Scene changes remain unsaved and support Undo. Blank template has no movement or camera.", MessageType.Info);
        }

        protected override void ValidateOptions(ScaffoldPlan plan)
        {
            if (wireActiveScene) ScaffoldAssets.ValidateSceneTarget(SceneManager.GetActiveScene());
        }
    }
}
