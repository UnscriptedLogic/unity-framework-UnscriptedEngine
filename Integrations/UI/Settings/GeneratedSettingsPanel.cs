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
        [SerializeField, Tooltip("Optional widget-prefab mappings by stable setting ID, editor hint, or value type. Leave empty for template controls.")]
        private SettingsTheme theme;
        [SerializeField, Tooltip("Optional custom content parent with a layout group and scroll sizing. Leave empty to create the full template with search and advanced filtering.")]
        private RectTransform content;
        private SettingsService service;
        private string search = "";
        private bool showAdvanced;
        private bool dirty;
        private readonly Dictionary<string, bool> collapsed = new Dictionary<string, bool>(StringComparer.Ordinal);
        private readonly List<GameObject> generated = new List<GameObject>();

        public SettingsTheme Theme
        {
            get => theme;
            set { theme = value; Refresh(); }
        }

        private void OnEnable()
        {
            service = SettingsRuntime.Service;
            service.StructureChanged += Refresh;
            if (content == null) CreateTemplateLayout();
            Refresh();
        }

        private void OnDisable()
        {
            if (service != null) service.StructureChanged -= Refresh;
            SettingsRuntime.Save();
        }

        private void LateUpdate()
        {
            if (dirty) RebuildVisibleGroups();
        }

        public void SetSearch(string value) { search = value ?? ""; Refresh(); }
        public void SetShowAdvanced(bool value) { showAdvanced = value; Refresh(); }
        public void Refresh() => dirty = true;

        private void CreateTemplateLayout()
        {
            RectTransform root = SettingsTemplateUI.Rect("Settings Template", transform);
            SettingsTemplateUI.Fill(root);
            SettingsTemplateUI.Background(root, new Color(.055f, .065f, .085f, .98f));
            SettingsTemplateUI.Vertical(root, 16);
            CreateFilterControls(root);
            content = CreateScrollableContent(root);
        }

        private void CreateFilterControls(RectTransform root)
        {
            SettingsTemplateUI.Label(root, "Settings", 24);
            SettingsTemplateUI.Label(root, "Search settings or groups", 13);
            InputField searchField = SettingsTemplateUI.Input(root);
            searchField.onValueChanged.AddListener(SetSearch);
            Toggle advancedToggle = SettingsTemplateUI.Toggle(root, "Show advanced settings");
            advancedToggle.onValueChanged.AddListener(SetShowAdvanced);
        }

        private static RectTransform CreateScrollableContent(RectTransform root)
        {
            RectTransform scrollRect = SettingsTemplateUI.Rect("Scroll", root);
            scrollRect.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1;
            ScrollRect scroll = scrollRect.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30;

            RectTransform viewport = SettingsTemplateUI.Rect("Viewport", scrollRect);
            SettingsTemplateUI.Fill(viewport);
            SettingsTemplateUI.Background(viewport, new Color(.055f, .065f, .085f));
            viewport.gameObject.AddComponent<RectMask2D>();

            RectTransform contentRoot = SettingsTemplateUI.Rect("Content", viewport);
            contentRoot.anchorMin = new Vector2(0, 1);
            contentRoot.anchorMax = Vector2.one;
            contentRoot.pivot = new Vector2(.5f, 1);
            contentRoot.sizeDelta = Vector2.zero;
            SettingsTemplateUI.Vertical(contentRoot, 0);
            contentRoot.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = contentRoot;
            return contentRoot;
        }

        private bool MatchesFilter(SettingDefinition definition)
        {
            if (definition.Advanced && !showAdvanced) return false;
            if (string.IsNullOrWhiteSpace(search)) return true;
            string searchableText = definition.Group + " " + definition.DisplayName + " " + definition.Description + " " + definition.Id;
            return searchableText.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool HasVisibleSettings(SettingsGroup group)
        {
            return group.Settings.Any(MatchesFilter) || group.Children.Any(HasVisibleSettings);
        }

        private void RebuildVisibleGroups()
        {
            dirty = false;
            ClearGeneratedGroups();
            foreach (SettingsGroup group in service.ActiveGroups)
            {
                if (!HasVisibleSettings(group)) continue;
                generated.Add(CreateGroup(group, content).gameObject);
            }
            if (generated.Count == 0)
                generated.Add(SettingsTemplateUI.Label(content, "No active settings match this view.").gameObject);
        }

        private void ClearGeneratedGroups()
        {
            foreach (GameObject root in generated)
            {
                foreach (SettingsWidget widget in root.GetComponentsInChildren<SettingsWidget>(true)) widget.Unbind();
                root.SetActive(false);
                if (Application.isPlaying) Destroy(root);
                else DestroyImmediate(root);
            }
            generated.Clear();
        }

        private RectTransform CreateGroup(SettingsGroup group, Transform parent)
        {
            RectTransform root = SettingsTemplateUI.Rect(group.DisplayName, parent);
            SettingsTemplateUI.Vertical(root, 4);
            bool isCollapsed = string.IsNullOrWhiteSpace(search) &&
                collapsed.TryGetValue(group.Path, out bool previouslyCollapsed) && previouslyCollapsed;
            Button header = SettingsTemplateUI.Button(root, GetGroupCaption(group, isCollapsed));
            RectTransform body = SettingsTemplateUI.Rect("Body", root);
            SettingsTemplateUI.Vertical(body, 8);
            CreateSettingRows(group, body);
            foreach (SettingsGroup child in group.Children.OrderBy(item => item.DisplayName, StringComparer.Ordinal))
                if (HasVisibleSettings(child)) CreateGroup(child, body);
            body.gameObject.SetActive(!isCollapsed);
            header.onClick.AddListener(() => ToggleGroup(group, body, header));
            return root;
        }

        private void CreateSettingRows(SettingsGroup group, Transform parent)
        {
            foreach (SettingDefinition definition in group.Settings.OrderBy(item => item.Order).ThenBy(item => item.DisplayName))
            {
                if (!MatchesFilter(definition)) continue;
                SettingsWidget prefab = theme != null ? theme.Resolve(definition) : null;
                SettingsWidget widget = prefab != null ? Instantiate(prefab, parent) :
                    SettingsTemplateUI.Rect(definition.Id, parent).gameObject.AddComponent<DefaultSettingsWidget>();
                widget.Bind(service, definition);
            }
        }

        private void ToggleGroup(SettingsGroup group, RectTransform body, Button header)
        {
            bool shouldCollapse = body.gameObject.activeSelf;
            collapsed[group.Path] = shouldCollapse;
            body.gameObject.SetActive(!shouldCollapse);
            header.GetComponentInChildren<Text>().text = GetGroupCaption(group, shouldCollapse);
        }

        private static string GetGroupCaption(SettingsGroup group, bool isCollapsed)
        {
            return (isCollapsed ? "+ " : "- ") + group.DisplayName;
        }
    }
}
