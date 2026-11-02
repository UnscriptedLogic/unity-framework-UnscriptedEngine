using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace UnscriptedEngine.UI.Editor
{
    /// <summary>Creates a non-interactive composition overlay using a shared material.</summary>
    internal static class GoldenRatioGridMenu
    {
        private const string MaterialGuid = "c7d23b2dd3f541bfa6cc99c979523498";

        [MenuItem("Tools/UnscriptedEngine/UI/GoldenRatioGrid")]
        private static void CreateGrid()
        {
            string materialPath = AssetDatabase.GUIDToAssetPath(MaterialGuid);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = Resources.Load<Material>("UnscriptedEngine/GoldenRatioGrid");
            }
            if (material == null)
            {
                Debug.LogError("GoldenRatioGrid material could not be loaded. Reimport the UnscriptedEngine package " +
                    "and check that Resources/UnscriptedEngine/GoldenRatioGrid.mat is present.");
                return;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Create Golden Ratio Grid");

            RectTransform parent = Selection.activeTransform as RectTransform;
            // A RectTransform outside a Canvas still needs a rendering root.
            if (parent == null || parent.GetComponentInParent<Canvas>(true) == null)
            {
                GameObject canvasObject = new GameObject("Golden Ratio Canvas",
                    typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                StageUtility.PlaceGameObjectInCurrentStage(canvasObject);
                if (parent != null)
                {
                    SceneManager.MoveGameObjectToScene(canvasObject, parent.gameObject.scene);
                }
                Undo.RegisterCreatedObjectUndo(canvasObject, "Create Golden Ratio Canvas");
                if (parent != null)
                {
                    Undo.SetTransformParent(canvasObject.transform, parent, "Parent Golden Ratio Canvas");
                }
                Canvas canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                parent = (RectTransform)canvasObject.transform;
            }

            GameObject gridObject = new GameObject("Golden Ratio Grid",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
            StageUtility.PlaceGameObjectInCurrentStage(gridObject);
            SceneManager.MoveGameObjectToScene(gridObject, parent.gameObject.scene);
            Undo.RegisterCreatedObjectUndo(gridObject, "Create Golden Ratio Grid");
            Undo.SetTransformParent(gridObject.transform, parent, "Parent Golden Ratio Grid");
            gridObject.layer = parent.gameObject.layer;

            RectTransform rect = (RectTransform)gridObject.transform;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.anchoredPosition3D = Vector3.zero;
            rect.sizeDelta = Vector2.zero;
            rect.SetAsLastSibling();

            Image image = gridObject.GetComponent<Image>();
            image.type = Image.Type.Simple;
            image.color = Color.white;
            image.raycastTarget = false;
            image.material = material;
            gridObject.GetComponent<LayoutElement>().ignoreLayout = true;

            EditorSceneManager.MarkSceneDirty(gridObject.scene);
            Selection.activeGameObject = gridObject;
            Undo.CollapseUndoOperations(undoGroup);
        }
    }
}
