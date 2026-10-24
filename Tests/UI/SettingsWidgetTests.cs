using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnscriptedEngine.Settings;
using UnscriptedEngine.UI.Settings;
using Object = UnityEngine.Object;

namespace UnscriptedEngine.Tests
{
    [SettingsCatalogRoot]
    public static class SceneSettingsTestCatalog
    {
        public static class Shared
        {
            public static readonly Setting<bool> Enabled = Setting.Bool("test.scene.shared", true, "Shared", "General");
        }
        public static class First
        {
            public static readonly Setting<float> Number = Setting.Float("test.scene.number", .5f, 0f, 1f, "Number", "Prototype");
            public static readonly Setting<bool> Advanced = Setting.Bool(
                "test.scene.advanced", false, "Advanced flag", "Prototype", advanced: true);
            public static readonly Setting<bool> Shared = SceneSettingsTestCatalog.Shared.Enabled;
        }
        public static class Second
        {
            public static readonly Setting<bool> Racing = Setting.Bool("test.scene.racing", true, "Racing", "Racing");
            public static readonly Setting<bool> Shared = SceneSettingsTestCatalog.Shared.Enabled;
        }
    }

    public sealed class FirstSettingsTestProvider : SettingsProvider
    {
        protected override Type CatalogType => typeof(SceneSettingsTestCatalog.First);
    }

    public sealed class SecondSettingsTestProvider : SettingsProvider
    {
        protected override Type CatalogType => typeof(SceneSettingsTestCatalog.Second);
    }

    public sealed class SettingsWidgetTests
    {
        [UnitySetUp]
        public IEnumerator EnterRuntime()
        {
            yield return new EnterPlayMode();
            Assert.IsTrue(Application.isPlaying);
        }

        [UnityTearDown]
        public IEnumerator ExitRuntime() { yield return new ExitPlayMode(); }

        [UnityTest]
        public IEnumerator GeneratedPanelFiltersAdvancedSettingsAndRemovesDisabledProvider()
        {
            GameObject providerObject = new GameObject("Scene Settings Container");
            GameObject root = new GameObject("Panel", typeof(RectTransform));
            try
            {
                FirstSettingsTestProvider provider = providerObject.AddComponent<FirstSettingsTestProvider>();
                GeneratedSettingsPanel panel = root.AddComponent<GeneratedSettingsPanel>();
                yield return null;
                Assert.AreEqual(2, root.GetComponentsInChildren<SettingsWidget>().Length);
                Assert.IsFalse(root.GetComponentsInChildren<Toggle>().First().isOn);
                panel.SetShowAdvanced(true);
                yield return null;
                Assert.AreEqual(3, root.GetComponentsInChildren<SettingsWidget>().Length);
                panel.SetSearch("advanced flag");
                yield return null;
                Assert.AreEqual(1, root.GetComponentsInChildren<SettingsWidget>().Length);
                provider.enabled = false;
                yield return null;
                Assert.AreEqual(0, root.GetComponentsInChildren<SettingsWidget>().Length);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(providerObject);
            }
        }

        [UnityTest]
        public IEnumerator SceneUnloadRemovesOnlyItsCatalogAndKeepsSharedDefinitions()
        {
            Scene firstScene = SceneManager.CreateScene("SettingsPrototypeOne");
            Scene secondScene = SceneManager.CreateScene("SettingsPrototypeTwo");
            GameObject firstContainer = new GameObject("First Settings Container");
            GameObject secondContainer = new GameObject("Second Settings Container");
            SceneManager.MoveGameObjectToScene(firstContainer, firstScene);
            SceneManager.MoveGameObjectToScene(secondContainer, secondScene);
            SettingsService service = SettingsRuntime.Service;
            SettingsPreferences originalPreferences = service.ExportPreferences();
            try
            {
                firstContainer.AddComponent<FirstSettingsTestProvider>();
                secondContainer.AddComponent<SecondSettingsTestProvider>();
                Assert.IsTrue(service.TrySet(SceneSettingsTestCatalog.First.Number, .9f));
                yield return SceneManager.UnloadSceneAsync(firstScene);
                Assert.IsFalse(service.IsActive(SceneSettingsTestCatalog.First.Number));
                Assert.AreEqual(.5f, service.Get(SceneSettingsTestCatalog.First.Number));
                Assert.IsTrue(service.IsActive(SceneSettingsTestCatalog.Shared.Enabled));
                Assert.IsTrue(service.IsActive(SceneSettingsTestCatalog.Second.Racing));
                Assert.IsFalse(service.ActiveGroups.Any(group => group.Path == "Prototype"));
                yield return SceneManager.UnloadSceneAsync(secondScene);
                Assert.IsFalse(service.IsActive(SceneSettingsTestCatalog.Shared.Enabled));
                GameObject returningContainer = new GameObject("Returning Settings Container");
                try
                {
                    returningContainer.AddComponent<FirstSettingsTestProvider>();
                    Assert.AreEqual(.9f, service.Get(SceneSettingsTestCatalog.First.Number));
                }
                finally { Object.DestroyImmediate(returningContainer); }
            }
            finally
            {
                if (firstContainer != null) Object.DestroyImmediate(firstContainer);
                if (secondContainer != null) Object.DestroyImmediate(secondContainer);
                SettingsRuntime.ImportPreferences(originalPreferences);
                SettingsRuntime.Save();
            }
        }

