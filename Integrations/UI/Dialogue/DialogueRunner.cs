using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnscriptedEngine.Dialogue;

namespace UnscriptedEngine.UI.Dialogue
{
    /// <summary>Scene lifetime, participants and interaction policy around a UI-independent session.</summary>
    public sealed class DialogueRunner : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Conversation started by Play().")]
        private DialogueDefinition conversation;
        [SerializeField]
        [Tooltip("Screen or world presenter exclusively owned by this runner while playing.")]
        private DialoguePresenter presenter;
        [SerializeField]
        [Tooltip("Scene speakers. IDs must be unique and cover every non-empty speaker ID in the conversation.")]
        private DialogueSpeaker[] participants = Array.Empty<DialogueSpeaker>();
        [SerializeField]
        [Tooltip("Controller whose actions drive dialogue and whose gameplay may be blocked. Optional for button-only UI.")]
        private UController inputOwner;
        [SerializeField]
        [Tooltip("Block the input owner's gameplay while dialogue runs. Without an owner, blocks gameplay globally.")]
        private bool blockGameplay;
        [SerializeField]
        [Tooltip("Advance lines automatically after text reveal. Choices still require selection.")]
        private bool automaticAdvance;
        [SerializeField]
        [Min(0f)]
        [Tooltip("Seconds each fully revealed line stays visible in automatic mode.")]
        private float automaticDelay = 2f;
        [SerializeField]
        [Tooltip("Input System action key used to reveal or advance. Empty disables action input.")]
        private string advanceAction = "UI/DialogueAdvance";
        [SerializeField]
        [Tooltip("Invoked when the conversation finishes normally.")]
        private UnityEvent completed = new UnityEvent();
        [SerializeField]
        [Tooltip("Invoked when the conversation is cancelled, disabled, or loses a participant/presenter.")]
        private UnityEvent cancelled = new UnityEvent();

        private readonly Dictionary<string, DialogueSpeaker> speakers = new Dictionary<string, DialogueSpeaker>(StringComparer.Ordinal);
        private DialogueSession session;
        private IDisposable focusLease;
        private float elapsed;
        private bool stopping;
        public bool IsRunning => session != null && session.IsRunning;
        public DialogueSession Session => session;

        public void Configure(DialogueDefinition definition, DialoguePresenter view, DialogueSpeaker[] actors,
            UController owner = null, bool modal = false, bool timed = false)
        {
            Cancel();
            conversation = definition;
            presenter = view;
            participants = actors == null ? Array.Empty<DialogueSpeaker>() : (DialogueSpeaker[])actors.Clone();
            inputOwner = owner;
            blockGameplay = modal;
            automaticAdvance = timed;
        }

        public void Play() => TryPlay();

        /// <summary>Wire the interactable's local completion event with source to this method.</summary>
        public void PlayForInteractor(GameObject source)
        {
            if (source == null || IsRunning) return;
            UPawn pawn = source.GetComponentInParent<UPawn>();
            UController owner = pawn != null ? pawn.Controller : source.GetComponentInParent<UController>();
            if (owner == null) return;
            inputOwner = owner;
            Play();
        }

        public bool TryPlay()
        {
            if (!isActiveAndEnabled || stopping || IsRunning) return false;
            if (!ValidateSetup(out string error)) { Debug.LogWarning(error, this); return false; }
            if (!presenter.TryAcquire(this)) return false;
            session = new DialogueSession(conversation);
            session.LineChanged += ShowLine;
            session.Ended += HandleEnded;
            presenter.AdvanceRequested += Advance;
            presenter.ChoiceRequested += Choose;
            presenter.Unavailable += Cancel;
            EventBus.Subscribe<PlayerActionInputEvent>(HandleAction, UCanvasController.InputPriority - 1);
            if (blockGameplay) focusLease = InputFocus.Acquire(this, inputOwner);
            try { session.Start(); }
            catch { Stop(false, false); throw; }
            return IsRunning;
        }

