using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnscriptedEngine.Settings;

namespace UnscriptedEngine.UI.Settings
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class GeneratedSettingsPanel : MonoBehaviour
    {
        [SerializeField, Tooltip("Optional widget-prefab mappings for individual settings, editor hints, or value types. Leave empty to use the built-in toggle, slider, input, and dropdown controls.")]
        private SettingsTheme theme;
        [SerializeField, Tooltip("Optional parent for generated groups in your own layout. Supply a layout group and scroll content sizing, and wire custom search/advanced controls yourself. Leave empty to create the complete template panel.")]
        private RectTransform content;
        private SettingsService service;
        private string search = "";
        private bool showAdvanced;
        private bool dirty;
        private readonly Dictionary<SettingsGroup, bool> collapsed = new();
        private readonly List<GameObject> generated = new();
        public SettingsTheme Theme { get => theme; set { theme = value; dirty = true; } }

        private void OnEnable()
        {
            service = SettingsRuntime.Service;
            service.StructureChanged += Refresh;
            if (content == null) CreateLayout();
            Refresh();
        }
        private void OnDisable()
        {
            if (service != null) service.StructureChanged -= Refresh;
            SettingsRuntime.Save();
        }
        private void LateUpdate() { if (dirty) Rebuild(); }
        public void SetSearch(string value) { search = value ?? ""; Refresh(); }
        public void SetShowAdvanced(bool value) { showAdvanced = value; Refresh(); }
        public void Refresh() => dirty = true;

        private void CreateLayout()
        {
            var root = SettingsTemplateUI.Rect("Settings Template", transform); SettingsTemplateUI.Fill(root);
            SettingsTemplateUI.Background(root, new Color(.055f, .065f, .085f, .98f));
            SettingsTemplateUI.Vertical(root, 16);
            SettingsTemplateUI.Label(root, "Settings", 24);
            SettingsTemplateUI.Label(root, "Search settings or groups", 13);
            var searchField = SettingsTemplateUI.Input(root); searchField.onValueChanged.AddListener(SetSearch);
            var advanced = SettingsTemplateUI.Toggle(root, "Show advanced settings"); advanced.onValueChanged.AddListener(SetShowAdvanced);
            var scrollRect = SettingsTemplateUI.Rect("Scroll", root);
            scrollRect.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1;
            var scroll = scrollRect.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            var viewport = SettingsTemplateUI.Rect("Viewport", scrollRect); SettingsTemplateUI.Fill(viewport);
            SettingsTemplateUI.Background(viewport, new Color(.055f, .065f, .085f));
            viewport.gameObject.AddComponent<RectMask2D>();
            content = SettingsTemplateUI.Rect("Content", viewport);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1);
            content.sizeDelta = Vector2.zero;
            SettingsTemplateUI.Vertical(content, 0);
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport; scroll.content = content; scroll.scrollSensitivity = 30;
        }

        private bool Matches(SettingDefinition definition, string path) => (!definition.Advanced || showAdvanced) &&
            (string.IsNullOrWhiteSpace(search) ||
             (path + " " + definition.DisplayName + " " + definition.Description + " " + definition.Id)
             .IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);

        private bool HasVisible(SettingsGroup group, string path) =>
            group.Settings.Any(d => service.IsActive(d) && Matches(d, path)) ||
            group.Children.Any(child => HasVisible(child, path + " / " + child.DisplayName));

        private void Rebuild()
        {
            dirty = false;
            foreach (var root in generated)
            {
                foreach (var widget in root.GetComponentsInChildren<SettingsWidget>(true)) widget.Unbind();
                root.SetActive(false);
                if (Application.isPlaying) Destroy(root);
                else DestroyImmediate(root);
            }
            generated.Clear();
            foreach (var group in service.ActiveGroups.ToArray())
            {
                if (!HasVisible(group, group.DisplayName)) continue;
                generated.Add(BuildGroup(group, content, group.DisplayName).gameObject);
            }
            if (generated.Count == 0)
                generated.Add(SettingsTemplateUI.Label(content, "No active settings match this view.").gameObject);
        }

        private RectTransform BuildGroup(SettingsGroup group, Transform parent, string path)
        {
            var root = SettingsTemplateUI.Rect(group.DisplayName, parent); SettingsTemplateUI.Vertical(root, 4);
            bool isCollapsed = string.IsNullOrWhiteSpace(search) && collapsed.TryGetValue(group, out var closed) && closed;
            var header = SettingsTemplateUI.Button(root, (isCollapsed ? "+ " : "- ") + group.DisplayName);
            var body = SettingsTemplateUI.Rect("Body", root); SettingsTemplateUI.Vertical(body, 8);
            foreach (var definition in group.Settings.OrderBy(d => d.Order).ThenBy(d => d.DisplayName))
            {
                if (!service.IsActive(definition) || !Matches(definition, path)) continue;
                var prefab = theme != null ? theme.Resolve(definition) : null;
                var widget = prefab != null ? Instantiate(prefab, body) :
                    SettingsTemplateUI.Rect(definition.Id, body).gameObject.AddComponent<DefaultSettingsWidget>();
                widget.Bind(service, definition);
            }
            foreach (var child in group.Children.OrderBy(g => g.Order).ThenBy(g => g.DisplayName))
                if (HasVisible(child, path + " / " + child.DisplayName)) BuildGroup(child, body, path + " / " + child.DisplayName);
            body.gameObject.SetActive(!isCollapsed);
            header.onClick.AddListener(() =>
            {
                collapsed[group] = body.gameObject.activeSelf;
                body.gameObject.SetActive(!body.gameObject.activeSelf);
                header.GetComponentInChildren<Text>().text = (body.gameObject.activeSelf ? "- " : "+ ") + group.DisplayName;
            });
            return root;
        }
    }
}
