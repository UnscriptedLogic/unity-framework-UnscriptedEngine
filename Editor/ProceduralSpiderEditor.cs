using Framework.Components;
using UnityEditor;
using UnityEngine;

namespace Framework.Editor
{
    public sealed class ProceduralSpiderSetup : EditorWindow
    {
        private int legCount = 8;
        private float attachmentRadius = 0.35f;
        private float sectionLength = 0.9f;
        private int sectionCount = 2;
        private float firstBend = 40f;
        private float subsequentBend = -110f;
        private Material sharedMaterial;

        [MenuItem("Tools/UnscriptedEngine/Procedural/Spider Setup")]
        public static void Open() { GetWindow<ProceduralSpiderSetup>("Spider Setup"); }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Creates a new spider root with independent legs. Local Y points away from the surface. Resize the root using positive uniform Transform Scale, including during Play Mode. Dimensions are authored at scale 1; step timing stays unchanged. Body movement is supplied by your own controller.", MessageType.Info);
            legCount = Mathf.Clamp(EditorGUILayout.IntField(new GUIContent("Leg Count", "Any count from 1 to 64, distributed around the body."), legCount), 1, 64);
            attachmentRadius = Mathf.Max(0f, EditorGUILayout.FloatField(new GUIContent("Attachment Radius", "Distance of each leg root from the body center."), attachmentRadius));
            sectionCount = Mathf.Clamp(EditorGUILayout.IntField(new GUIContent("Sections Per Leg", "Initial number of blocks. Edit individual legs afterward."), sectionCount), 1, 16);
            sectionLength = Mathf.Max(0.01f, EditorGUILayout.FloatField(new GUIContent("Section Length", "Initial length of every block at unit root scale."), sectionLength));
            firstBend = EditorGUILayout.Slider(new GUIContent("First Bend", "Degrees above the outward body plane."), firstBend, -175f, 175f);
            subsequentBend = EditorGUILayout.Slider(new GUIContent("Following Bends", "Signed rest angle added at each following joint."), subsequentBend, -175f, 175f);
            sharedMaterial = (Material)EditorGUILayout.ObjectField(new GUIContent("Material", "Shared material assigned to all blocks."), sharedMaterial, typeof(Material), false);
            if (GUILayout.Button("Create Spider")) CreateSpider();
        }

        private void CreateSpider()
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Create Procedural Spider");
            GameObject root = new GameObject("Procedural Spider");
            Undo.RegisterCreatedObjectUndo(root, "Create Procedural Spider");
            if (SceneView.lastActiveSceneView != null)
                root.transform.position = SceneView.lastActiveSceneView.pivot + Vector3.up;
            ProceduralSpider brain = Undo.AddComponent<ProceduralSpider>(root);
            for (int index = 0; index < legCount; index++)
            {
                GameObject legObject = new GameObject("Leg " + (index + 1));
                Undo.RegisterCreatedObjectUndo(legObject, "Create Procedural Spider");
                Undo.SetTransformParent(legObject.transform, root.transform, "Create Procedural Spider");
                float angle = 360f * index / legCount;
                legObject.transform.localRotation = Quaternion.Euler(0f, angle, 0f);
                legObject.transform.localPosition = legObject.transform.localRotation * Vector3.forward * attachmentRadius;
                ProceduralSpiderLeg leg = Undo.AddComponent<ProceduralSpiderLeg>(legObject);
                SpiderLegSection[] shape = new SpiderLegSection[sectionCount];
                for (int section = 0; section < sectionCount; section++)
                    shape[section] = new SpiderLegSection(sectionLength, section == 0 ? firstBend : subsequentBend);
                leg.Configure(shape, sharedMaterial);
                leg.RebuildGeometry();
                for (int child = 0; child < legObject.transform.childCount; child++)
                    Undo.RegisterCreatedObjectUndo(legObject.transform.GetChild(child).gameObject, "Create Procedural Spider");
                EditorUtility.SetDirty(leg);
            }
            brain.CollectChildLegs();
            EditorUtility.SetDirty(brain);
            Selection.activeGameObject = root;
            Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
        }
    }

    [CustomEditor(typeof(ProceduralSpider))]
    public sealed class ProceduralSpiderEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("Resize the whole spider with positive uniform root Transform Scale (for example 0.1, 1 or 10 on all axes). Lengths, thickness, probes and step distances scale together. Nonuniform or negative hierarchy scale is unsupported.", MessageType.Info);
            if (GUILayout.Button("Collect Child Legs"))
            {
                Undo.RecordObject(target, "Collect Spider Legs");
                ((ProceduralSpider)target).CollectChildLegs();
                EditorUtility.SetDirty(target);
            }
        }
    }

    [CustomEditor(typeof(ProceduralSpiderLeg))]
    [CanEditMultipleObjects]
    public sealed class ProceduralSpiderLegEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("Angles define rest shape, not hard joint limits. IK bends to reach planted feet. After shape edits, rebuild geometry. Avoid Obstacles checks block volumes against Surface Layers and swivels joints around the planted foot. Wall Clearance adds a size-scaled margin. Exclude body colliders using Surface Layers. Impossible clearances can still clip; red gizmos show blocked legs.", MessageType.Info);
            if (GUILayout.Button("Rebuild Geometry")) RebuildSelected(null);
            if (GUILayout.Button("Preset: Spider (2 Sections)"))
                RebuildSelected(new[] { new SpiderLegSection(0.8f, 40f), new SpiderLegSection(1.1f, -110f) });
            if (GUILayout.Button("Preset: Long Reach (3 Sections)"))
                RebuildSelected(new[] { new SpiderLegSection(0.5f, 35f), new SpiderLegSection(0.8f, -35f), new SpiderLegSection(1f, -75f) });
        }

        private void RebuildSelected(SpiderLegSection[] preset)
        {
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Rebuild Spider Leg");
            foreach (Object selected in targets)
            {
                ProceduralSpiderLeg leg = (ProceduralSpiderLeg)selected;
                Undo.RegisterFullObjectHierarchyUndo(leg.gameObject, "Rebuild Spider Leg");
                // Remove through Undo before the runtime builder runs, preserving destroyed objects on undo.
                SerializedObject legObject = new SerializedObject(leg);
                SerializedProperty blocksProperty = legObject.FindProperty("blocks");
                for (int index = 0; index < blocksProperty.arraySize; index++)
                {
                    Transform block = blocksProperty.GetArrayElementAtIndex(index).objectReferenceValue as Transform;
                    if (block != null) Undo.DestroyObjectImmediate(block.gameObject);
                }
                blocksProperty.ClearArray();
                legObject.ApplyModifiedProperties();
                if (preset != null)
                {
                    Material material = legObject.FindProperty("material").objectReferenceValue as Material;
                    leg.Configure(preset, material);
                }
                leg.RebuildGeometry();
                SerializedObject rebuilt = new SerializedObject(leg);
                SerializedProperty blocks = rebuilt.FindProperty("blocks");
                for (int index = 0; index < blocks.arraySize; index++)
                {
                    Transform block = blocks.GetArrayElementAtIndex(index).objectReferenceValue as Transform;
                    if (block != null) Undo.RegisterCreatedObjectUndo(block.gameObject, "Rebuild Spider Leg");
                }
                PrefabUtility.RecordPrefabInstancePropertyModifications(leg);
                EditorUtility.SetDirty(leg);
            }
            Undo.CollapseUndoOperations(undoGroup);
        }
    }
}
