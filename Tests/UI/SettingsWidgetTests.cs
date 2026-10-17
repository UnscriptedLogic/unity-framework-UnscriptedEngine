using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnscriptedEngine.Settings;
using UnscriptedEngine.UI.Settings;

namespace UnscriptedEngine.Tests
{
    public sealed class SettingsWidgetTests
    {
        [UnitySetUp]
        public IEnumerator EnterRuntime() { yield return new EnterPlayMode(); Assert.IsTrue(Application.isPlaying); }

        [UnityTearDown]
        public IEnumerator ExitRuntime() { yield return new ExitPlayMode(); }

        [UnityTest]
        public IEnumerator GeneratedPanelFiltersAdvancedSettingsAndRemovesDisabledModule()
        {
            var number = ScriptableObject.CreateInstance<FloatSettingDefinition>(); number.Configure("ui.panel.number", "Number");
            var advanced = ScriptableObject.CreateInstance<BoolSettingDefinition>(); advanced.Configure("ui.panel.advanced", "Advanced flag");
            var serialized = new UnityEditor.SerializedObject(advanced);
            serialized.FindProperty("advanced").boolValue = true; serialized.ApplyModifiedPropertiesWithoutUndo();
            var group = ScriptableObject.CreateInstance<SettingsGroup>(); group.Configure("Prototype", new SettingDefinition[] { number, advanced });
            var moduleObject = new GameObject("Module");
            var root = new GameObject("Panel", typeof(RectTransform));
            try
            {
                var module = moduleObject.AddComponent<SettingsModule>(); module.SetGroup(group);
                var panel = root.AddComponent<GeneratedSettingsPanel>();
                yield return null;
                Assert.AreEqual(1, root.GetComponentsInChildren<SettingsWidget>().Length);
                Assert.IsFalse(root.GetComponentInChildren<Toggle>().isOn, "Advanced visibility must start unchecked.");
                panel.SetShowAdvanced(true); yield return null;
                Assert.AreEqual(2, root.GetComponentsInChildren<SettingsWidget>().Length);
                panel.SetSearch("advanced flag"); yield return null;
                Assert.AreEqual(1, root.GetComponentsInChildren<SettingsWidget>().Length);
                module.enabled = false; yield return null;
                Assert.AreEqual(0, root.GetComponentsInChildren<SettingsWidget>().Length);
            }
            finally
            {
                Object.DestroyImmediate(root); Object.DestroyImmediate(moduleObject);
                Object.DestroyImmediate(group); Object.DestroyImmediate(number); Object.DestroyImmediate(advanced);
            }
        }

        [UnityTest]
        public IEnumerator FloatWidgetEditsRefreshesAndResubscribesAfterCollapse()
        {
            var definition = ScriptableObject.CreateInstance<FloatSettingDefinition>();
            definition.Configure("ui.float", "Float"); definition.SetDefault(.5f);
            var group = ScriptableObject.CreateInstance<SettingsGroup>(); group.Configure("UI", new[] { definition });
            var service = new SettingsService(); using var registration = service.Register(group);
            var root = new GameObject("Widget", typeof(RectTransform));
            try
            {
                var widget = root.AddComponent<DefaultSettingsWidget>(); widget.Bind(service, definition);
                var slider = root.GetComponentInChildren<Slider>(); var input = root.GetComponentInChildren<InputField>();
                Assert.AreEqual(.5f, slider.value);
                slider.value = .75f; Assert.AreEqual(.75f, service.Get(definition));
                input.onEndEdit.Invoke("0.25"); Assert.AreEqual(.25f, slider.value);
                input.onEndEdit.Invoke("not a number"); Assert.AreEqual("0.25", input.text);
                root.SetActive(false); service.TrySet(definition, .9f); root.SetActive(true);
                Assert.AreEqual(.9f, slider.value);
                root.GetComponentInChildren<Button>().onClick.Invoke(); Assert.AreEqual(.5f, slider.value);
                widget.Unbind(); service.TrySet(definition, .1f);
                yield return null;
                Assert.AreEqual(0, root.GetComponentsInChildren<Slider>().Length);
            }
            finally
            {
                Object.DestroyImmediate(root); Object.DestroyImmediate(group); Object.DestroyImmediate(definition);
            }
        }

        [UnityTest]
        public IEnumerator ServerChoiceWidgetIsReadOnlyAndFollowsReplication()
        {
            var definition = ScriptableObject.CreateInstance<StringSettingDefinition>();
            definition.Configure("ui.choice", "Choice", SettingScope.ServerSession);
            definition.SetChoices("easy", "hard"); definition.SetDefault("easy");
            var group = ScriptableObject.CreateInstance<SettingsGroup>(); group.Configure("UI", new[] { definition });
            var service = new SettingsService(SettingsRole.Client); using var registration = service.Register(group);
            var root = new GameObject("Widget", typeof(RectTransform));
            try
            {
                root.AddComponent<DefaultSettingsWidget>().Bind(service, definition);
                var dropdown = root.GetComponentInChildren<Dropdown>();
                Assert.IsFalse(dropdown.interactable);
                Assert.AreEqual(2, dropdown.options.Count);
                service.ApplyServerValue(definition, "hard"); Assert.AreEqual(1, dropdown.value);
                dropdown.onValueChanged.Invoke(0); Assert.AreEqual("hard", service.Get(definition));
                Assert.AreEqual(1, dropdown.value);
                Assert.IsFalse(root.GetComponentInChildren<Button>().interactable);
                yield return null;
            }
            finally
            {
                Object.DestroyImmediate(root); Object.DestroyImmediate(group); Object.DestroyImmediate(definition);
            }
        }

        [UnityTest]
        public IEnumerator RebindingDefaultWidgetDoesNotDuplicateControlsOrListeners()
        {
            var definition = ScriptableObject.CreateInstance<BoolSettingDefinition>(); definition.Configure("ui.bool", "Boolean");
            var group = ScriptableObject.CreateInstance<SettingsGroup>(); group.Configure("UI", new[] { definition });
            var service = new SettingsService(); using var registration = service.Register(group);
            var root = new GameObject("Widget", typeof(RectTransform));
            try
            {
                var widget = root.AddComponent<DefaultSettingsWidget>(); widget.Bind(service, definition); widget.Bind(service, definition);
                yield return null;
                Assert.AreEqual(1, root.GetComponentsInChildren<Toggle>().Length);
                root.GetComponentInChildren<Toggle>().isOn = true; Assert.IsTrue(service.Get(definition));
            }
            finally
            {
                Object.DestroyImmediate(root); Object.DestroyImmediate(group); Object.DestroyImmediate(definition);
            }
        }
    }
}
