#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace UnscriptedEngine.Editor
{
    [CustomEditor(typeof(ItemDataSO), true)]
    public class ItemDataSOEditor : UnityEditor.Editor
    {
        private ReorderableList fragments;

        private void OnEnable()
        {
            var property = serializedObject.FindProperty(nameof(ItemDataSO.fragments));
            fragments = new ReorderableList(serializedObject, property, true, true, true, true);
            fragments.drawHeaderCallback = rect => EditorGUI.LabelField(rect, property.displayName);
            fragments.elementHeightCallback = index =>
                EditorGUI.GetPropertyHeight(property.GetArrayElementAtIndex(index), true) + 4;
            fragments.drawElementCallback = (rect, index, active, focused) =>
            {
                var element = property.GetArrayElementAtIndex(index);
                rect.y += 2;
                rect.height = EditorGUI.GetPropertyHeight(element, true);
                var label = element.managedReferenceValue == null
                    ? "None"
                    : ObjectNames.NicifyVariableName(element.managedReferenceValue.GetType().Name);
                EditorGUI.PropertyField(rect, element, new GUIContent(label), true);
            };
            fragments.onAddDropdownCallback = (rect, list) => ShowFragmentMenu(rect);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, nameof(ItemDataSO.fragments));
            fragments.DoLayoutList();
            serializedObject.ApplyModifiedProperties();
        }

        private void ShowFragmentMenu(Rect rect)
        {
            var menu = new GenericMenu();
            var types = TypeCache.GetTypesDerivedFrom<UItemFragment>()
                .Where(type => !type.IsAbstract && !type.ContainsGenericParameters
                    && type.IsDefined(typeof(SerializableAttribute), false)
                    && type.GetConstructor(Type.EmptyTypes) != null)
                .OrderBy(type => type.FullName)
                .ToArray();

            foreach (var type in types)
            {
                var fragmentType = type;
                menu.AddItem(new GUIContent(type.FullName.Replace('.', '/')), false, () =>
                {
                    if (target == null)
                        return;

                    var instance = (UItemFragment)Activator.CreateInstance(fragmentType);
                    serializedObject.Update();
                    var property = serializedObject.FindProperty(nameof(ItemDataSO.fragments));
                    var index = property.arraySize;
                    property.arraySize++;
                    var element = property.GetArrayElementAtIndex(index);
                    element.managedReferenceValue = instance;
                    element.isExpanded = true;
                    serializedObject.ApplyModifiedProperties();
                    fragments.index = index;
                });
            }

            if (types.Length == 0)
                menu.AddDisabledItem(new GUIContent("No serializable fragment types found"));

            menu.DropDown(rect);
        }
    }
}
#endif
