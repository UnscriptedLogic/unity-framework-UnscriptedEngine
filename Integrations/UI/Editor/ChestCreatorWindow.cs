using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UnscriptedEngine.UI.Editor
{
    public sealed class ChestCreatorWindow : EditorWindow
    {
        [SerializeField]
        [Tooltip("Display name of the chest object.")]
        private string chestName = "New Chest";
        [SerializeField]
        [Tooltip("Prototype namespace used by the optional generated script.")]
        private string prototypeName = "Chests";
        [SerializeField]
        [Tooltip("Parent folder for the generated chest assets. Choose a folder inside Assets.")]
        private DefaultAsset outputFolder;
        [SerializeField]
        [Tooltip("Generate and attach a script extending UChest.")]
        private bool extendScript;
        [SerializeField]
        [Tooltip("Choose side-by-side inventory UI or physical loot release.")]
        private ChestOpenMode openMode;
        [SerializeField]
        [Tooltip("Number of storage slots on the chest.")]
        private int slotCount = 20;
        [SerializeField]
        [Tooltip("Place the created prefab in the current scene with Undo support.")]
        private bool placeInScene = true;
        [SerializeField]
        [Tooltip("Exact contents, independent chance rolls, or custom generation.")]
        private ChestContentsMode contentsMode;
        [SerializeField]
        [Tooltip("Exact item definitions and quantities for this chest.")]
        private List<ChestItemAmount> predefinedItems = new List<ChestItemAmount>();
        [SerializeField]
        [Tooltip("Chance recipe shared by chest instances.")]
        private ChestLootTable lootTable;
        [SerializeField]
        [Tooltip("Custom recipe asset. Alternatively generate a subclass and implement GenerateContents.")]
        private ChestLootProvider customProvider;
        private Vector2 scroll;

        [MenuItem("Tools/UnscriptedEngine/Inventory/Create Chest")]
        public static void ShowWindow()
        {
            ChestCreatorWindow window = GetWindow<ChestCreatorWindow>("Create Chest");
            window.minSize = new Vector2(420f, 330f);
        }

        private void OnEnable()
        {
            if (outputFolder != null) return;
            if (!AssetDatabase.IsValidFolder("Assets/Prototypes"))
                AssetDatabase.CreateFolder("Assets", "Prototypes");
            outputFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>("Assets/Prototypes");
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            chestName = EditorGUILayout.TextField(new GUIContent("Name", "Chest display name."), chestName);
            prototypeName = EditorGUILayout.TextField(new GUIContent("Prototype", "Namespace for the optional generated script."), prototypeName);
            outputFolder = (DefaultAsset)EditorGUILayout.ObjectField(new GUIContent("Output folder", "Parent folder for generated chest assets."), outputFolder, typeof(DefaultAsset), false);
            if (GUILayout.Button("Browse folder..."))
            {
                string selected = EditorUtility.OpenFolderPanel("Choose a folder inside Assets", Application.dataPath, "");
                if (!string.IsNullOrEmpty(selected))
                {
                    string relative = FileUtil.GetProjectRelativePath(selected);
                    if (AssetDatabase.IsValidFolder(relative) && (relative == "Assets" || relative.StartsWith("Assets/", StringComparison.Ordinal)))
                        outputFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(relative);
                    else EditorUtility.DisplayDialog("Invalid folder", "Choose an existing folder inside this project's Assets folder.", "OK");
                }
            }
            extendScript = EditorGUILayout.Toggle(new GUIContent("Extend Script", "Generate a subclass of UChest and attach it after compilation."), extendScript);
            openMode = (ChestOpenMode)EditorGUILayout.EnumPopup(new GUIContent("Open Behaviour", "How successful interaction opens the chest."), openMode);
            slotCount = EditorGUILayout.IntSlider(new GUIContent("Inventory Slots", "Storage capacity."), slotCount, 1, 100);
            placeInScene = EditorGUILayout.Toggle(new GUIContent("Place In Scene", "Create an undoable instance at the Scene view pivot."), placeInScene);
            SerializedObject settings = new SerializedObject(this);
            settings.Update();
            UnscriptedEngine.Editor.UChestEditor.DrawContents(settings);
            settings.ApplyModifiedProperties();
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(openMode == ChestOpenMode.SplitInventory
                ? "Creates a modal two-column UI with drag/drop and Close button. Uses the player's UCanvasController for input blocking and cursor focus. Player needs an inventory and initialized UI controller."
                : "Creates a loot release component and spawn origin. Add contents whose drop fragments reference physical pickup prefabs.", MessageType.Info);

            ChestCreationPlan plan = new ChestCreationPlan
            {
                name = chestName, prototype = prototypeName, extend = extendScript, mode = openMode,
                outputFolder = outputFolder != null ? AssetDatabase.GetAssetPath(outputFolder) : "Invalid",
                contentsMode = contentsMode,
                lootTableGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(lootTable)),
                customProviderGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(customProvider)),
                slots = slotCount, place = placeInScene,
                sceneHandle = UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle.GetRawData(),
                position = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero
            };
            foreach (ChestItemAmount entry in predefinedItems)
                plan.contents.Add(new ChestCreationItem
                {
                    itemGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(entry.Item)), quantity = entry.Quantity
                });
            string problem = null;
            try { if (ChestCreation.HasPending) plan.Validate(); else plan.ValidateNew(); }
            catch (Exception exception) { problem = exception.Message; }
            if (problem != null) EditorGUILayout.HelpBox(problem, MessageType.Warning);
            else EditorGUILayout.LabelField("Output", plan.Folder, EditorStyles.wordWrappedLabel);
            using (new EditorGUI.DisabledScope(problem != null || ChestCreation.HasPending
                || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode || EditorUtility.scriptCompilationFailed))
                if (GUILayout.Button("Create Chest")) ChestCreation.Start(plan);

            if (!string.IsNullOrEmpty(ChestCreation.Status)) EditorGUILayout.HelpBox(ChestCreation.Status, MessageType.Info);
            if (ChestCreation.HasPending)
            {
                if (GUILayout.Button("Finish Pending Chest")) ChestCreation.TryComplete();
                if (GUILayout.Button("Cancel Pending Chest")) ChestCreation.Cancel();
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
