using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UnscriptedEngine.UI
{
    /// <summary>Passive view of the target selected by the gameplay interactor.</summary>
    public sealed class InteractionPromptWidget : UWidgetComponent
    {
        [SerializeField] private GameObject visualRoot;
        [SerializeField] private TextMeshProUGUI actionLabel;
        [SerializeField] private TextMeshProUGUI targetLabel;
        [SerializeField] private Image icon;
        [SerializeField] private Image progressFill;
        [SerializeField] private GameObject progressRoot;

        private UInteractableComponent target;
        private string bindingLabel = "Interact";

        public override bool Focusable => false;
        public UInteractableComponent Target => target;

        public void SetBindingLabel(string value)
        {
            string next = string.IsNullOrWhiteSpace(value) ? "Interact" : value;
            if (bindingLabel == next) return;

            bindingLabel = next;
            RefreshLabels();
        }

        public void SetTarget(UInteractableComponent value)
        {
            if (!isActiveAndEnabled || (value != null && !value.isActiveAndEnabled)) value = null;
            if (ReferenceEquals(target, value)) return;

            Unsubscribe();
            target = value;
            if (target != null) target.ProgressChanged += HandleProgress;

            RefreshLabels();
            if (target != null) HandleProgress(target.Progress01);
        }

        private void RefreshLabels()
        {
            bool visible = target != null && target.isActiveAndEnabled;
            // Keep this component active so hidden prompts can receive a new target.
            if (visualRoot != null && visualRoot != gameObject) visualRoot.SetActive(visible);
            CanvasGroup group = GetComponent<CanvasGroup>();
            group.alpha = visible ? 1f : 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            if (!visible) return;

            InteractionPromptInfo info = target.GetComponent<InteractionPromptInfo>();
            string action = info != null && !string.IsNullOrWhiteSpace(info.ActionText) ? info.ActionText : "Interact";
            string name = info != null && !string.IsNullOrWhiteSpace(info.DisplayName) ? info.DisplayName : target.gameObject.name;
            string verb = target.Mode == InteractionMode.Instant ? "Press" : "Hold";

            if (actionLabel != null) actionLabel.text = $"{verb} [{bindingLabel}]  {action}";
            if (targetLabel != null) targetLabel.text = name;
            if (icon != null)
            {
                icon.sprite = info != null ? info.Icon : null;
                icon.gameObject.SetActive(icon.sprite != null);
            }
            if (progressFill != null)
            {
                GameObject progress = progressRoot != null ? progressRoot : progressFill.gameObject;
                progress.SetActive(target.Mode != InteractionMode.Instant);
            }
        }

        private void HandleProgress(float value)
        {
            if (progressFill == null) return;

            progressFill.fillAmount = Mathf.Clamp01(value);
            if (progressFill.sprite == null)
                progressFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(value), 1f);
        }

        private void Unsubscribe()
        {
            // CLR reference check also releases the delegate on a destroyed Unity object.
            if (!ReferenceEquals(target, null)) target.ProgressChanged -= HandleProgress;
            target = null;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            RefreshLabels();
        }

        protected override void OnDisable()
        {
            Unsubscribe();
            base.OnDisable();
        }

        protected override void OnDestroy()
        {
            Unsubscribe();
            base.OnDestroy();
        }

        public static InteractionPromptWidget CreateDefault(Transform parent)
        {
            RectTransform root = Rect("Interaction prompt", parent);
            root.anchorMin = root.anchorMax = new Vector2(.5f, .22f);
            root.sizeDelta = new Vector2(360f, 92f);
            InteractionPromptWidget widget = root.gameObject.AddComponent<InteractionPromptWidget>();

            RectTransform panel = Rect("Panel", root);
            Fill(panel);
            Image background = panel.gameObject.AddComponent<Image>();
            background.color = new Color(.03f, .04f, .06f, .85f);
            background.raycastTarget = false;
            widget.visualRoot = panel.gameObject;
            widget.actionLabel = Label("Action", panel, new Vector2(0f, .4f), Vector2.one, 19);
            widget.targetLabel = Label("Target", panel, new Vector2(0f, .1f), new Vector2(1f, .5f), 15);

            RectTransform track = Rect("Progress", panel);
            track.anchorMin = new Vector2(.08f, .06f);
            track.anchorMax = new Vector2(.92f, .1f);
            track.offsetMin = track.offsetMax = Vector2.zero;
            Image trackImage = track.gameObject.AddComponent<Image>();
            widget.progressRoot = track.gameObject;
            trackImage.color = new Color(1f, 1f, 1f, .15f);
            trackImage.raycastTarget = false;

            RectTransform fill = Rect("Fill", track);
            Fill(fill);
            widget.progressFill = fill.gameObject.AddComponent<Image>();
            widget.progressFill.color = Color.white;
            widget.progressFill.raycastTarget = false;
            widget.progressFill.type = Image.Type.Simple;
            widget.RefreshLabels();
            return widget;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = parent.gameObject.layer;
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Fill(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static TextMeshProUGUI Label(string name, Transform parent, Vector2 min, Vector2 max, int size)
        {
            RectTransform rect = Rect(name, parent);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = size;
            text.alignment = TextAlignmentOptions.Center;
            text.richText = false;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }
    }
}
