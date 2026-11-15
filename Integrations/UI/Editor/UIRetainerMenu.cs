using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace UnscriptedEngine.UI.Editor
{
    public static class UIRetainerMenu
    {
        private const string MaterialPath = "Packages/com.unscriptedlogic.unscriptedengine/Integrations/UI/Retainer/RetainerWarp.mat";

        [MenuItem("Tools/UnscriptedEngine/UI/Create Retainer")]
        public static void Create()
        {
            Transform parent = Selection.activeTransform;
            if (parent == null || parent.GetComponentInParent<Canvas>() == null)
            {
                Debug.LogWarning("Select a RectTransform under a Canvas before creating a retainer.");
                return;
            }
            if (parent.GetComponentInParent<UIRetainer>() != null)
            {
                Debug.LogWarning("Nested retainers are unsupported. Select a parent outside the existing retainer.");
                return;
            }
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                // Also support local embedded packages whose folder differs from the package name.
                string[] matches = AssetDatabase.FindAssets("RetainerWarp t:Material");
                foreach (string match in matches)
                {
                    Material candidate = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(match));
                    if (candidate.shader != null && candidate.shader.name == "UnscriptedEngine/UI/Retainer Warp")
                    { material = candidate; break; }
                }
            }
            if (material == null) { Debug.LogError("RetainerWarp material asset is missing."); return; }

            GameObject root = new GameObject("UI Retainer", typeof(RectTransform), typeof(RawImage), typeof(UIRetainer));
            Undo.RegisterCreatedObjectUndo(root, "Create UI Retainer");
            root.layer = parent.gameObject.layer;
            root.transform.SetParent(parent, false);
            ((RectTransform)root.transform).sizeDelta = new Vector2(640f, 360f);
            GameObject child = new GameObject("Content", typeof(RectTransform));
            child.layer = root.layer;
            child.transform.SetParent(root.transform, false);
            RectTransform content = (RectTransform)child.transform;
            content.anchorMin = Vector2.zero;
            content.anchorMax = Vector2.one;
            content.offsetMin = content.offsetMax = Vector2.zero;
            SerializedObject settings = new SerializedObject(root.GetComponent<UIRetainer>());
            settings.FindProperty("content").objectReferenceValue = content;
            settings.FindProperty("effectMaterial").objectReferenceValue = material;
            settings.ApplyModifiedPropertiesWithoutUndo();
            Selection.activeGameObject = root;
            Debug.Log("Retainer created. Reserve capture layer 31 and exclude it from gameplay cameras. Add UI under Content.", root);
        }
    }
}
