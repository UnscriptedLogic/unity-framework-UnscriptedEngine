using UnityEditor;
using UnityEngine;

namespace UnscriptedEngine.UI.Editor
{
    public sealed class UIRetainerPreviewWindow : EditorWindow
    {
        [SerializeField]
        private UIRetainer retainer;
        [SerializeField]
        private bool live = true;
        [SerializeField]
        private bool usePanelSize = true;
        [SerializeField]
        private Vector2 previewSize = new Vector2(960f, 540f);
        private UIRetainerPreviewSession session;
        private Texture2D checker;
        private double nextUpdate;

        [MenuItem("Tools/UnscriptedEngine/UI/Retainer Live Preview")]
        private static void OpenSelected()
        {
            UIRetainer selected = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<UIRetainer>() : null;
            Open(selected);
        }

        public static UIRetainerPreviewWindow Open(UIRetainer target)
        {
            UIRetainerPreviewWindow window = GetWindow<UIRetainerPreviewWindow>("Retainer Preview");
            window.SetTarget(target);
            window.live = true;
            window.minSize = new Vector2(360f, 300f);
            window.Show();
            return window;
        }

        private void OnEnable()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += HandlePlayMode;
            EditorApplication.projectChanged += Invalidate;
            EditorApplication.quitting += ReleaseSession;
            AssemblyReloadEvents.beforeAssemblyReload += ReleaseSession;
            Undo.undoRedoPerformed += Invalidate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= HandlePlayMode;
            EditorApplication.projectChanged -= Invalidate;
            EditorApplication.quitting -= ReleaseSession;
            AssemblyReloadEvents.beforeAssemblyReload -= ReleaseSession;
            Undo.undoRedoPerformed -= Invalidate;
            ReleaseSession();
            if (checker != null) DestroyImmediate(checker);
            checker = null;
        }

        private void SetTarget(UIRetainer target)
        {
            ReleaseSession();
            retainer = target;
            nextUpdate = 0d;
            Repaint();
        }

        private void HandlePlayMode(PlayModeStateChange state)
        {
            ReleaseSession();
            nextUpdate = 0d;
            Repaint();
        }
        private void Invalidate() { session?.Invalidate(); nextUpdate = 0d; }

        private Vector2 LogicalSize
        {
            get
            {
                if (usePanelSize && retainer != null)
                {
                    Vector2 size = ((RectTransform)retainer.transform).rect.size;
                    if (size.x > 1f && size.y > 1f) return size;
                }
                return new Vector2(Mathf.Clamp(previewSize.x, 1f, 8192f), Mathf.Clamp(previewSize.y, 1f, 8192f));
            }
        }

        private void Tick()
        {
            if (!live || retainer == null || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if (session != null) { ReleaseSession(); Repaint(); }
                return;
            }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.timeSinceStartup < nextUpdate) return;
            nextUpdate = EditorApplication.timeSinceStartup + 0.1d;
            if (session == null || session.Source != retainer)
            {
                ReleaseSession();
                session = new UIRetainerPreviewSession(retainer);
            }
            if (session.UpdatePreview(LogicalSize)) Repaint();
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Live visual preview of authored UI. Edit Curvature / Perspective on the component; changes remain normal Edit Mode values. Source objects stay untouched.", MessageType.Info);
            EditorGUI.BeginChangeCheck();
            UIRetainer selected = (UIRetainer)EditorGUILayout.ObjectField("Retainer", retainer, typeof(UIRetainer), true);
            if (EditorGUI.EndChangeCheck()) SetTarget(selected);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                live = GUILayout.Toggle(live, "Live Preview", "Button");
                if (EditorGUI.EndChangeCheck() && !live) ReleaseSession();
                if (GUILayout.Button("Use Selection"))
                    SetTarget(Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<UIRetainer>() : null);
                if (GUILayout.Button("Refresh")) Invalidate();
            }
            EditorGUI.BeginChangeCheck();
            usePanelSize = EditorGUILayout.Toggle("Use Panel Size", usePanelSize);
            if (!usePanelSize || retainer == null || ((RectTransform)retainer.transform).rect.width <= 1f || ((RectTransform)retainer.transform).rect.height <= 1f)
                previewSize = EditorGUILayout.Vector2Field("Preview Size", previewSize);
            if (EditorGUI.EndChangeCheck()) Invalidate();

            if (EditorApplication.isPlayingOrWillChangePlaymode)
                EditorGUILayout.HelpBox("Preview is suspended during Play Mode. Use the Game view for runtime UI.", MessageType.Info);
            else if (retainer == null)
                EditorGUILayout.HelpBox("Choose a scene, Prefab Mode, or prefab-asset retainer. This window stays pinned while you edit its children.", MessageType.Info);
            else if (session?.Error != null) EditorGUILayout.HelpBox(session.Error, MessageType.Warning);

            Rect pane = GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(pane, new Color(0.08f, 0.08f, 0.08f));
            if (session?.Texture == null || session.Material == null || session.Error != null) return;
            float aspect = LogicalSize.x / LogicalSize.y;
            float width = Mathf.Min(pane.width - 16f, (pane.height - 16f) * aspect);
            if (width <= 0f) return;
            Rect image = new Rect(pane.center.x - width * 0.5f, pane.center.y - width / aspect * 0.5f, width, width / aspect);
            EnsureChecker();
            GUI.DrawTextureWithTexCoords(image, checker, new Rect(0f, 0f, image.width / 24f, image.height / 24f));
            if (Event.current.type == EventType.Repaint)
            {
                bool previousSRGB = GL.sRGBWrite;
                try
                {
                    GL.sRGBWrite = false;
                    Graphics.DrawTexture(image, session.Texture, new Rect(0f, 0f, 1f, 1f), 0, 0, 0, 0, Color.white, session.Material);
                }
                finally { GL.sRGBWrite = previousSRGB; }
            }
            GUI.Label(new Rect(pane.x + 8f, pane.y + 4f, pane.width - 16f, 20f), $"{session.Texture.width} x {session.Texture.height} px / visual-only / up to 10 updates per second", EditorStyles.miniLabel);
        }

        private void EnsureChecker()
        {
            if (checker != null) return;
            checker = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
            Color dark = new Color(0.14f, 0.14f, 0.14f);
            Color light = new Color(0.2f, 0.2f, 0.2f);
            checker.SetPixels(new[] {dark, light, light, dark});
            checker.Apply();
        }

        private void ReleaseSession() { session?.Dispose(); session = null; }
    }
}
