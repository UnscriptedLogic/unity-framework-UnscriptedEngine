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
            GameObject modelSource = plan.ResolveModel();
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
                AddModel(held, modelSource);
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
                GameObject worldModel = AddModel(world, modelSource);
                FitCollider(world.AddComponent<BoxCollider>(), worldModel);
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

        private static GameObject AddModel(GameObject root, GameObject source)
        {
            if (source == null)
            {
                GameObject placeholder = GameObject.CreatePrimitive(PrimitiveType.Cube);
                placeholder.name = "Model (replace with artwork)";
                placeholder.transform.SetParent(root.transform, false);
                placeholder.transform.localScale = Vector3.one * 0.25f;
                Object.DestroyImmediate(placeholder.GetComponent<Collider>());

                return placeholder;
            }

            // Keep a nested prefab/model link so shared artwork edits reach both outputs.
            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(source, root.transform);
            if (model == null) throw new IOException("Could not instantiate model: " + AssetDatabase.GetAssetPath(source));

            model.name = "Model";

            // The artwork must follow its item root rather than simulate independently.
            foreach (Collider collider in model.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }

            foreach (Rigidbody body in model.GetComponentsInChildren<Rigidbody>(true))
            {
                body.isKinematic = true;
                body.useGravity = false;
            }

            return model;
        }

        private static void FitCollider(BoxCollider collider, GameObject model)
        {
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            Bounds localBounds = new Bounds();

            foreach (Renderer renderer in renderers)
            {
                Bounds bounds = renderer.localBounds;
                Matrix4x4 toRoot = collider.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix;

                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = bounds.center + Vector3.Scale(bounds.extents, new Vector3(
                        (corner & 1) == 0 ? -1f : 1f,
                        (corner & 2) == 0 ? -1f : 1f,
                        (corner & 4) == 0 ? -1f : 1f));
                    point = toRoot.MultiplyPoint3x4(point);

                    if (hasBounds) localBounds.Encapsulate(point);
                    else
                    {
                        localBounds = new Bounds(point, Vector3.zero);
                        hasBounds = true;
                    }
                }
            }

            collider.center = hasBounds ? localBounds.center : Vector3.zero;
            Vector3 size = hasBounds ? localBounds.size : Vector3.one * 0.25f;
            collider.size = new Vector3(Mathf.Max(0.001f, size.x), Mathf.Max(0.001f, size.y), Mathf.Max(0.001f, size.z));
        }

        private static GameObject SavePrefab(GameObject root, string path)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            if (prefab == null) throw new IOException("Could not save item prefab: " + path);
            return prefab;
        }
    }
}
