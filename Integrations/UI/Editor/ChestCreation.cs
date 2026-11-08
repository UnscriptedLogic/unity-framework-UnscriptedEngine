using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace UnscriptedEngine.UI.Editor
{
    [Serializable]
    public sealed class ChestCreationItem
    {
        public string itemGuid;
        public int quantity;
    }

    [Serializable]
    public sealed class ChestCreationPlan
    {
        private const string Keywords = " abstract as base bool break byte case catch char checked class const continue decimal default delegate do double else enum event explicit extern false finally fixed float for foreach goto if implicit in int interface internal is lock long namespace new null object operator out override params private protected public readonly ref return sbyte sealed short sizeof stackalloc static string struct switch this throw true try typeof uint ulong unchecked unsafe ushort using virtual void volatile while ";
        public string name = "New Chest";
        public string prototype = "Chests";
        public string outputFolder = "";
        public ChestContentsMode contentsMode;
        public List<ChestCreationItem> contents = new List<ChestCreationItem>();
        public string lootTableGuid = "";
        public string customProviderGuid = "";
        public bool extend;
        public ChestOpenMode mode;
        public int slots = 20;
        public bool place;
        public ulong sceneHandle;
        public Vector3 position;
        public string ClassName => "Chest_" + Regex.Replace((name ?? "").Trim(), "[^a-zA-Z0-9_]", "_");
        public string Folder => string.IsNullOrEmpty(outputFolder)
            ? "Assets/Prototypes/" + prototype + "/Chests/" + ClassName
            : outputFolder + "/" + ClassName;
        public string ScriptPath => Folder + "/" + ClassName + ".cs";
        public string PrefabPath => Folder + "/" + ClassName + ".prefab";
        public Scene TargetScene
        {
            get
            {
                for (int index = 0; index < SceneManager.sceneCount; index++)
                {
                    Scene scene = SceneManager.GetSceneAt(index);
                    if (scene.handle.GetRawData() == sceneHandle) return scene;
                }
                return default;
            }
        }

        public void Validate()
        {
            if (!Enum.IsDefined(typeof(ChestContentsMode), contentsMode)) throw new ArgumentException("Choose a valid contents mode.");
            if (contentsMode == ChestContentsMode.Predefined)
            {
                foreach (ChestCreationItem entry in contents)
                    if (entry == null || entry.quantity < 1 || ResolveAsset<ItemDataSO>(entry.itemGuid) == null)
                        throw new ArgumentException("Predefined contents need item assets and positive quantities.");
            }
            if (contentsMode == ChestContentsMode.Chance && ResolveAsset<ChestLootTable>(lootTableGuid) == null)
                throw new ArgumentException("Assign a Chest Loot Table asset.");
            if (contentsMode == ChestContentsMode.Custom && ResolveAsset<ChestLootProvider>(customProviderGuid) == null && !extend)
                throw new ArgumentException("Assign a custom provider, or enable Extend Script to author GenerateContents.");
            if (!string.IsNullOrEmpty(outputFolder))
            {
                if ((outputFolder != "Assets" && !outputFolder.StartsWith("Assets/", StringComparison.Ordinal))
                    || outputFolder.Contains('\\') || !AssetDatabase.IsValidFolder(outputFolder))
                    throw new ArgumentException("Choose an existing output folder inside Assets.");
                foreach (string segment in outputFolder.Split('/'))
                    if (segment == "." || segment == ".." || segment.Equals("Editor", StringComparison.OrdinalIgnoreCase)
                        || segment.Equals("Resources", StringComparison.OrdinalIgnoreCase))
                        throw new ArgumentException("Choose a runtime output folder outside Editor and Resources.");
            }
            if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 64 || !Regex.IsMatch(name, "[a-zA-Z0-9]"))
                throw new ArgumentException("Name needs at least one letter or digit, maximum 64 characters.");
            if (prototype == null || !Regex.IsMatch(prototype, "^[a-zA-Z_][a-zA-Z0-9_]*$")
                || Regex.IsMatch(prototype, "^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9]|Editor|Resources)$", RegexOptions.IgnoreCase)
                || Keywords.Contains(" " + prototype + " "))
                throw new ArgumentException("Prototype must be a valid identifier and runtime folder name.");
            if (slots < 1 || slots > 100 || !Enum.IsDefined(typeof(ChestOpenMode), mode))
                throw new ArgumentException("Choose a valid mode and 1–100 slots.");
            if (place)
            {
                Scene target = TargetScene;
                if (!target.IsValid() || !target.isLoaded || EditorSceneManager.IsPreviewScene(target))
                    throw new InvalidOperationException("Target scene is no longer loaded. Reopen it before finishing.");
            }
        }

        private static T ResolveAsset<T>(string guid) where T : Object
            => string.IsNullOrEmpty(guid) ? null : AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));

        internal void ConfigureContents(Component chest)
        {
            SerializedObject settings = new SerializedObject(chest);
            settings.FindProperty("contentsMode").enumValueIndex = (int)contentsMode;
            settings.FindProperty("lootTable").objectReferenceValue = ResolveAsset<ChestLootTable>(lootTableGuid);
            settings.FindProperty("customProvider").objectReferenceValue = ResolveAsset<ChestLootProvider>(customProviderGuid);
            SerializedProperty items = settings.FindProperty("predefinedItems");
            items.arraySize = contents.Count;
            for (int index = 0; index < contents.Count; index++)
            {
                SerializedProperty entry = items.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("item").objectReferenceValue = ResolveAsset<ItemDataSO>(contents[index].itemGuid);
                entry.FindPropertyRelative("quantity").intValue = contents[index].quantity;
            }
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        public void ValidateNew()
        {
            Validate();
            if (Directory.Exists(Folder) || File.Exists(Folder) || File.Exists(Folder + ".meta"))
                throw new IOException("Output already exists. Choose another chest name or prototype.");
            if (extend && AssetDatabase.FindAssets(ClassName + " t:MonoScript").Length > 0)
                throw new IOException("A similarly named script already exists. Choose another chest name.");
        }
    }

    [InitializeOnLoad]
    public static class ChestCreation
    {
        private const string PendingKey = "UnscriptedEngine.Chest.Pending";
        private const string StatusKey = "UnscriptedEngine.Chest.Status";
        public static bool HasPending => !string.IsNullOrEmpty(SessionState.GetString(PendingKey, ""));
        public static string Status => SessionState.GetString(StatusKey, "");

        static ChestCreation() { ScheduleCompletion(); }
        private static void ScheduleCompletion()
        {
            EditorApplication.update -= Resume;
            EditorApplication.update += Resume;
        }
        private static void Resume()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorApplication.update -= Resume;
            TryComplete();
        }

        public static void Start(ChestCreationPlan plan)
        {
            if (HasPending || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Finish pending work and return to Edit Mode first.");
            plan.ValidateNew();
            EnsureFolder(plan.Folder);
            SessionState.SetString(PendingKey, JsonUtility.ToJson(plan));
            if (plan.extend)
            {
                string source = "using UnityEngine;\nusing UnscriptedEngine;\n\nnamespace UnscriptedEngine.Prototypes." + plan.prototype
                    + "\n{\n    public class " + plan.ClassName + " : UChest\n    {\n"
                    + "        protected override bool CanOpen(GameObject interactor)\n        {\n"
                    + "            // Add key, quest or lock checks here.\n            return base.CanOpen(interactor);\n        }\n\n"
                    + "        protected override void OnOpened(GameObject interactor)\n        {\n"
                    + "            base.OnOpened(interactor);\n            // Add chest-specific effects here.\n        }\n"
                    + (plan.contentsMode == ChestContentsMode.Custom && string.IsNullOrEmpty(plan.customProviderGuid)
                        ? "\n        protected override void GenerateContents(GameObject interactor, System.Collections.Generic.List<InventorySlot> output)\n        {\n            // Append your items here, e.g. output.Add(new InventorySlot(itemAsset, 2));\n            // Runs once. Leaving this empty creates an empty chest.\n        }\n" : "")
                    + "    }\n}\n";
                using (FileStream stream = new FileStream(plan.ScriptPath, FileMode.CreateNew, FileAccess.Write))
                using (StreamWriter writer = new StreamWriter(stream)) writer.Write(source);
                SessionState.SetString(StatusKey, "Compiling subclass; prefab creation resumes automatically.");
                AssetDatabase.Refresh();
                ScheduleCompletion();
            }
            else TryComplete();
        }

        public static void TryComplete()
        {
            if (!HasPending || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            try
            {
                if (EditorUtility.scriptCompilationFailed) throw new InvalidOperationException("Fix Console compilation errors, then finish pending chest.");
                ChestCreationPlan plan = JsonUtility.FromJson<ChestCreationPlan>(SessionState.GetString(PendingKey, ""));
                plan.Validate();
                Type chestType = typeof(UChest);
                if (plan.extend)
                {
                    MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(plan.ScriptPath);
                    chestType = script != null ? script.GetClass() : null;
                    if (chestType == null && script != null)
                    {
                        string assemblyName = Path.GetFileNameWithoutExtension(CompilationPipeline.GetAssemblyNameFromScriptPath(plan.ScriptPath));
                        foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                            if (assembly.GetName().Name == assemblyName)
                                chestType = assembly.GetType("UnscriptedEngine.Prototypes." + plan.prototype + "." + plan.ClassName);
                    }
                    if (chestType == null)
                    {
                        SessionState.SetString(StatusKey, "Waiting for subclass compilation. Fix errors or assembly references, then finish pending chest.");
                        return;
                    }
                    if (chestType.IsAbstract || !typeof(UChest).IsAssignableFrom(chestType))
                        throw new InvalidOperationException("Generated script must extend UChest.");
                }
                if (File.Exists(plan.PrefabPath)) throw new IOException("Prefab already exists; refusing to overwrite it.");
                GameObject prefab = BuildPrefab(plan, chestType);
                SessionState.EraseString(PendingKey);
                SessionState.SetString(StatusKey, "Created " + plan.PrefabPath);
                Object selected = prefab;
                if (plan.place)
                {
                    GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, plan.TargetScene);
                    instance.transform.position = plan.position;
                    Undo.RegisterCreatedObjectUndo(instance, "Create Chest");
                    selected = instance;
                }
                Selection.activeObject = selected;
                EditorGUIUtility.PingObject(selected);
            }
            catch (Exception exception)
            {
                SessionState.SetString(StatusKey, "Creation paused: " + exception.Message + " Generated files retained.");
                Debug.LogException(exception);
            }
        }

        public static void Cancel()
        {
            SessionState.EraseString(PendingKey);
            SessionState.SetString(StatusKey, "Cancelled. Generated folders and scripts retained.");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = path.Substring(0, path.LastIndexOf('/'));
            EnsureFolder(parent);
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, Path.GetFileName(path))))
                throw new IOException("Could not create " + path);
        }

        public static GameObject BuildPrefab(ChestCreationPlan plan, Type chestType)
        {
            Scene preview = EditorSceneManager.NewPreviewScene();
            GameObject root = new GameObject(plan.name);
            SceneManager.MoveGameObjectToScene(root, preview);
            root.SetActive(false);
            try
            {
                root.AddComponent<Unity.Netcode.NetworkObject>();
                Component chest = root.AddComponent(chestType);
                UInventoryComponent inventory = root.GetComponent<UInventoryComponent>();
                SetInt(inventory, "inventorySize", plan.slots);
                SetInt(chest, "openMode", (int)plan.mode);
                plan.ConfigureContents(chest);
                GameObject model = GameObject.CreatePrimitive(PrimitiveType.Cube);
                model.name = "Chest Visual (Replace Me)";
                model.transform.SetParent(root.transform, false);
                model.transform.localPosition = new Vector3(0f, 0.5f, 0f);
                if (plan.mode == ChestOpenMode.BurstOpen)
                {
                    InventoryLootRelease release = root.AddComponent<InventoryLootRelease>();
                    GameObject origin = new GameObject("Loot Origin");
                    origin.transform.SetParent(root.transform, false);
                    origin.transform.localPosition = new Vector3(0f, 1.25f, 0f);
                    SetObject(release, "inventory", inventory);
                    SetObject(release, "origin", origin.transform);
                    SetObject(chest, "lootRelease", release);
                }
                else ChestInventoryUIBuilder.Build(root);
                root.SetActive(true);
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, plan.PrefabPath);
                if (prefab == null) throw new IOException("Could not save chest prefab.");
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        internal static void SetObject(Object target, string property, Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(property).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void SetInt(Object target, string property, int value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(property).intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
