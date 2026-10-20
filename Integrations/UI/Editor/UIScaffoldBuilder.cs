using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnscriptedEngine.Editor.Scaffolding;
using Object = UnityEngine.Object;

namespace UnscriptedEngine.UI.Editor
{
    public sealed class UIScaffoldBuilder : IUIScaffoldBuilder
    {
        public bool Build(ScaffoldPlan plan)
        {
            string prefix = plan.Prefixes[0];
            string path = plan.PathFor(prefix, "prefab");
            if (!plan.ConfirmConflicts(new[] { path })) return false;
            if (plan.ShouldSkip(path))
            {
                var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (existing == null) throw new IOException("Skipped prefab is missing or invalid: " + path);
                Selection.activeObject = existing;
                return true;
            }
            Type type = ScaffoldGeneration.ResolveScript(plan, prefix);
            Type expected = plan.ui == UIScaffoldKind.Canvas ? typeof(UCanvasComponent) : typeof(UWidgetComponent);
            if (type == null || type.IsAbstract || !expected.IsAssignableFrom(type))
                throw new InvalidOperationException("The generated or skipped script must extend " + expected.Name + ".");

            plan.EnsureOutputFolders();
            using var files = new ScaffoldFileTransaction();
            Scene preview = EditorSceneManager.NewPreviewScene();
            GameObject root = null;
            try
            {
                root = new GameObject(prefix + "_" + plan.name, typeof(RectTransform));
                SceneManager.MoveGameObjectToScene(root, preview);
                ConfigureRoot(root, plan.ui, type);
                files.Capture(plan, path);
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                if (prefab == null) throw new IOException("Failed to save UI prefab: " + path);
                files.Commit();
                Selection.activeObject = prefab;
                return true;
            }
            finally
            {
                if (root != null) Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        /// <summary>Shared prefab defaults, also usable by future UI templates.</summary>
        public static void ConfigureRoot(GameObject root, UIScaffoldKind kind, Type componentType)
        {
            if (kind != UIScaffoldKind.Canvas && kind != UIScaffoldKind.Widget)
                throw new ArgumentException("Choose Canvas or Widget.", nameof(kind));
            Type expected = kind == UIScaffoldKind.Canvas ? typeof(UCanvasComponent) : typeof(UWidgetComponent);
            if (componentType == null || componentType.IsAbstract || !expected.IsAssignableFrom(componentType))
                throw new ArgumentException("Component must extend " + expected.Name, nameof(componentType));
            root.layer = LayerMask.NameToLayer("UI");
            var rect = root.GetComponent<RectTransform>() ?? root.AddComponent<RectTransform>();
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition3D = Vector3.zero;
            rect.sizeDelta = Vector2.zero;
            if (root.GetComponent<CanvasGroup>() == null) root.AddComponent<CanvasGroup>();
            if (root.GetComponent(componentType) == null) root.AddComponent(componentType);
        }
    }
}
