using UnityEngine;
using UnityEngine.UI;

namespace UnscriptedEngine.UI.Settings
{
    /// <summary>Small, dependency-free uGUI template factory; themed widget prefabs can replace its controls.</summary>
    public static class SettingsTemplateUI
    {
        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }
        public static void Fill(RectTransform rect, float inset = 0)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.one * inset; rect.offsetMax = Vector2.one * -inset;
        }
        public static Image Background(RectTransform rect, Color? color = null)
        {
            var image = rect.GetComponent<Image>() ?? rect.gameObject.AddComponent<Image>();
            image.color = color ?? new Color(.16f, .18f, .22f, 1f);
            return image;
        }
        public static void Height(RectTransform rect, float height, float width = -1)
        {
            var layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = height; layout.preferredHeight = height;
            if (width >= 0) { layout.minWidth = width; layout.preferredWidth = width; }
        }
        public static VerticalLayoutGroup Vertical(RectTransform rect, int padding = 8)
        {
            var layout = rect.GetComponent<VerticalLayoutGroup>() ?? rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.spacing = 6; layout.childControlHeight = true; layout.childForceExpandHeight = false;
            layout.childControlWidth = true; layout.childForceExpandWidth = true;
            return layout;
        }
        public static Text Label(Transform parent, string value, int size = 16)
        {
            var rect = Rect("Label", parent);
            var label = rect.gameObject.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = value; label.fontSize = size; label.color = Color.white;
            label.alignment = TextAnchor.MiddleLeft; label.raycastTarget = false;
            Height(rect, size + 10);
            return label;
        }
        public static Button Button(Transform parent, string value)
        {
            var rect = Rect(value, parent); Height(rect, 32);
            var background = Background(rect);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = background;
            var label = Label(rect, value); Fill(label.rectTransform, 5);
            return button;
        }
        public static InputField Input(Transform parent, string initial = "")
        {
            var rect = Rect("Input", parent); Height(rect, 32);
            var background = Background(rect);
            var input = rect.gameObject.AddComponent<InputField>(); input.targetGraphic = background;
            var label = Label(rect, ""); Fill(label.rectTransform, 6);
            label.supportRichText = false;
            input.textComponent = label; input.text = initial;
            return input;
        }
        public static Toggle Toggle(Transform parent, string caption)
        {
            var rect = Rect("Toggle", parent); Height(rect, 30);
            var toggle = rect.gameObject.AddComponent<Toggle>();
            var box = Rect("Box", rect); box.anchorMin = box.anchorMax = new Vector2(0, .5f);
            box.pivot = new Vector2(0, .5f); box.sizeDelta = new Vector2(24, 24);
            toggle.targetGraphic = Background(box);
            var mark = Rect("Check", box); Fill(mark, 4);
            toggle.graphic = Background(mark, new Color(.3f, .8f, .65f));
            toggle.SetIsOnWithoutNotify(false);
            var label = Label(rect, caption); Fill(label.rectTransform); label.rectTransform.offsetMin = new Vector2(34, 0);
            return toggle;
        }
        public static Slider Slider(Transform parent)
        {
            var rect = Rect("Slider", parent); Height(rect, 28);
            var slider = rect.gameObject.AddComponent<Slider>();
            var track = Rect("Track", rect); Fill(track); track.offsetMin = new Vector2(8, 10); track.offsetMax = new Vector2(-8, -10);
            Background(track);
            var area = Rect("Handle Area", rect); Fill(area); area.offsetMin = new Vector2(9, 0); area.offsetMax = new Vector2(-9, 0);
            var handle = Rect("Handle", area); handle.sizeDelta = new Vector2(18, 0);
            slider.targetGraphic = Background(handle, new Color(.3f, .8f, .65f)); slider.handleRect = handle;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            return slider;
        }
        public static Dropdown Dropdown(Transform parent)
        {
            var rect = Rect("Dropdown", parent); Height(rect, 32);
            var dropdown = rect.gameObject.AddComponent<Dropdown>(); dropdown.targetGraphic = Background(rect);
            dropdown.captionText = Label(rect, ""); Fill(dropdown.captionText.rectTransform, 5);
            var template = Rect("Template", rect); Background(template);
            template.anchorMin = new Vector2(0, 0); template.anchorMax = new Vector2(1, 0);
            template.pivot = new Vector2(.5f, 1); template.sizeDelta = new Vector2(0, 160);
            var scroll = template.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false;
            var viewport = Rect("Viewport", template); Fill(viewport); Background(viewport); viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var content = Rect("Content", viewport); content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1); content.sizeDelta = new Vector2(0, 30);
            var item = Toggle(content, "Choice"); var itemRect = (RectTransform)item.transform;
            itemRect.anchorMin = new Vector2(0, .5f); itemRect.anchorMax = new Vector2(1, .5f); itemRect.sizeDelta = new Vector2(0, 30);
            dropdown.itemText = item.GetComponentInChildren<Text>(); dropdown.template = template;
            scroll.viewport = viewport; scroll.content = content; template.gameObject.SetActive(false);
            return dropdown;
        }
    }
}
