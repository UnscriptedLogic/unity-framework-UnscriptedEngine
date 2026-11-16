using UnityEditor;
using UnityEngine;

namespace UnscriptedEngine.UI.Editor
{
    [CustomEditor(typeof(UIRetainer))]
    [CanEditMultipleObjects]
    public sealed class UIRetainerInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || targets.Length != 1))
                if (GUILayout.Button("Open Live Preview")) UIRetainerPreviewWindow.Open((UIRetainer)target);
            EditorGUILayout.HelpBox("Preview uses an isolated visual copy. Source hierarchy, prefab links, and runtime resources are not modified.", MessageType.None);
        }
    }
}
