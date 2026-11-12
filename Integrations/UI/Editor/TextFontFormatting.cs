using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace UnscriptedEngine.UI.Editor
{
    public enum TextFloatProperty { CharacterSpacing, WordSpacing, LineSpacing, ParagraphSpacing, FontSize, MinimumFontSize, MaximumFontSize }

    [Serializable]
    public sealed class TextFloatOverride
    {
        [SerializeField]
        [Tooltip("Apply this value to texts using this rule's source font.")]
        private bool enabled;
        [SerializeField]
        [Tooltip("TextMeshPro property to adjust.")]
        private TextFloatProperty property;
        [SerializeField]
        [Tooltip("Replacement value, using the same units as the TextMeshPro Inspector.")]
        private float value;

        public bool Enabled { get => enabled; set => enabled = value; }
        public TextFloatProperty Property => property;
        public float Value { get => value; set => this.value = value; }

        public TextFloatOverride(TextFloatProperty property, float value = 0f)
        { this.property = property; this.value = value; }

        internal float Read(TMP_Text text)
        {
            switch (property)
            {
                case TextFloatProperty.CharacterSpacing: return text.characterSpacing;
                case TextFloatProperty.WordSpacing: return text.wordSpacing;
                case TextFloatProperty.LineSpacing: return text.lineSpacing;
                case TextFloatProperty.ParagraphSpacing: return text.paragraphSpacing;
                case TextFloatProperty.FontSize: return text.fontSize;
                case TextFloatProperty.MinimumFontSize: return text.fontSizeMin;
                case TextFloatProperty.MaximumFontSize: return text.fontSizeMax;
                default: throw new ArgumentOutOfRangeException();
            }
        }

        internal void Apply(TMP_Text text)
        {
            if (!enabled) return;
            switch (property)
            {
                case TextFloatProperty.CharacterSpacing: text.characterSpacing = value; break;
                case TextFloatProperty.WordSpacing: text.wordSpacing = value; break;
                case TextFloatProperty.LineSpacing: text.lineSpacing = value; break;
                case TextFloatProperty.ParagraphSpacing: text.paragraphSpacing = value; break;
                case TextFloatProperty.FontSize: text.fontSize = value; break;
                case TextFloatProperty.MinimumFontSize: text.fontSizeMin = value; break;
                case TextFloatProperty.MaximumFontSize: text.fontSizeMax = value; break;
            }
        }
    }

    /// <summary>Unchecked properties retain each individual text's original values.</summary>
    [Serializable]
    public sealed class TextFontFormatting
    {
        [SerializeField]
        [Tooltip("Optional spacing and size overrides for this font group.")]
        private List<TextFloatOverride> numbers = new List<TextFloatOverride>
        {
            new TextFloatOverride(TextFloatProperty.CharacterSpacing),
            new TextFloatOverride(TextFloatProperty.WordSpacing),
            new TextFloatOverride(TextFloatProperty.LineSpacing),
            new TextFloatOverride(TextFloatProperty.ParagraphSpacing),
            new TextFloatOverride(TextFloatProperty.FontSize, 36f),
            new TextFloatOverride(TextFloatProperty.MinimumFontSize, 18f),
            new TextFloatOverride(TextFloatProperty.MaximumFontSize, 72f)
        };
        [SerializeField]
        [Tooltip("Override automatic font sizing.")]
        private bool overrideAutoSize = false;
        [SerializeField]
        [Tooltip("Whether matching texts automatically size to their rectangles.")]
        private bool autoSize = false;
        [SerializeField]
        [Tooltip("Override the font style flags.")]
        private bool overrideStyle = false;
        [SerializeField]
        [Tooltip("Replacement font style flags, including bold and italic.")]
        private FontStyles style = FontStyles.Normal;
        [SerializeField]
        [Tooltip("Override text alignment.")]
        private bool overrideAlignment = false;
        [SerializeField]
        [Tooltip("Replacement horizontal and vertical alignment.")]
        private TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft;
        [SerializeField]
        [Tooltip("Override text wrapping behavior.")]
        private bool overrideWrapping = false;
        [SerializeField]
        [Tooltip("Replacement TextMeshPro wrapping behavior.")]
        private TextWrappingModes wrapping = TextWrappingModes.Normal;
        [SerializeField]
        [Tooltip("Override text vertex color.")]
        private bool overrideColor = false;
        [SerializeField]
        [Tooltip("Replacement text vertex color. Material and gradient settings remain independent.")]
        private Color color = Color.white;
        [SerializeField]
        [Tooltip("Override text margins.")]
        private bool overrideMargins = false;
        [SerializeField]
        [Tooltip("Text margins in left, top, right, bottom order, matching the TMP Inspector.")]
        private Vector4 margins = Vector4.zero;

        public IReadOnlyList<TextFloatOverride> Numbers => numbers;

        internal string Validate(TMP_Text text)
        {
            float minimum = text.fontSizeMin;
            float maximum = text.fontSizeMax;
            bool validateSizeRange = overrideAutoSize && autoSize;
            foreach (TextFloatOverride number in numbers)
            {
                if (!number.Enabled) continue;
                float value = number.Value;
                if (float.IsNaN(value) || float.IsInfinity(value)) return "Values must be finite numbers.";
                if (number.Property >= TextFloatProperty.FontSize && value <= 0f) return "Font sizes must be greater than zero.";
                if (number.Property == TextFloatProperty.MinimumFontSize) { minimum = value; validateSizeRange = true; }
                if (number.Property == TextFloatProperty.MaximumFontSize) { maximum = value; validateSizeRange = true; }
            }
            if (validateSizeRange && minimum > maximum) return "Minimum font size exceeds maximum on '" + text.name + "'.";
            if (overrideMargins && (!IsFinite(margins.x) || !IsFinite(margins.y) || !IsFinite(margins.z) || !IsFinite(margins.w)))
                return "Margins must be finite numbers.";
            return null;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        internal bool HasChanges(TMP_Text text)
        {
            foreach (TextFloatOverride number in numbers)
                if (number.Enabled && number.Read(text) != number.Value) return true;
            return overrideAutoSize && text.enableAutoSizing != autoSize ||
                overrideStyle && text.fontStyle != style || overrideAlignment && text.alignment != alignment ||
                overrideWrapping && text.textWrappingMode != wrapping || overrideColor && text.color != color ||
                overrideMargins && text.margin != margins;
        }

        internal void Apply(TMP_Text text)
        {
            foreach (TextFloatOverride number in numbers) number.Apply(text);
            if (overrideAutoSize) text.enableAutoSizing = autoSize;
            if (overrideStyle) text.fontStyle = style;
            if (overrideAlignment) text.alignment = alignment;
            if (overrideWrapping) text.textWrappingMode = wrapping;
            if (overrideColor) text.color = color;
            if (overrideMargins) text.margin = margins;
        }

        internal string DescribeChanges(TMP_Text text)
        {
            StringBuilder summary = new StringBuilder();
            foreach (TextFloatOverride number in numbers)
                if (number.Enabled) AddChange(summary, number.Property.ToString(), number.Read(text), number.Value);
            if (overrideAutoSize) AddChange(summary, "Auto size", text.enableAutoSizing, autoSize);
            if (overrideStyle) AddChange(summary, "Style", text.fontStyle, style);
            if (overrideAlignment) AddChange(summary, "Alignment", text.alignment, alignment);
            if (overrideWrapping) AddChange(summary, "Wrapping", text.textWrappingMode, wrapping);
            if (overrideColor) AddChange(summary, "Color", text.color, color);
            if (overrideMargins) AddChange(summary, "Margins", text.margin, margins);
            return summary.ToString();
        }

        private static void AddChange<T>(StringBuilder summary, string label, T before, T after)
        {
            if (EqualityComparer<T>.Default.Equals(before, after)) return;
            if (summary.Length > 0) summary.Append('\n');
            summary.Append(label).Append(": ").Append(before).Append(" → ").Append(after);
        }
    }
}
