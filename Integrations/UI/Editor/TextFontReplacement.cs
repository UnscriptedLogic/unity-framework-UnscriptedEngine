using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UnscriptedEngine.UI.Editor
{
    [Serializable]
    public sealed class TextFontRule
    {
        [SerializeField]
        [Tooltip("Only texts originally using this exact font asset match this rule.")]
        private TMP_FontAsset source;
        [SerializeField]
        [Tooltip("Replacement font. Empty keeps the current font and applies only enabled formatting overrides.")]
        private TMP_FontAsset replacement;
        [SerializeField]
        [Tooltip("Optional text settings applied only to this source-font group.")]
        private TextFontFormatting formatting = new TextFontFormatting();

        public TMP_FontAsset Source => source;
        public TMP_FontAsset Replacement { get => replacement; set => replacement = value; }
        public TextFontFormatting Formatting => formatting;
        public TextFontRule(TMP_FontAsset source) => this.source = source;
        internal bool HasChanges(TMP_Text text) => replacement != null && text.font != replacement || formatting.HasChanges(text);
        internal string Validate(TMP_Text text) => replacement != null && replacement != text.font && replacement.material == null
            ? "Replacement font '" + replacement.name + "' has no material." : formatting.Validate(text);
    }

    /// <summary>Stage-scoped collection and one-pass replacement. No font-asset or prefab-asset mutation.</summary>
    public static class TextFontReplacement
    {
        public static List<TMP_Text> Collect(IEnumerable<GameObject> roots, bool includeInactive)
        {
            List<TMP_Text> result = new List<TMP_Text>();
            HashSet<TMP_Text> visited = new HashSet<TMP_Text>();
            List<TMP_Text> children = new List<TMP_Text>();
            foreach (GameObject root in roots)
            {
                if (!IsEditable(root)) continue;
                root.GetComponentsInChildren(includeInactive, children);
                foreach (TMP_Text text in children)
                    if ((includeInactive || text.gameObject.activeInHierarchy) && visited.Add(text)) result.Add(text);
            }
            return result;
        }

        internal static bool IsEditable(GameObject root) => root != null && !EditorUtility.IsPersistent(root) &&
            root.scene.IsValid() && root.scene.isLoaded &&
            StageUtility.GetStageHandle(root).Equals(StageUtility.GetCurrentStageHandle()) &&
            (root.hideFlags & HideFlags.NotEditable) == 0;

        public static TextFontRule FindRule(IReadOnlyList<TextFontRule> rules, TMP_FontAsset source)
        {
            foreach (TextFontRule rule in rules) if (rule.Source == source) return rule;
            return null;
        }

        public static int Apply(IReadOnlyList<TMP_Text> texts, IReadOnlyList<TextFontRule> rules)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before editing text.");
            List<TMP_Text> changed = new List<TMP_Text>();
            List<TextFontRule> matched = new List<TextFontRule>();
            HashSet<TMP_Text> visited = new HashSet<TMP_Text>();
            // Resolve against original fonts before editing, so A -> B and B -> C never cascade.
            foreach (TMP_Text text in texts)
            {
                if (text == null || !IsEditable(text.gameObject) || !visited.Add(text)) continue;
                TextFontRule rule = FindRule(rules, text.font);
                if (rule == null || !rule.HasChanges(text)) continue;
                string error = rule.Validate(text);
                if (error != null) throw new InvalidOperationException(error);
                changed.Add(text);
                matched.Add(rule);
            }
            if (changed.Count == 0) return 0;
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Replace UI Fonts and Formatting");
            Undo.RecordObjects(changed.ToArray(), "Replace UI Fonts and Formatting");
            for (int index = 0; index < changed.Count; index++)
            {
                TMP_Text text = changed[index];
                TextFontRule rule = matched[index];
                if (rule.Replacement != null && rule.Replacement != text.font)
                {
                    text.font = rule.Replacement;
                    // A material preset for the old atlas is incompatible with the replacement font.
                    text.fontSharedMaterial = rule.Replacement.material;
                }
                rule.Formatting.Apply(text);
                EditorUtility.SetDirty(text);
                if (PrefabUtility.IsPartOfPrefabInstance(text)) PrefabUtility.RecordPrefabInstancePropertyModifications(text);
                EditorSceneManager.MarkSceneDirty(text.gameObject.scene);
            }
            Undo.CollapseUndoOperations(group);
            return changed.Count;
        }
    }
}
