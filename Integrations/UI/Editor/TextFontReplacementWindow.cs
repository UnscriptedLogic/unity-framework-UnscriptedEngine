using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace UnscriptedEngine.UI.Editor
{
    public sealed class TextFontReplacementWindow : EditorWindow
    {
        [SerializeField]
        [Tooltip("Hierarchy roots to scan. Project prefab assets must first be opened in Prefab Mode.")]
        private GameObject[] roots = Array.Empty<GameObject>();
        [SerializeField]
        [Tooltip("Independent replacement and formatting rules for each discovered source font.")]
        private List<TextFontRule> rules = new List<TextFontRule>();
        [SerializeField]
        [Tooltip("Include text on inactive descendants.")]
        private bool includeInactive = true;
        [SerializeField]
        [Tooltip("Keep the current roots while selecting other objects in the editor.")]
        private bool lockSelection;
        private readonly List<TMP_Text> texts = new List<TMP_Text>();
        private Vector2 scroll;
        private bool needsScan = true;
        private bool showPreview = true;
        private string status;

        [MenuItem("Tools/UnscriptedEngine/UI/Font Replacement")]
        public static void Open() => GetWindow<TextFontReplacementWindow>("UI Font Replacement");

        private void OnEnable()
        {
            minSize = new Vector2(460f, 420f);
            Undo.undoRedoPerformed -= HandleUndo;
            Undo.undoRedoPerformed += HandleUndo;
            if (!lockSelection) roots = Selection.gameObjects;
            needsScan = true;
        }

        private void OnDisable() => Undo.undoRedoPerformed -= HandleUndo;
        private void HandleUndo() { needsScan = true; Repaint(); }
        private void OnHierarchyChange() { needsScan = true; Repaint(); }
        private void OnFocus() { needsScan = true; Repaint(); }
        private void OnSelectionChange()
        {
            if (lockSelection) return;
            roots = Selection.gameObjects;
            status = null;
            needsScan = true;
            Repaint();
        }

        private void Scan()
        {
            texts.Clear();
            texts.AddRange(TextFontReplacement.Collect(roots, includeInactive));
            List<TextFontRule> nextRules = new List<TextFontRule>();
            foreach (TMP_Text text in texts)
            {
                if (TextFontReplacement.FindRule(nextRules, text.font) != null) continue;
                TextFontRule rule = TextFontReplacement.FindRule(rules, text.font);
                if (rule == null)
                {
                    rule = new TextFontRule(text.font);
                    foreach (TextFloatOverride number in rule.Formatting.Numbers) number.Value = number.Read(text);
                }
                nextRules.Add(rule);
            }
            rules = nextRules;
            needsScan = false;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Font replacement and text formatting", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Select one or more hierarchy roots in a scene or Prefab Mode. Each source font has its own replacement and optional overrides. Nothing changes until Apply.", MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                lockSelection = EditorGUILayout.ToggleLeft("Lock selection", lockSelection);
                EditorGUI.BeginChangeCheck();
                includeInactive = EditorGUILayout.ToggleLeft("Include inactive", includeInactive);
                if (EditorGUI.EndChangeCheck()) needsScan = true;
                if (GUILayout.Button("Rescan", GUILayout.Width(75f))) needsScan = true;
            }
            if (needsScan) Scan();
            EditorGUILayout.LabelField(roots.Length + " selected root(s), " + texts.Count + " TMP text(s), " + rules.Count + " source font(s)");
            if (texts.Count == 0)
            {
                EditorGUILayout.HelpBox("No editable TextMeshPro text found in the current stage. Open a prefab in Prefab Mode instead of selecting its Project asset. Both UI and world-space TMP components are supported.", MessageType.Warning);
                return;
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            using SerializedObject serializedWindow = new SerializedObject(this);
            serializedWindow.Update();
            SerializedProperty serializedRules = serializedWindow.FindProperty("rules");
            for (int index = 0; index < rules.Count; index++) DrawRule(serializedRules.GetArrayElementAtIndex(index), rules[index]);
            serializedWindow.ApplyModifiedPropertiesWithoutUndo();
            EditorGUILayout.HelpBox("Unchecked settings preserve every text's current values. Empty replacement keeps its font. Font swaps use the new font's default material; existing custom material presets are not carried across atlases. Rich-text tags can override these component settings.", MessageType.None);

            int affected = CountChanges(out string error);
            showPreview = EditorGUILayout.Foldout(showPreview, "Review changes (" + affected + " text component(s))", true);
            if (showPreview) DrawPreview();
            if (error != null) EditorGUILayout.HelpBox(error, MessageType.Error);
            EditorGUILayout.EndScrollView();
            bool playing = EditorApplication.isPlayingOrWillChangePlaymode;
            if (playing) EditorGUILayout.HelpBox("Exit Play Mode before applying changes.", MessageType.Warning);
            using (new EditorGUI.DisabledScope(playing || affected == 0 || error != null))
            {
                if (GUILayout.Button("Apply to " + affected + " text component(s)", GUILayout.Height(30f)))
                {
                    // Recollect before mutation so additions, removals and stage changes cannot use a stale plan.
                    Scan();
                    try
                    {
                        int applied = TextFontReplacement.Apply(texts, rules);
                        status = "Updated " + applied + " text component(s). Undo restores the whole operation. Save the scene or prefab normally.";
                        SceneView.RepaintAll();
                    }
                    catch (Exception exception) { status = exception.Message; }
                }
            }
            if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, MessageType.Info);
        }

        private void DrawRule(SerializedProperty property, TextFontRule rule)
        {
            int count = 0;
            foreach (TMP_Text text in texts) if (text != null && text.font == rule.Source) count++;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                property.isExpanded = EditorGUILayout.Foldout(property.isExpanded,
                    (rule.Source != null ? rule.Source.name : "Missing font") + " — " + count + " text(s)", true);
                if (!property.isExpanded) return;
                using (new EditorGUI.DisabledScope(true)) EditorGUILayout.PropertyField(property.FindPropertyRelative("source"));
                EditorGUILayout.PropertyField(property.FindPropertyRelative("replacement"), new GUIContent("Replace with"));
                SerializedProperty formatting = property.FindPropertyRelative("formatting");
                SerializedProperty numbers = formatting.FindPropertyRelative("numbers");
                EditorGUILayout.LabelField("Enable only settings to replace", EditorStyles.miniBoldLabel);
                for (int index = 0; index < numbers.arraySize; index++)
                {
                    SerializedProperty number = numbers.GetArrayElementAtIndex(index);
                    TextFloatProperty kind = (TextFloatProperty)number.FindPropertyRelative("property").enumValueIndex;
                    DrawOverride(number.FindPropertyRelative("enabled"), number.FindPropertyRelative("value"),
                        ObjectNames.NicifyVariableName(kind.ToString()));
                }
                DrawOverride(formatting.FindPropertyRelative("overrideAutoSize"), formatting.FindPropertyRelative("autoSize"), "Auto size");
                DrawOverride(formatting.FindPropertyRelative("overrideStyle"), formatting.FindPropertyRelative("style"), "Font style");
                DrawOverride(formatting.FindPropertyRelative("overrideAlignment"), formatting.FindPropertyRelative("alignment"), "Alignment");
                DrawOverride(formatting.FindPropertyRelative("overrideWrapping"), formatting.FindPropertyRelative("wrapping"), "Wrapping");
                DrawOverride(formatting.FindPropertyRelative("overrideColor"), formatting.FindPropertyRelative("color"), "Color");
                DrawOverride(formatting.FindPropertyRelative("overrideMargins"), formatting.FindPropertyRelative("margins"), "Margins (L/T/R/B)");
                EditorGUILayout.LabelField("Spacing uses TMP Inspector units. Size changes may be overridden by auto sizing.", EditorStyles.wordWrappedMiniLabel);
            }
        }

        private static void DrawOverride(SerializedProperty enabled, SerializedProperty value, string label)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                enabled.boolValue = EditorGUILayout.Toggle(enabled.boolValue, GUILayout.Width(18f));
                using (new EditorGUI.DisabledScope(!enabled.boolValue))
                    EditorGUILayout.PropertyField(value, new GUIContent(label, value.tooltip), true);
            }
        }

        private int CountChanges(out string error)
        {
            int count = 0;
            error = null;
            foreach (TMP_Text text in texts)
            {
                if (text == null || !TextFontReplacement.IsEditable(text.gameObject)) continue;
                TextFontRule rule = TextFontReplacement.FindRule(rules, text.font);
                if (rule == null || !rule.HasChanges(text)) continue;
                count++;
                string validation = rule.Validate(text);
                if (error == null && validation != null) error = validation;
            }
            return count;
        }

        private void DrawPreview()
        {
            int shown = 0;
            foreach (TMP_Text text in texts)
            {
                if (text == null || !TextFontReplacement.IsEditable(text.gameObject)) continue;
                TextFontRule rule = TextFontReplacement.FindRule(rules, text.font);
                if (rule == null || !rule.HasChanges(text)) continue;
                if (shown++ == 100)
                {
                    EditorGUILayout.LabelField("Review list limited to 100 entries. Apply includes every matching text.");
                    break;
                }
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUI.DisabledScope(true)) EditorGUILayout.ObjectField(text, typeof(TMP_Text), true);
                    EditorGUILayout.LabelField("Font: " + (text.font != null ? text.font.name : "Missing") + "  →  " +
                        (rule.Replacement != null ? rule.Replacement.name : "Keep current"));
                    string changes = rule.Formatting.DescribeChanges(text);
                    if (!string.IsNullOrEmpty(changes)) EditorGUILayout.LabelField(changes, EditorStyles.wordWrappedMiniLabel);
                    string sample = (text.text ?? string.Empty).Replace('\n', ' ').Replace('\r', ' ');
                    if (sample.Length > 120) sample = sample.Substring(0, 120) + "…";
                    EditorGUILayout.LabelField(sample, EditorStyles.wordWrappedMiniLabel);
                }
            }
        }
    }
}
