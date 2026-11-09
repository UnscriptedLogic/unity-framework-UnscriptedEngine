using Framework.GameplayTag;
using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnscriptedEngine.UI;

namespace UnscriptedEngine.Tests
{
    public sealed class TaggedFloatBindingTests
    {
        private GameObject actor;
        private GameObject ui;
        private GameplayTag health;
        private TaggedFloatValue value;
        private TaggedFloatBinding binding;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            yield return new EnterPlayMode();
            health = ScriptableObject.CreateInstance<GameplayTag>();
            JsonUtility.FromJsonOverwrite("{\"path\":\"Stats.Health\"}", health);
            actor = new GameObject("Actor");
            GameObject child = new GameObject("Inactive stats");
            child.transform.SetParent(actor.transform);
            value = child.AddComponent<TaggedFloatValue>();
            value.Tags.Add(health);
            child.SetActive(false);
            ui = new GameObject("UI");
            binding = ui.AddComponent<TaggedFloatBinding>();
            binding.ValueTag = health;
            binding.SetSource(actor);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Object.DestroyImmediate(ui);
            Object.DestroyImmediate(actor);
            Object.DestroyImmediate(health);
            yield return new ExitPlayMode();
        }

        [Test]
        public void FindsInactiveChildAndPublishesNormalizedAndInverseResponse()
        {
            Assert.That(binding.Source, Is.SameAs(value));
            float normalized = -1f;
            float response = -1f;
            binding.OnNormalizedChanged.AddListener(next => normalized = next);
            binding.OnResponseChanged.AddListener(next => response = next);
            value.SetValue(25f);
            Assert.That(binding.Value, Is.EqualTo(25f));
            Assert.That(normalized, Is.EqualTo(0.25f).Within(0.001f));
            Assert.That(response, Is.EqualTo(0.75f).Within(0.001f));
        }

        [Test]
        public void RebindingAndDisablingReleaseOldListener()
        {
            int notifications = 0;
            binding.Changed += () => notifications++;
            binding.SetSource(ui);
            int afterRebind = notifications;
            value.SetValue(50f);
            Assert.That(notifications, Is.EqualTo(afterRebind));
            Assert.That(binding.IsBound, Is.False);
            Assert.That(binding.ResponseValue, Is.Zero);
            binding.SetSource(actor);
            binding.enabled = false;
            int afterDisable = notifications;
            value.SetValue(10f);
            Assert.That(notifications, Is.EqualTo(afterDisable));
        }

        [Test]
        public void DuplicateTagsClearBindingInsteadOfPickingWrongValue()
        {
            TaggedFloatValue duplicate = actor.AddComponent<TaggedFloatValue>();
            duplicate.Tags.Add(health);
            LogAssert.Expect(LogType.Warning, "Multiple tagged values match this UI binding. Binding cleared; use a more specific source or tag.");
            binding.Rebind();
            Assert.That(binding.IsBound, Is.False);
        }

        [Test]
        public void ReactiveThresholdSupportsBothLimitsAndEndpointPercentages()
        {
            GameObject visual = new GameObject("Visual", typeof(RectTransform));
            visual.transform.SetParent(ui.transform);
            UIAnimReactive_Shake shake = visual.AddComponent<UIAnimReactive_Shake>();
            value.SetValue(25f);
            Assert.That(shake.ThresholdReached, Is.True);
            Assert.That(shake.Strength, Is.Zero);
            value.SetValue(12.5f);
            Assert.That(shake.Strength, Is.EqualTo(0.5f).Within(0.001f));
            SerializedObject settings = new SerializedObject(shake);
            settings.FindProperty("limit").enumValueIndex = (int)UIReactiveLimit.Maximum;
            settings.FindProperty("percentage").floatValue = 75f;
            settings.ApplyModifiedPropertiesWithoutUndo();
            value.SetValue(87.5f);
            Assert.That(shake.Strength, Is.EqualTo(0.5f).Within(0.001f));
            settings.Update();
            settings.FindProperty("percentage").floatValue = 100f;
            settings.ApplyModifiedPropertiesWithoutUndo();
            value.SetValue(100f);
            Assert.That(shake.Strength, Is.EqualTo(1f));
            binding.enabled = false;
            Assert.That(shake.ThresholdReached, Is.False);
            Assert.That(shake.Strength, Is.Zero);
        }

        [Test]
        public void HideClearsRaycastsAndRestoresCanvasStateOnDisable()
        {
            GameObject visual = new GameObject("Visual", typeof(RectTransform), typeof(CanvasGroup));
            visual.transform.SetParent(ui.transform);
            CanvasGroup group = visual.GetComponent<CanvasGroup>();
            group.alpha = 0.8f;
            UIAnimReactive_Hide hide = visual.AddComponent<UIAnimReactive_Hide>();
            SerializedObject settings = new SerializedObject(hide);
            settings.FindProperty("fadeDuration").floatValue = 0f;
            settings.ApplyModifiedPropertiesWithoutUndo();
            value.SetValue(10f);
            hide.SendMessage("Update");
            Assert.That(group.alpha, Is.Zero);
            Assert.That(group.blocksRaycasts, Is.False);
            value.SetValue(50f);
            hide.SendMessage("Update");
            Assert.That(group.alpha, Is.EqualTo(0.8f));
            value.SetValue(10f);
            hide.SendMessage("Update");
            hide.enabled = false;
            Assert.That(group.alpha, Is.EqualTo(0.8f));
            Assert.That(group.interactable && group.blocksRaycasts, Is.True);
        }

        [Test]
        public void ShakeRestoresOriginalPositionOnDisable()
        {
            GameObject visual = new GameObject("Visual", typeof(RectTransform));
            visual.transform.SetParent(ui.transform);
            RectTransform rect = (RectTransform)visual.transform;
            Vector3 original = new Vector3(13f, 27f, 0f);
            rect.anchoredPosition3D = original;
            UIAnimReactive_Shake shake = visual.AddComponent<UIAnimReactive_Shake>();
            value.SetValue(0f);
            shake.SendMessage("LateUpdate");
            shake.SendMessage("LateUpdate");
            shake.enabled = false;
            Assert.That(Vector3.Distance(rect.anchoredPosition3D, original), Is.LessThan(0.001f));
        }
    }
}
