using UnityEditor;

namespace UnscriptedEngine.Editor
{
    [CustomEditor(typeof(UChest), true)]
    [CanEditMultipleObjects]
    public sealed class UChestEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "contentsMode", "predefinedItems", "lootTable", "customProvider");
            EditorGUILayout.Space();
            DrawContents(serializedObject);
            serializedObject.ApplyModifiedProperties();
        }

        // Shared by the chest Inspector and creation window.
        public static void DrawContents(SerializedObject target)
        {
            SerializedProperty mode = target.FindProperty("contentsMode");
            EditorGUILayout.LabelField("Contents", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(mode);
            if (mode.hasMultipleDifferentValues) return;
            switch ((ChestContentsMode)mode.enumValueIndex)
            {
                case ChestContentsMode.Predefined:
                    EditorGUILayout.PropertyField(target.FindProperty("predefinedItems"), true);
                    break;
                case ChestContentsMode.Chance:
                    EditorGUILayout.PropertyField(target.FindProperty("lootTable"));
                    EditorGUILayout.HelpBox("Each table entry rolls independently once per chest. 100% always appears; 0% never appears.", MessageType.Info);
                    break;
                case ChestContentsMode.Custom:
                    EditorGUILayout.PropertyField(target.FindProperty("customProvider"));
                    EditorGUILayout.HelpBox("Assign a ChestLootProvider asset, or override GenerateContents in a chest subclass. Recipes run once on server/offline authority.", MessageType.Info);
                    break;
            }
        }
    }
}
