using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnscriptedEngine.Settings;

namespace UnscriptedEngine.UI
{
    /// <summary>Automatic application-level overlay. Does not capture pointer or UI focus.</summary>
    [AddComponentMenu("")]
    public sealed class DebugOverlayDriver : MonoBehaviour
    {
        private readonly StringBuilder left = new StringBuilder(512);
        private readonly StringBuilder right = new StringBuilder(512);
        private IDisposable registration;
        private IDisposable visibilitySubscription;
        private GameObject display;
        private Text leftText;
        private Text rightText;
        private float nextRefresh;
        private EntityDebugDisplay entityDisplay;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (Application.isBatchMode) return;
            GameObject host = new GameObject("Debug Overlay");
            DontDestroyOnLoad(host);
            host.AddComponent<DebugOverlayDriver>();
        }

        private void OnEnable()
        {
            List<SettingDefinition> definitions = new List<SettingDefinition>(DebugEntities.Definitions);
            definitions.Add(DebugOverlay.VisibleSetting);
            registration = SettingsRuntime.Service.Register(definitions);
            visibilitySubscription = SettingsRuntime.Service.Observe(DebugOverlay.VisibleSetting, SetVisible);
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame)
                SettingsRuntime.Service.TrySet(DebugOverlay.VisibleSetting, !DebugOverlay.IsVisible);
            if (DebugOverlay.IsVisible && Keyboard.current != null && Keyboard.current.f4Key.wasPressedThisFrame &&
                SettingsRuntime.Service.Get(DebugEntities.InspectSetting))
            {
                entityDisplay?.UpdateTarget();
                DebugEntities.TogglePin();
                nextRefresh = 0f;
            }
        }

        private void LateUpdate()
        {
            if (!DebugOverlay.IsVisible) return;
            if (Time.unscaledTime < nextRefresh)
            {
                entityDisplay?.UpdatePositions();
                return;
            }
            nextRefresh = Time.unscaledTime + 0.1f;
            DebugOverlay.Prune();
            left.Clear();
            right.Clear();
            foreach (DebugOverlay.Entry entry in DebugOverlay.Entries)
            {
                StringBuilder column = entry.Side == DebugOverlaySide.TopRight ? right : left;
                if (column.Length > 0) column.Append('\n');
                column.Append(entry.Name).Append(": ").Append(entry.Value);
            }
            entityDisplay?.Refresh(right);
            RefreshColumn(leftText, left);
            RefreshColumn(rightText, right);
        }

        private void SetVisible(bool visible)
        {
            if (visible && display == null) CreateDisplay();
            if (display != null) display.SetActive(visible);
            if (!visible)
            {
                DebugOverlay.Clear();
                entityDisplay?.Hide();
            }
            nextRefresh = 0f;
        }

        private void CreateDisplay()
        {
            display = new GameObject("Display", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            display.transform.SetParent(transform, false);
            Canvas canvas = display.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;
            CanvasScaler scaler = display.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            leftText = CreateColumn("Top Left", false, font);
            rightText = CreateColumn("Top Right", true, font);
            entityDisplay = new EntityDebugDisplay(gameObject, display, font);
        }

        private Text CreateColumn(string name, bool alignRight, Font font)
        {
            GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(display.transform, false);
            Image background = panel.GetComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.65f);
            background.raycastTarget = false;
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            Vector2 anchor = new Vector2(alignRight ? 1f : 0f, 1f);
            panelRect.anchorMin = anchor;
            panelRect.anchorMax = anchor;
            panelRect.pivot = anchor;
            panelRect.anchoredPosition = new Vector2(alignRight ? -8f : 8f, -8f);

            GameObject label = new GameObject("Values", typeof(RectTransform), typeof(Text));
            label.transform.SetParent(panel.transform, false);
            RectTransform labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(5f, 3f);
            labelRect.offsetMax = new Vector2(-5f, -3f);
            Text text = label.GetComponent<Text>();
            text.font = font;
            text.fontSize = 16;
            text.color = new Color(0.9f, 0.9f, 0.9f);
            text.supportRichText = false;
            text.raycastTarget = false;
            text.alignment = alignRight ? TextAnchor.UpperRight : TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private void RefreshColumn(Text text, StringBuilder content)
        {
            text.transform.parent.gameObject.SetActive(content.Length > 0);
            if (content.Length == 0) return;
            string value = content.ToString();
            if (text.text != value) text.text = value;
            RectTransform canvasRect = display.GetComponent<RectTransform>();
            float width = Mathf.Min(text.preferredWidth + 10f, canvasRect.rect.width * 0.48f);
            RectTransform panel = (RectTransform)text.transform.parent;
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                Mathf.Min(text.preferredHeight + 6f, canvasRect.rect.height - 16f));
        }

        private void OnDisable()
        {
            visibilitySubscription?.Dispose();
            visibilitySubscription = null;
            registration?.Dispose();
            registration = null;
            if (display != null) display.SetActive(false);
            entityDisplay?.Hide();
            DebugEntities.ClearPin();
            DebugOverlay.Clear();
        }
    }
}
