using UnityEditor;
using UnityEngine;

namespace UnscriptedEngine.Editor
{
    [CustomPropertyDrawer(typeof(UItemFragEquip))]
    public sealed class UItemFragEquipDrawer : PropertyDrawer
    {
        private static float Gap => EditorGUIUtility.standardVerticalSpacing;
        private static float Line => EditorGUIUtility.singleLineHeight;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded) return Line;

            return Line * 2 + Gap * 4
                + EditorGUI.GetPropertyHeight(property.FindPropertyRelative("visualPrefab"), true)
                + EditorGUI.GetPropertyHeight(property.FindPropertyRelative("anchorPosition"), true)
                + EditorGUI.GetPropertyHeight(property.FindPropertyRelative("anchorRotation"), true);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            Rect row = new Rect(position.x, position.y, position.width, Line);
            property.isExpanded = EditorGUI.Foldout(row, property.isExpanded, label, true);

            if (property.isExpanded)
            {
                int previousIndent = EditorGUI.indentLevel;
                EditorGUI.indentLevel++;

                DrawField(ref row, property.FindPropertyRelative("visualPrefab"));

                row.y += row.height + Gap;
                row.height = Line;
                Rect content = EditorGUI.IndentedRect(row);
                Rect button = new Rect(content.xMax - 90, content.y, 90, Line);
                Rect heading = new Rect(content.x, content.y, Mathf.Max(0, content.width - 94), Line);
                GUI.Label(heading, "Anchor pose", EditorStyles.boldLabel);

                if (EditorGUI.DropdownButton(button, new GUIContent("Presets", "Save or apply project-wide left/right hand offsets."), FocusType.Keyboard))
                {
                    GenericMenu menu = new GenericMenu();
                    ItemEquipPoseMenu.AddItems(menu, property, "");
                    menu.DropDown(button);
                }

                DrawField(ref row, property.FindPropertyRelative("anchorPosition"));
                DrawField(ref row, property.FindPropertyRelative("anchorRotation"));

                EditorGUI.indentLevel = previousIndent;
            }

            EditorGUI.EndProperty();
        }

        private static void DrawField(ref Rect row, SerializedProperty property)
        {
            row.y += row.height + Gap;
            row.height = EditorGUI.GetPropertyHeight(property, true);
            EditorGUI.PropertyField(row, property, true);
        }
    }

    [InitializeOnLoad]
    public static class ItemEquipPoseMenu
    {
        static ItemEquipPoseMenu()
        {
            EditorApplication.contextualPropertyMenu += AddContextItems;
        }

        private static void AddContextItems(GenericMenu menu, SerializedProperty property)
        {
            SerializedProperty fragment = property.Copy();

            while (fragment != null)
            {
                if (fragment.propertyType == SerializedPropertyType.ManagedReference
                    && fragment.managedReferenceValue is UItemFragEquip)
                {
                    AddItems(menu, fragment, "Anchor Pose/");
                    return;
                }

                int divider = fragment.propertyPath.LastIndexOf('.');
                if (divider < 0) return;
                fragment = property.serializedObject.FindProperty(fragment.propertyPath.Substring(0, divider));
            }
        }

        public static void AddItems(GenericMenu menu, SerializedProperty fragment, string prefix)
        {
            // Resolve afresh when clicked; refuse to edit a different fragment if the list moved.
            Object[] targets = fragment.serializedObject.targetObjects;
            string path = fragment.propertyPath;
            long referenceId = fragment.managedReferenceId;

            SerializedProperty Resolve(out SerializedObject serialized)
            {
                serialized = null;
                foreach (Object target in targets)
                {
                    if (target == null) return null;
                }

                serialized = new SerializedObject(targets);
                serialized.Update();
                SerializedProperty current = serialized.FindProperty(path);

                return current != null && current.propertyType == SerializedPropertyType.ManagedReference
                    && current.managedReferenceId == referenceId && current.managedReferenceValue is UItemFragEquip
                    ? current : null;
            }

            void AddHand(ItemHandPreset hand, string title)
            {
                if (ItemEquipPosePresets.instance.TryGet(hand, out Vector3 position, out Vector3 rotation))
                {
                    menu.AddItem(new GUIContent(prefix + "Apply " + title), false, () =>
                    {
                        SerializedProperty current = Resolve(out SerializedObject serialized);
                        using (serialized)
                        {
                            if (current != null) ItemEquipPosePresets.ApplyPose(current, position, rotation);
                        }
                    });
                }
                else
                {
                    menu.AddDisabledItem(new GUIContent(prefix + "Apply " + title + " (not saved yet)"));
                }

                GUIContent saveLabel = new GUIContent(prefix + "Save Current Pose as " + title);
                if (targets.Length != 1)
                {
                    menu.AddDisabledItem(saveLabel);
                    return;
                }

                menu.AddItem(saveLabel, false, () =>
                {
                    SerializedProperty current = Resolve(out SerializedObject serialized);
                    using (serialized)
                    {
                        if (current == null) return;

                        ItemEquipPosePresets.instance.SavePose(hand,
                            current.FindPropertyRelative("anchorPosition").vector3Value,
                            current.FindPropertyRelative("anchorRotation").vector3Value);
                    }
                });
            }

            AddHand(ItemHandPreset.LeftHand, "Left Hand");
            menu.AddSeparator(prefix);
            AddHand(ItemHandPreset.RightHand, "Right Hand");
        }
    }
}
