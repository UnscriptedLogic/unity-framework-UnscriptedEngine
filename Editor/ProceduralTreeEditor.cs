using Framework.Components;
using UnityEditor;
using UnityEngine;

namespace Framework.Editor
{
    [CustomEditor(typeof(ProceduralTree))]
    [CanEditMultipleObjects]
    public sealed class ProceduralTreeEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            if (GUILayout.Button(new GUIContent("Randomize Seed",
                "Choose a new seed for each selected tree. The Seed field remains manually editable.")))
            {
                foreach (Object selectedTarget in targets)
                {
                    SerializedObject treeObject = new SerializedObject(selectedTarget);
                    SerializedProperty seedProperty = treeObject.FindProperty("seed");
                    int previousSeed = seedProperty.intValue;
                    int newSeed = System.Guid.NewGuid().GetHashCode();
                    if (newSeed == previousSeed)
                    {
                        newSeed = unchecked(previousSeed + 1);
                    }

                    seedProperty.intValue = newSeed;
                    // Serialized edits support Undo, prefab overrides, and OnValidate.
                    treeObject.ApplyModifiedProperties();
                }
            }
        }
    }
}
