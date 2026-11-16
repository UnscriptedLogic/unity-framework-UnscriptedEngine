using System;
using System.Collections.Generic;
using Framework.GameplayTag;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace UnscriptedEngine.Editor
{
    [CustomPropertyDrawer(typeof(GameplayTag))]
    public sealed class GameplayTagDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            Rect field = position;
            field.width -= 58f;
            EditorGUI.ObjectField(field, property, typeof(GameplayTag), label);

            Rect button = new Rect(field.xMax + 4f, position.y, 54f, position.height);
            if (EditorGUI.DropdownButton(button, new GUIContent("Tags", "Browse gameplay tags by dotted path."), FocusType.Keyboard))
            {
                // Resolve a fresh property when the popup closes: Inspector properties can be disposed meanwhile.
                UnityEngine.Object[] targets = property.serializedObject.targetObjects;
                string propertyPath = property.propertyPath;
                var dropdown = new GameplayTagDropdown(tag => AssignTag(targets, propertyPath, tag));
                dropdown.Show(position);
            }
            EditorGUI.EndProperty();
        }

        private static void AssignTag(UnityEngine.Object[] targets, string propertyPath, GameplayTag tag)
        {
            foreach (UnityEngine.Object target in targets)
            {
                if (target == null) continue;
                using (var serialized = new SerializedObject(target))
                {
                    SerializedProperty property = serialized.FindProperty(propertyPath);
                    if (property == null || property.propertyType != SerializedPropertyType.ObjectReference) continue;
                    property.objectReferenceValue = tag;
                    serialized.ApplyModifiedProperties();
                }
            }
        }
    }

    internal sealed class GameplayTagDropdown : AdvancedDropdown
    {
        private readonly Action<GameplayTag> selectTag;
        private readonly Dictionary<int, GameplayTag> tagsById = new Dictionary<int, GameplayTag>();

        public GameplayTagDropdown(Action<GameplayTag> selectTag) : base(new AdvancedDropdownState())
        {
            this.selectTag = selectTag;
            minimumSize = new Vector2(340f, 360f);
        }

        protected override AdvancedDropdownItem BuildRoot()
        {
            tagsById.Clear();
            var root = new AdvancedDropdownItem("Gameplay Tags") { id = 0 };
            AddSelection(root, "None", null);
            var tags = new List<GameplayTag>();
            foreach (string guid in AssetDatabase.FindAssets("t:GameplayTag"))
            {
                var tag = AssetDatabase.LoadAssetAtPath<GameplayTag>(AssetDatabase.GUIDToAssetPath(guid));
                if (tag != null) tags.Add(tag);
            }
            tags.Sort((left, right) =>
            {
                int comparison = string.Compare(left.Path, right.Path, StringComparison.OrdinalIgnoreCase);
                return comparison != 0 ? comparison : string.CompareOrdinal(AssetDatabase.GetAssetPath(left), AssetDatabase.GetAssetPath(right));
            });

            var pathCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (GameplayTag tag in tags)
            {
                string path = GetDisplayPath(tag);
                pathCounts.TryGetValue(path, out int count);
                pathCounts[path] = count + 1;
            }
            var categories = new Dictionary<string, AdvancedDropdownItem>(StringComparer.Ordinal);
            foreach (GameplayTag tag in tags)
            {
                string path = GetDisplayPath(tag);
                string[] segments = path.Split('.');
                AdvancedDropdownItem category = root;
                string categoryPath = string.Empty;
                for (int i = 0; i < segments.Length - 1; i++)
                {
                    categoryPath += "." + segments[i];
                    if (!categories.TryGetValue(categoryPath, out AdvancedDropdownItem child))
                    {
                        child = new AdvancedDropdownItem(string.IsNullOrEmpty(segments[i]) ? "(Empty segment)" : segments[i]);
                        category.AddChild(child);
                        child.id = -categories.Count - 1;
                        categories.Add(categoryPath, child);
                    }
                    category = child;
                }
                // Full paths keep search results clear, including assets with duplicate authored paths.
                string label = pathCounts[path] > 1 ? path + "  [" + AssetDatabase.GetAssetPath(tag) + "]" : path;
                AddSelection(category, label, tag);
            }
            return root;
        }

        private static string GetDisplayPath(GameplayTag tag)
        {
            return string.IsNullOrWhiteSpace(tag.Path) ? "(No path)." + tag.name : tag.Path;
        }

        private void AddSelection(AdvancedDropdownItem parent, string label, GameplayTag tag)
        {
            int id = tagsById.Count + 1;
            tagsById.Add(id, tag);
            AdvancedDropdownItem item = new AdvancedDropdownItem(label);
            parent.AddChild(item);
            // Unity recomputes the child's ID in AddChild, so assign our lookup ID afterward.
            item.id = id;
        }

        protected override void ItemSelected(AdvancedDropdownItem item)
        {
            if (tagsById.TryGetValue(item.id, out GameplayTag tag)) selectTag(tag);
        }
    }
}