        [UnityTest]
        public IEnumerator FloatWidgetEditsRefreshesAndResubscribesAfterCollapse()
        {
            Setting<float> definition = Setting.Float("ui.float", .5f, 0f, 1f, "Float", "UI");
            SettingsService service = new SettingsService();
            using IDisposable registration = service.Register(new SettingDefinition[] { definition });
            GameObject root = new GameObject("Widget", typeof(RectTransform));
            try
            {
                DefaultSettingsWidget widget = root.AddComponent<DefaultSettingsWidget>();
                widget.Bind(service, definition);
                Slider slider = root.GetComponentInChildren<Slider>();
                InputField input = root.GetComponentInChildren<InputField>();
                Assert.AreEqual(.5f, slider.value);
                slider.value = .75f;
                Assert.AreEqual(.75f, service.Get(definition));
                input.onEndEdit.Invoke("0.25");
                Assert.AreEqual(.25f, slider.value);
                input.onEndEdit.Invoke("not a number");
                Assert.AreEqual("0.25", input.text);
                root.SetActive(false);
                service.TrySet(definition, .9f);
                root.SetActive(true);
                Assert.AreEqual(.9f, slider.value);
                root.GetComponentInChildren<Button>().onClick.Invoke();
                Assert.AreEqual(.5f, slider.value);
                widget.Unbind();
                service.TrySet(definition, .1f);
                yield return null;
                Assert.AreEqual(0, root.GetComponentsInChildren<Slider>().Length);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [UnityTest]
        public IEnumerator ServerChoiceWidgetIsReadOnlyAndFollowsReplication()
        {
            Setting<string> definition = Setting.Choice("ui.choice", "easy", new string[] { "easy", "hard" },
                "Choice", "UI", scope: SettingScope.ServerSession);
            SettingsService service = new SettingsService(SettingsRole.Client);
            using IDisposable registration = service.Register(new SettingDefinition[] { definition });
            GameObject root = new GameObject("Widget", typeof(RectTransform));
            try
            {
                root.AddComponent<DefaultSettingsWidget>().Bind(service, definition);
                Dropdown dropdown = root.GetComponentInChildren<Dropdown>();
                Assert.IsFalse(dropdown.interactable);
                Assert.AreEqual(2, dropdown.options.Count);
                service.ApplyServerValue(definition, "hard");
                Assert.AreEqual(1, dropdown.value);
                dropdown.onValueChanged.Invoke(0);
                Assert.AreEqual("hard", service.Get(definition));
                Assert.AreEqual(1, dropdown.value);
                Assert.IsFalse(root.GetComponentInChildren<Button>().interactable);
                yield return null;
            }
            finally { Object.DestroyImmediate(root); }
        }

        [UnityTest]
        public IEnumerator RebindingDefaultWidgetDoesNotDuplicateControlsOrListeners()
        {
            Setting<bool> definition = Setting.Bool("ui.bool", false, "Boolean", "UI");
            SettingsService service = new SettingsService();
            using IDisposable registration = service.Register(new SettingDefinition[] { definition });
            GameObject root = new GameObject("Widget", typeof(RectTransform));
            try
            {
                DefaultSettingsWidget widget = root.AddComponent<DefaultSettingsWidget>();
                widget.Bind(service, definition);
                widget.Bind(service, definition);
                yield return null;
                Assert.AreEqual(1, root.GetComponentsInChildren<Toggle>().Length);
                root.GetComponentInChildren<Toggle>().isOn = true;
                Assert.IsTrue(service.Get(definition));
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
