using System;
using System.IO;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnscriptedEngine.Item;
using Object = UnityEngine.Object;

namespace UnscriptedEngine.Editor.Scaffolding
{
    public static class ItemScaffoldAssets
    {
        public static ItemDataSO Build(ItemScaffoldPlan plan, Type visualType, Type worldType, Type fragmentType)
        {
            plan.Validate();
            ValidateType(visualType, typeof(MonoBehaviour));
            ValidateType(worldType, typeof(UWorldItem));
            ValidateType(fragmentType, typeof(UItemFragBehaviour));
            if (!fragmentType.IsSerializable || fragmentType.GetConstructor(Type.EmptyTypes) == null)
                throw new InvalidOperationException("Item fragment needs Serializable and a public parameterless constructor.");
            ItemScaffoldPlan.RequireAbsent(plan.AssetPaths);
            plan.EnsureFolders();
            using var files = new ScaffoldFileTransaction();
            var noReplacement = new ScaffoldPlan();
            Scene preview = EditorSceneManager.NewPreviewScene();
            GameObject held = null, world = null;
            ItemDataSO data = null;
            try
            {
                held = CreateRoot(plan.name + "Held", preview);
                held.AddComponent(visualType);
                AddPlaceholder(held);
                files.Capture(noReplacement, plan.HeldPath);
                GameObject heldPrefab = SavePrefab(held, plan.HeldPath);

                data = ScriptableObject.CreateInstance<ItemDataSO>();
                data.fragments.Add(new UItemFragDetails { Name = string.IsNullOrWhiteSpace(plan.displayName) ? plan.name : plan.displayName });
                data.fragments.Add(new UItemFragStack());
                data.fragments.Add(new UItemFragEquip());
                data.fragments.Add((UItemFragBehaviour)Activator.CreateInstance(fragmentType));
                var serializedData = new SerializedObject(data);
                var fragments = serializedData.FindProperty("fragments");
                fragments.GetArrayElementAtIndex(1).FindPropertyRelative("_stackSize").intValue = plan.stackSize;
                fragments.GetArrayElementAtIndex(2).FindPropertyRelative("visualPrefab").objectReferenceValue = heldPrefab;
                serializedData.ApplyModifiedPropertiesWithoutUndo();
                files.Capture(noReplacement, plan.DataPath);
                AssetDatabase.CreateAsset(data, plan.DataPath);

                world = CreateRoot(plan.name + "World", preview);
                world.AddComponent<NetworkObject>();
                world.AddComponent<UInteractableComponent>();
                var pickup = world.AddComponent(worldType);
                world.AddComponent<BoxCollider>().size = Vector3.one * 0.25f;
                AddPlaceholder(world);
                var serializedPickup = new SerializedObject(pickup);
                serializedPickup.FindProperty("itemData").objectReferenceValue = data;
                serializedPickup.FindProperty("quantity").intValue = plan.pickupQuantity;
                serializedPickup.FindProperty("defaultBehaviour").intValue = (int)plan.defaultBehaviour;
                serializedPickup.ApplyModifiedPropertiesWithoutUndo();
                files.Capture(noReplacement, plan.WorldPath);
                SavePrefab(world, plan.WorldPath);
                AssetDatabase.SaveAssetIfDirty(data);
                files.Commit();
                return data;
            }
            finally
            {
                if (held != null) Object.DestroyImmediate(held);
                if (world != null) Object.DestroyImmediate(world);
                if (data != null && !EditorUtility.IsPersistent(data)) Object.DestroyImmediate(data);
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        private static void ValidateType(Type type, Type required)
        {
            if (type == null || type.IsAbstract || type.ContainsGenericParameters || !required.IsAssignableFrom(type))
                throw new InvalidOperationException("Compiled item script must inherit " + required.Name + ".");
        }

        private static GameObject CreateRoot(string name, Scene preview)
        {
            var root = new GameObject(name);
            SceneManager.MoveGameObjectToScene(root, preview);
            return root;
        }

        private static void AddPlaceholder(GameObject root)
        {
            var model = GameObject.CreatePrimitive(PrimitiveType.Cube);
            model.name = "Model (replace with artwork)";
            model.transform.SetParent(root.transform, false);
            model.transform.localScale = Vector3.one * 0.25f;
            Object.DestroyImmediate(model.GetComponent<Collider>());
        }

        private static GameObject SavePrefab(GameObject root, string path)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            if (prefab == null) throw new IOException("Could not save item prefab: " + path);
            return prefab;
        }
    }
}
