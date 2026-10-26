using System;
using UnityEditor;
using UnityEngine;
using UnscriptedEngine.Item;

namespace UnscriptedEngine.Editor.Scaffolding
{
    public sealed class ItemScaffoldWindow : EditorWindow
    {
        [SerializeField] private DefaultAsset root;
        [SerializeField] private string itemName = "NewItem";
        [SerializeField] private string displayName = "";
        [SerializeField] private int stackSize = 1;
        [SerializeField] private int pickupQuantity = 1;
        [SerializeField] private WorldItemInteraction defaultBehaviour = WorldItemInteraction.PickUp;
        [SerializeField] private GameObject modelOverride;
        private Vector2 scroll;

        [MenuItem("Tools/UnscriptedEngine/Gameplay/Items")]
        public static void Open() => GetWindow<ItemScaffoldWindow>("Create Item");

        private void OnEnable()
        {
            minSize = new Vector2(480, 440);
            if (root == null) root = AssetDatabase.LoadAssetAtPath<DefaultAsset>("Assets");
        }

        private void OnInspectorUpdate() => Repaint();

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("Create item", EditorStyles.boldLabel);
            root = (DefaultAsset)EditorGUILayout.ObjectField("Root folder", root, typeof(DefaultAsset), false);
            if (GUILayout.Button("Browse root folder..."))
            {
                string selected = EditorUtility.OpenFolderPanel("Choose item root inside Assets", Application.dataPath, "");
                if (!string.IsNullOrEmpty(selected))
                {
                    string path = FileUtil.GetProjectRelativePath(selected);
                    if (AssetDatabase.IsValidFolder(path) && (path == "Assets" || path.StartsWith("Assets/", StringComparison.Ordinal)))
                        root = AssetDatabase.LoadAssetAtPath<DefaultAsset>(path);
                    else EditorUtility.DisplayDialog("Invalid root", "Choose an existing folder inside this project's Assets folder.", "OK");
                }
            }
            itemName = EditorGUILayout.TextField(new GUIContent("Item name", "Used for script names and folders. Letters, digits, and underscores; start with a letter."), itemName);
            displayName = EditorGUILayout.TextField(new GUIContent("Display name", "Optional. Defaults to item name."), displayName);
            stackSize = EditorGUILayout.IntField("Maximum stack size", stackSize);

            modelOverride = (GameObject)EditorGUILayout.ObjectField(
                new GUIContent("Model override (optional)", "Prefab or imported 3D model used by both world and held visuals. Leave empty for the default cube."),
                modelOverride, typeof(GameObject), false);

            defaultBehaviour = (WorldItemInteraction)EditorGUILayout.EnumPopup("Default behaviour", defaultBehaviour);
            using (new EditorGUI.DisabledScope(defaultBehaviour != WorldItemInteraction.PickUp))
            {
                pickupQuantity = EditorGUILayout.IntField("World pickup quantity", pickupQuantity);
            }

            ItemScaffoldPlan plan = new ItemScaffoldPlan
            {
                name = itemName, folder = AssetDatabase.GetAssetPath(root), displayName = displayName,
                stackSize = stackSize, pickupQuantity = pickupQuantity, defaultBehaviour = defaultBehaviour,
                modelGuid = modelOverride == null ? "" : AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(modelOverride))
            };
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Files to create", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(string.Join("\n", plan.OutputPaths), MessageType.None);
            EditorGUILayout.HelpBox("Creates four editable scripts, item data, and separate world/held prefabs. Both visuals use the selected model, or a default cube when empty. Selected interaction behaviour and held behaviour are wired automatically. Existing files are never overwritten.", MessageType.Info);
            if (!string.IsNullOrEmpty(ItemScaffoldGeneration.Status))
                EditorGUILayout.HelpBox(ItemScaffoldGeneration.Status, MessageType.Info);
            bool busy = EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode;
            using (new EditorGUI.DisabledScope(busy))
            {
                if (ItemScaffoldGeneration.HasPending)
                {
                    if (GUILayout.Button("Finish Pending Item")) ItemScaffoldGeneration.TryComplete();
                    if (GUILayout.Button("Cancel Pending Item")) ItemScaffoldGeneration.Cancel();
                }
                else if (GUILayout.Button("Create Item"))
                {
                    try { ItemScaffoldGeneration.Start(plan); }
                    catch (Exception exception) { EditorUtility.DisplayDialog("Cannot create item", exception.Message, "OK"); }
                }
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
