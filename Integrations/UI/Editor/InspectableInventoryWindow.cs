using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnscriptedEngine.UI.Editor
{
    internal sealed class InspectableInventoryWindow : EditorWindow
    {
        private const string ActionName = "Make Inventory Inspectable";
        private const string ContextMenu = "CONTEXT/UInventoryComponent/" + ActionName;
        private const string ToolsMenu = "Tools/UnscriptedEngine/Gameplay/Items/" + ActionName;

        [SerializeField] private UInventoryComponent target;
        [SerializeField] private InventoryPresenter selectedPanel;
        private string[] paths;

        [MenuItem(ContextMenu)]
        private static void ShowForComponent(MenuCommand command) => ShowFor((UInventoryComponent)command.context);

        [MenuItem(ContextMenu, true)]
        private static bool ValidateComponent(MenuCommand command) => CanConfigure(command.context as UInventoryComponent);

        [MenuItem(ToolsMenu)]
        private static void ShowForSelection() => ShowFor(SelectedInventory());

        [MenuItem(ToolsMenu, true)]
        private static bool ValidateSelection() => CanConfigure(SelectedInventory());

        private static UInventoryComponent SelectedInventory() =>
            Selection.gameObjects.Length == 1 ? Selection.activeGameObject.GetComponent<UInventoryComponent>() : null;

        private static bool CanConfigure(UInventoryComponent inventory) =>
            inventory != null && !EditorUtility.IsPersistent(inventory) && !EditorApplication.isPlayingOrWillChangePlaymode;

        private static void ShowFor(UInventoryComponent inventory)
        {
            if (!CanConfigure(inventory)) return;
            var window = GetWindow<InspectableInventoryWindow>(true, ActionName);
            window.target = inventory;
            InspectableInventory existing = inventory.GetComponent<InspectableInventory>();
            window.selectedPanel = existing != null
                ? new SerializedObject(existing).FindProperty("panel").objectReferenceValue as InventoryPresenter
                : null;
            window.RefreshPrefabs();
            window.Show();
        }

        private void OnEnable() => RefreshPrefabs();

        private void OnProjectChange()
        {
            RefreshPrefabs();
            Repaint();
        }

        private void RefreshPrefabs()
        {
            paths = AssetDatabase.FindAssets("t:Prefab")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => LoadPresenter(path) != null)
                .OrderBy(path => path)
                .ToArray();
        }

        private static InventoryPresenter LoadPresenter(string path) =>
            AssetDatabase.LoadAssetAtPath<GameObject>(path)?.GetComponent<InventoryPresenter>();

        private void OnGUI()
        {
            if (!CanConfigure(target))
            {
                EditorGUILayout.HelpBox("Select an inventory in the scene or in Prefab Mode.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Inventory", target.gameObject.name);
            EditorGUILayout.Space();
            if (paths.Length > 0)
            {
                string currentPath = AssetDatabase.GetAssetPath(selectedPanel);
                int current = System.Array.IndexOf(paths, currentPath);
                int chosen = EditorGUILayout.Popup("UI Prefab", current, paths);
                if (chosen >= 0 && chosen != current) selectedPanel = LoadPresenter(paths[chosen]);
            }
            else
            {
                EditorGUILayout.HelpBox("No prefab has an InventoryPresenter on its root.", MessageType.Info);
            }

            if (GUILayout.Button("Create Split Inventory Prefab…")) CreateSplitInventoryPrefab();

            bool canApply = selectedPanel != null && LoadPresenter(AssetDatabase.GetAssetPath(selectedPanel)) == selectedPanel;
            using (new EditorGUI.DisabledScope(!canApply))
            {
                if (GUILayout.Button("Apply"))
                {
                    ConfigureTarget();
                    Close();
                }
            }
        }

        private void CreateSplitInventoryPrefab()
        {
            string path = EditorUtility.SaveFilePanelInProject("Create Split Inventory Prefab",
                "SplitInventory", "prefab", "Choose where to save the inventory UI prefab.");
            if (string.IsNullOrEmpty(path)) return;

            InventoryTransferPanel template = ChestInventoryUIBuilder.CreatePanel(
                null, Path.GetFileNameWithoutExtension(path), "Container");
            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                SceneManager.MoveGameObjectToScene(template.gameObject, preview);
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(template.gameObject, path);
                if (prefab != null) selectedPanel = prefab.GetComponent<InventoryPresenter>();
                RefreshPrefabs();
            }
            finally
            {
                DestroyImmediate(template.gameObject);
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        private void ConfigureTarget()
        {
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(ActionName);
            InspectableInventory inspectable = target.GetComponent<InspectableInventory>();
            if (inspectable == null)
            {
                bool splitChest = target.TryGetComponent(out UChest chest) && chest.OpenMode == ChestOpenMode.SplitInventory;
                inspectable = splitChest
                    ? Undo.AddComponent<ChestInventoryPresenter>(target.gameObject)
                    : Undo.AddComponent<InspectableInventory>(target.gameObject);
            }

            var serialized = new SerializedObject(inspectable);
            serialized.FindProperty("panel").objectReferenceValue = selectedPanel;
            serialized.ApplyModifiedProperties();
            Undo.CollapseUndoOperations(group);
        }
    }
}
