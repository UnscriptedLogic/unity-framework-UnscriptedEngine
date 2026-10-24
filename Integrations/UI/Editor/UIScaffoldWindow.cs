using UnityEditor;
using UnityEngine;
using UnscriptedEngine.Editor.Scaffolding;

namespace UnscriptedEngine.UI.Editor
{
    public sealed class UIScaffoldWindow : ScaffoldWindow
    {
        [SerializeField] private bool widget;
        protected override bool GeneratesScene => false;
        protected override string Heading => "UI setup";
        protected override string DefaultOutputFolder => "Assets/GameAssets/Prefabs/UI";

        [MenuItem("Tools/UnscriptedEngine/Create UI...")]
        public static void Open() { GetWindow<UIScaffoldWindow>("UI Setup"); }

        protected override void DrawOptions(ScaffoldPlan plan)
        {
            widget = EditorGUILayout.Popup("Extend", widget ? 1 : 0,
                new[] { "UCanvasComponent", "UWidgetComponent" }) == 1;
            plan.ui = widget ? UIScaffoldKind.Widget : UIScaffoldKind.Canvas;
            EditorGUILayout.HelpBox(widget
                ? "Creates a UW_ script and widget prefab with RectTransform and CanvasGroup. Place widgets under a UCanvasComponent; membership registers automatically."
                : "Creates a UC_ screen prefab with RectTransform and CanvasGroup. Show screens through UCanvasController, which owns rendering and layers.", MessageType.Info);
            EditorGUILayout.HelpBox("The output folder is the root for the folders shown below. Missing folders are created automatically. Use one EventSystem with the appropriate input module in your scene for UI pointer/navigation input.", MessageType.None);
        }
    }
}
