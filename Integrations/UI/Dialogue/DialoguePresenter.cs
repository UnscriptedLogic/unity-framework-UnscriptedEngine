using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnscriptedEngine.Dialogue;

namespace UnscriptedEngine.UI.Dialogue
{
    /// <summary>Shared text reveal and pooled choices. Concrete presenters only control placement.</summary>
    public abstract class DialoguePresenter : UCanvasComponent
    {
        [SerializeField]
        [Tooltip("Visible content root. Keep the presenter itself enabled while hiding this child.")]
        private GameObject contentRoot;
        [SerializeField]
        [Tooltip("Label displaying the complete current line.")]
        private TMP_Text lineLabel;
        [SerializeField]
        [Tooltip("Optional speaker name label.")]
        private TMP_Text speakerLabel;
        [SerializeField]
        [Tooltip("Optional speaker portrait image.")]
        private Image portrait;
        [SerializeField]
        [Tooltip("Inactive choice button template containing a TMP label. Clones are reused.")]
        private Button choiceTemplate;
        [SerializeField]
        [Tooltip("Optional advance button. First press completes text reveal.")]
        private Button advanceButton;
        [SerializeField]
        [Min(0f)]
        [Tooltip("Characters revealed per second. Zero displays the entire line immediately.")]
        private float charactersPerSecond = 40f;

        private readonly List<Button> buttons = new List<Button>();
        private readonly List<TMP_Text> buttonLabels = new List<TMP_Text>();
        private readonly List<UnityEngine.Events.UnityAction> buttonActions = new List<UnityEngine.Events.UnityAction>();
        private DialogueLine currentLine;
        private float visibleCharacters;
        private int characterCount;
        private DialogueRunner runner;
        public event Action AdvanceRequested;
        public event Action<int> ChoiceRequested;
        public event Action Unavailable;
        public override CanvasLayer Layer => CanvasLayer.HUD;
        public override CanvasInputPolicy InputPolicy => CanvasInputPolicy.Passive;
        public override bool Dismissible => false;
        public bool IsRevealed => currentLine != null && lineLabel.maxVisibleCharacters >= characterCount;
        public bool IsConfigured => contentRoot != null && contentRoot != gameObject &&
            contentRoot.transform.IsChildOf(transform) && lineLabel != null;
        public bool SupportsChoices => choiceTemplate != null && choiceTemplate.GetComponentInChildren<TMP_Text>(true) != null;

        internal bool TryAcquire(DialogueRunner owner)
        {
            if (runner != null && runner != owner) return false;
            runner = owner;
            return true;
        }

        internal void Release(DialogueRunner owner) { if (runner == owner) runner = null; }

        public void Configure(GameObject content, TMP_Text text, TMP_Text speaker, Button choices, Button advance, Image speakerPortrait = null)
        {
            UnbindAdvance();
            contentRoot = content;
            lineLabel = text;
            speakerLabel = speaker;
            choiceTemplate = choices;
            advanceButton = advance;
            portrait = speakerPortrait;
            BindAdvance();
            if (choiceTemplate != null) choiceTemplate.gameObject.SetActive(false);
            HideDialogue();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            BindAdvance();
            if (choiceTemplate != null) choiceTemplate.gameObject.SetActive(false);
            if (currentLine == null && IsConfigured) contentRoot.SetActive(false);
        }

        private void BindAdvance()
        {
            if (advanceButton == null) return;
            advanceButton.onClick.RemoveListener(RequestAdvance);
            advanceButton.onClick.AddListener(RequestAdvance);
        }
        private void UnbindAdvance() { if (advanceButton != null) advanceButton.onClick.RemoveListener(RequestAdvance); }
        private void RequestAdvance() => AdvanceRequested?.Invoke();

        public virtual void Present(DialogueLine line, DialogueSpeaker speaker)
        {
            currentLine = line;
            contentRoot.SetActive(true);
            lineLabel.text = line.Text ?? string.Empty;
            lineLabel.maxVisibleCharacters = int.MaxValue;
            lineLabel.ForceMeshUpdate();
            characterCount = lineLabel.textInfo.characterCount;
            visibleCharacters = 0f;
            lineLabel.maxVisibleCharacters = charactersPerSecond <= 0f ? characterCount : 0;
            if (speakerLabel != null) speakerLabel.text = speaker != null ? speaker.DisplayName : line.SpeakerId;
            if (portrait != null)
            {
                portrait.sprite = speaker != null ? speaker.Portrait : null;
                portrait.gameObject.SetActive(portrait.sprite != null);
            }
            SetChoicesVisible(false);
            if (advanceButton != null) advanceButton.gameObject.SetActive(true);
            if (IsRevealed) SetChoicesVisible(true);
        }

        protected virtual void Update()
        {
            if (currentLine == null || IsRevealed) return;
            visibleCharacters += Time.unscaledDeltaTime * charactersPerSecond;
            lineLabel.maxVisibleCharacters = Mathf.Min(characterCount, Mathf.FloorToInt(visibleCharacters));
            if (IsRevealed) SetChoicesVisible(true);
        }

        public void CompleteReveal()
        {
            if (currentLine == null) return;
            lineLabel.maxVisibleCharacters = characterCount;
            SetChoicesVisible(true);
        }

        private void SetChoicesVisible(bool visible)
        {
            int count = visible && currentLine != null ? currentLine.Choices.Count : 0;
            while (buttons.Count < count && SupportsChoices)
            {
                int index = buttons.Count;
                Button button = Instantiate(choiceTemplate, choiceTemplate.transform.parent);
                UnityEngine.Events.UnityAction action = () => ChoiceRequested?.Invoke(index);
                button.onClick.AddListener(action);
                buttons.Add(button);
                buttonLabels.Add(button.GetComponentInChildren<TMP_Text>(true));
                buttonActions.Add(action);
            }
            for (int i = 0; i < buttons.Count; i++)
            {
                buttons[i].gameObject.SetActive(i < count);
                if (i < count) buttonLabels[i].text = currentLine.Choices[i].Text;
            }
            if (advanceButton != null && visible) advanceButton.gameObject.SetActive(count == 0);
        }

        public virtual void HideDialogue()
        {
            currentLine = null;
            if (IsConfigured) contentRoot.SetActive(false);
            SetChoicesVisible(false);
        }

        protected override void OnDisable()
        {
            UnbindAdvance();
            Unavailable?.Invoke();
            HideDialogue();
            base.OnDisable();
        }

        protected override void OnDestroy()
        {
            UnbindAdvance();
            for (int i = 0; i < buttons.Count; i++)
                if (buttons[i] != null) buttons[i].onClick.RemoveListener(buttonActions[i]);
            base.OnDestroy();
        }
    }
}