        private bool ValidateSetup(out string error)
        {
            error = "Dialogue requires an enabled, configured presenter and valid conversation.";
            if (presenter == null || !presenter.isActiveAndEnabled || !presenter.IsConfigured || conversation == null) return false;
            if (!conversation.TryValidate(out error)) return false;
            speakers.Clear();
            foreach (DialogueSpeaker speaker in participants)
            {
                if (speaker == null || string.IsNullOrEmpty(speaker.SpeakerId) || speakers.ContainsKey(speaker.SpeakerId))
                { error = "Dialogue participants contain a missing speaker, empty ID, or duplicate ID."; return false; }
                speakers.Add(speaker.SpeakerId, speaker);
            }
            foreach (DialogueLine line in conversation.Lines)
            {
                if (!string.IsNullOrEmpty(line.SpeakerId) && !speakers.ContainsKey(line.SpeakerId))
                { error = "Dialogue speaker is not bound: " + line.SpeakerId; return false; }
                if (presenter is WorldDialoguePresenter && string.IsNullOrEmpty(line.SpeakerId))
                { error = "World dialogue requires a speaker anchor on every line."; return false; }
                if (line.Choices.Count > 0 && !presenter.SupportsChoices)
                { error = "Dialogue contains choices but presenter has no valid choice button template."; return false; }
            }
            return true;
        }

        private void ShowLine(DialogueLine line)
        {
            elapsed = 0f;
            DialogueSpeaker speaker = null;
            if (!string.IsNullOrEmpty(line.SpeakerId)) speakers.TryGetValue(line.SpeakerId, out speaker);
            presenter.Present(line, speaker);
        }

        public void Advance()
        {
            if (!IsRunning) return;
            if (!presenter.IsRevealed) presenter.CompleteReveal();
            else session.Advance();
        }

        public void Choose(int index)
        {
            if (IsRunning && presenter.IsRevealed) session.Choose(index);
        }

        public void Cancel() { if (IsRunning) session.Cancel(); }

        private void HandleAction(PlayerActionInputEvent input)
        {
            if (!IsRunning || automaticAdvance || inputOwner == null || input.Controller != inputOwner ||
                input.IsCancelled || input.IsSynthetic || !input.IsPerformed || !input.IsPressed ||
                !string.Equals(input.ActionKey, advanceAction, StringComparison.OrdinalIgnoreCase)) return;
            if (presenter.Controller != null && presenter.Controller.FocusedCanvas != null &&
                presenter.Controller.FocusedCanvas != presenter) return;
            if (session.CurrentLine.Choices.Count > 0 && presenter.IsRevealed) return;
            input.Cancel();
            Advance();
        }

        private void Update()
        {
            if (!IsRunning) return;
            foreach (DialogueSpeaker speaker in speakers.Values)
                if (speaker == null || !speaker.isActiveAndEnabled) { Cancel(); return; }
            if (presenter == null || !presenter.isActiveAndEnabled ||
                (presenter is WorldDialoguePresenter world && !world.HasAnchor)) { Cancel(); return; }
            if (!automaticAdvance || !presenter.IsRevealed || session.CurrentLine.Choices.Count > 0) return;
            elapsed += Time.unscaledDeltaTime;
            if (elapsed >= automaticDelay) session.Advance();
        }

        private void HandleEnded(bool wasCancelled) => Stop(wasCancelled, true);

        private void Stop(bool wasCancelled, bool notify)
        {
            stopping = true;
            try
            {
                EventBus.Unsubscribe<PlayerActionInputEvent>(HandleAction);
                if (session != null)
                {
                    session.LineChanged -= ShowLine;
                    session.Ended -= HandleEnded;
                    session = null;
                }
                if (presenter != null)
                {
                    presenter.AdvanceRequested -= Advance;
                    presenter.ChoiceRequested -= Choose;
                    presenter.Unavailable -= Cancel;
                    presenter.HideDialogue();
                    presenter.Release(this);
                }
                focusLease?.Dispose();
                focusLease = null;
                speakers.Clear();
                if (notify) { if (wasCancelled) cancelled.Invoke(); else completed.Invoke(); }
            }
            finally { stopping = false; }
        }

        private void OnDisable() => Cancel();
    }
}
