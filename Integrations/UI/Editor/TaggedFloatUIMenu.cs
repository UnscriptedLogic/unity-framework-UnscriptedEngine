using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace UnscriptedEngine.UI.Editor
{
    internal static class TaggedFloatUIMenu
    {
        [MenuItem("Tools/UnscriptedEngine/UI/Tagged Value/Slider")]
        private static void CreateSlider() => Create("Slider");
        [MenuItem("Tools/UnscriptedEngine/UI/Tagged Value/Text")]
        private static void CreateText() => Create("Text");
        [MenuItem("Tools/UnscriptedEngine/UI/Tagged Value/Filled Image")]
        private static void CreateImage() => Create("Filled Image");
        [MenuItem("Tools/UnscriptedEngine/UI/Tagged Value/Event Binding")]
        private static void CreateBinding() => Create("Event Binding");

        [MenuItem("Tools/UnscriptedEngine/UI/Reactive Animations/Add Hide to Selected")]
        private static void AddHide() => AddAnimation(typeof(UIAnimReactive_Hide));
        [MenuItem("Tools/UnscriptedEngine/UI/Reactive Animations/Add Shake to Selected")]
        private static void AddShake() => AddAnimation(typeof(UIAnimReactive_Shake));
        [MenuItem("Tools/UnscriptedEngine/UI/Reactive Animations/Add Hide to Selected", true)]
        [MenuItem("Tools/UnscriptedEngine/UI/Reactive Animations/Add Shake to Selected", true)]
        private static bool CanAddAnimation() => Selection.activeGameObject != null && Selection.activeGameObject.GetComponent<RectTransform>() != null;

        private static void AddAnimation(System.Type type)
        {
            GameObject selected = Selection.activeGameObject;
            if (selected.GetComponent(type) == null) Undo.AddComponent(selected, type);
        }

        private static void Create(string kind)
        {
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Create Tagged Value " + kind);
            Transform parent = Selection.activeTransform;
            if (parent == null || parent.GetComponentInParent<Canvas>(true) == null)
            {
                GameObject canvasObject = CreateObject("Value Canvas", null, typeof(Canvas), typeof(CanvasScaler));
                canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                parent = canvasObject.transform;
            }
            GameObject root = CreateObject("Tagged Value " + kind, parent, typeof(TaggedFloatBinding));
            ((RectTransform)root.transform).sizeDelta = new Vector2(240f, 32f);
            if (kind != "Event Binding")
            {
                TaggedFloatDisplay display = Undo.AddComponent<TaggedFloatDisplay>(root);
                SerializedObject settings = new SerializedObject(display);
                if (kind == "Text")
                {
                    TextMeshProUGUI text = Undo.AddComponent<TextMeshProUGUI>(root);
                    text.text = "0 / 100";
                    text.fontSize = 24f;
                    text.alignment = TextAlignmentOptions.Center;
                    text.raycastTarget = false;
                    settings.FindProperty("label").objectReferenceValue = text;
                }
                else
                {
                    Image background = Undo.AddComponent<Image>(root);
                    background.color = new Color(0.12f, 0.12f, 0.12f, 1f);
                    background.raycastTarget = false;
                    GameObject fillObject = CreateObject("Fill", root.transform, typeof(Image));
                    RectTransform fillRect = (RectTransform)fillObject.transform;
                    fillRect.anchorMin = Vector2.zero;
                    fillRect.anchorMax = Vector2.one;
                    fillRect.sizeDelta = Vector2.zero;
                    Image fill = fillObject.GetComponent<Image>();
                    fill.color = new Color(0.25f, 0.8f, 0.35f, 1f);
                    fill.raycastTarget = false;
                    if (kind == "Slider")
                    {
                        Slider slider = Undo.AddComponent<Slider>(root);
                        slider.fillRect = fillRect;
                        slider.targetGraphic = background;
                        slider.transition = Selectable.Transition.None;
                        slider.interactable = false;
                        slider.navigation = new Navigation { mode = Navigation.Mode.None };
                        slider.value = 1f;
                        settings.FindProperty("slider").objectReferenceValue = slider;
                    }
                    else
                    {
                        // Filled Images require a sprite to generate filled geometry.
                        fill.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
                        fill.type = Image.Type.Filled;
                        fill.fillMethod = Image.FillMethod.Horizontal;
                        settings.FindProperty("fillImage").objectReferenceValue = fill;
                    }
                }
                settings.ApplyModifiedProperties();
            }
            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(root.scene);
            Undo.CollapseUndoOperations(group);
        }

        private static GameObject CreateObject(string name, Transform parent, params System.Type[] components)
        {
            GameObject created = new GameObject(name, typeof(RectTransform));
            StageUtility.PlaceGameObjectInCurrentStage(created);
            if (parent != null) SceneManager.MoveGameObjectToScene(created, parent.gameObject.scene);
            Undo.RegisterCreatedObjectUndo(created, "Create " + name);
            if (parent != null)
            {
                Undo.SetTransformParent(created.transform, parent, "Parent " + name);
                created.layer = parent.gameObject.layer;
            }
            created.transform.localScale = Vector3.one;
            created.transform.localRotation = Quaternion.identity;
            ((RectTransform)created.transform).anchoredPosition3D = Vector3.zero;
            foreach (System.Type component in components) Undo.AddComponent(created, component);
            return created;
        }
    }
}
