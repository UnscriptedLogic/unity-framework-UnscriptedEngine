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

        [MenuItem("Tools/UnscriptedEngine/Create UI...")]
        public static void Open() { GetWindow<UIScaffoldWindow>("UI Setup"); }

        protected override void DrawOptions(ScaffoldPlan plan)
        {
            widget = EditorGUILayout.Popup("Extend", widget ? 1 : 0,
                new[] { "UCanvasComponent", "UWidgetComponent" }) == 1;
            plan.ui = widget ? UIScaffoldKind.Widget : UIScaffoldKind.Canvas;
            EditorGUILayout.HelpBox(widget
                ? "Creates a UW_ script and widget prefab with RectTransform and CanvasGroup. Attach widgets through an initialized UCanvasComponent."
                : "Creates a UC_ script and screen-space overlay canvas prefab with RectTransform, Canvas, CanvasScaler, and GraphicRaycaster. Scaling: 1920 x 1080, match width/height 0.5.", MessageType.Info);
            EditorGUILayout.HelpBox("The output folder is the root for the folders shown below. Missing folders are created automatically. Use one EventSystem with the appropriate input module in your scene for UI pointer/navigation input.", MessageType.None);
        }
    }
}
