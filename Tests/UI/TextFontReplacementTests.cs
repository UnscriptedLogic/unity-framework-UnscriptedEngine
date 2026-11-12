using System;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnscriptedEngine.UI.Editor;
using Object = UnityEngine.Object;

namespace UnscriptedEngine.Tests
{
    public sealed class TextFontReplacementTests
    {
        private GameObject root;
        private readonly List<TMP_FontAsset> fonts = new List<TMP_FontAsset>();
        private TMP_FontAsset original;

        [SetUp]
        public void SetUp()
        {
            original = TMP_Settings.defaultFontAsset;
            Assert.IsNotNull(original, "Import TMP Essentials and configure a default font before running UI tests.");
            root = new GameObject("Font replacement test", typeof(RectTransform));
            root.SetActive(false);
            for (int index = 0; index < 3; index++)
            {
                TMP_FontAsset font = Object.Instantiate(original);
                font.name = "Test font " + index;
                font.material = new Material(original.material);
                fonts.Add(font);
            }
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            foreach (TMP_FontAsset font in fonts)
            {
                Object.DestroyImmediate(font.material);
                Object.DestroyImmediate(font);
            }
            fonts.Clear();
        }

        private TextMeshProUGUI CreateText(TMP_FontAsset font)
        {
            GameObject child = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            child.transform.SetParent(root.transform, false);
            TextMeshProUGUI text = child.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = 30f;
            text.text = "Example text";
            return text;
        }

        private static void Override(TextFontRule rule, TextFloatProperty property, float value)
        {
            foreach (TextFloatOverride number in rule.Formatting.Numbers)
            {
                if (number.Property != property) continue;
                number.Enabled = true;
                number.Value = value;
                return;
            }
            Assert.Fail("Unknown numeric setting.");
        }

        [Test]
        public void CollectionDeduplicatesOverlappingRootsAndHonorsInactiveFilter()
        {
            TextMeshProUGUI text = CreateText(fonts[0]);
            Assert.AreEqual(1, TextFontReplacement.Collect(new[] { root, text.gameObject }, true).Count);
            Assert.IsEmpty(TextFontReplacement.Collect(new[] { root }, false));
            root.SetActive(true);
            Assert.AreEqual(1, TextFontReplacement.Collect(new[] { root }, false).Count);
        }

        [Test]
        public void FontRulesAreIndependentAndNeverCascade()
        {
            TextMeshProUGUI first = CreateText(fonts[0]);
            TextMeshProUGUI second = CreateText(fonts[1]);
            TextMeshProUGUI unchanged = CreateText(fonts[2]);
            TextFontRule firstRule = new TextFontRule(fonts[0]) { Replacement = fonts[1] };
            TextFontRule secondRule = new TextFontRule(fonts[1]) { Replacement = fonts[2] };
            Assert.AreEqual(2, TextFontReplacement.Apply(new TMP_Text[] { first, second, unchanged }, new[] { firstRule, secondRule }));
            Assert.AreSame(fonts[1], first.font);
            Assert.AreSame(fonts[2], second.font);
            Assert.AreSame(fonts[2], unchanged.font);
            Assert.AreSame(fonts[1].material, first.fontSharedMaterial);
        }

        [Test]
        public void FormattingOnlyAffectsMatchingFontAndEnabledProperties()
        {
            TextMeshProUGUI first = CreateText(fonts[0]);
            TextMeshProUGUI second = CreateText(fonts[1]);
            first.wordSpacing = 3f;
            first.characterSpacing = 2f;
            TextFontRule rule = new TextFontRule(fonts[0]);
            Override(rule, TextFloatProperty.LineSpacing, 12f);
            Assert.AreEqual(1, TextFontReplacement.Apply(new TMP_Text[] { first, second }, new[] { rule }));
            Assert.AreEqual(12f, first.lineSpacing);
            Assert.AreEqual(3f, first.wordSpacing);
            Assert.AreEqual(2f, first.characterSpacing);
            Assert.AreEqual(30f, first.fontSize);
            Assert.AreSame(fonts[0], first.font);
            Assert.AreEqual(0f, second.lineSpacing);
            Assert.AreEqual(0, TextFontReplacement.Apply(new TMP_Text[] { first, second }, new[] { rule }));
        }

        [Test]
        public void InvalidRulePreventsEveryChangeBeforeMutation()
        {
            TextMeshProUGUI first = CreateText(fonts[0]);
            TextMeshProUGUI second = CreateText(fonts[1]);
            TextFontRule valid = new TextFontRule(fonts[0]) { Replacement = fonts[2] };
            TextFontRule invalid = new TextFontRule(fonts[1]);
            Override(invalid, TextFloatProperty.FontSize, float.NaN);
            Assert.Throws<InvalidOperationException>(() => TextFontReplacement.Apply(new TMP_Text[] { first, second }, new[] { valid, invalid }));
            Assert.AreSame(fonts[0], first.font);
            Assert.AreEqual(30f, second.fontSize);
        }

        [Test]
        public void OneUndoRestoresFontsAndSpacingForAllTexts()
        {
            TextMeshProUGUI first = CreateText(fonts[0]);
            TextMeshProUGUI second = CreateText(fonts[0]);
            TextFontRule rule = new TextFontRule(fonts[0]) { Replacement = fonts[1] };
            Override(rule, TextFloatProperty.WordSpacing, 7f);
            TextFontReplacement.Apply(new TMP_Text[] { first, second }, new[] { rule });
            Undo.FlushUndoRecordObjects();
            Undo.PerformUndo();
            Assert.AreSame(fonts[0], first.font);
            Assert.AreSame(fonts[0], second.font);
            Assert.AreEqual(0f, first.wordSpacing);
            Assert.AreEqual(0f, second.wordSpacing);
        }

        [Test]
        public void ProjectPrefabAssetsAreNotEditedDirectly()
        {
            string path = "Assets/__FontReplacementTest_" + Guid.NewGuid().ToString("N") + ".prefab";
            try
            {
                TextMeshProUGUI text = CreateText(original);
                GameObject asset = PrefabUtility.SaveAsPrefabAsset(root, path);
                Assert.IsNotNull(asset);
                Assert.IsEmpty(TextFontReplacement.Collect(new[] { asset }, true));
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
                instance.transform.SetParent(root.transform, false);
                TMP_Text instanceText = instance.GetComponentInChildren<TMP_Text>(true);
                TextFontRule rule = new TextFontRule(original);
                Override(rule, TextFloatProperty.LineSpacing, 9f);
                Assert.AreEqual(1, TextFontReplacement.Apply(new[] { instanceText }, new[] { rule }));
                Assert.IsTrue(PrefabUtility.HasPrefabInstanceAnyOverrides(instance, false));
                Assert.AreEqual(text.lineSpacing, asset.GetComponentInChildren<TMP_Text>(true).lineSpacing);
            }
            finally { AssetDatabase.DeleteAsset(path); }
        }
    }
}
