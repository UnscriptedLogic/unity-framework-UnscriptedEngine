using UnityEngine;
using UnscriptedEngine.Dialogue;

namespace UnscriptedEngine.UI.Dialogue
{
    /// <summary>Add alongside shop, quest or other interaction components to supply local speech independently.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UInteractableComponent))]
    public sealed class SpeechBubbleInteraction : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Interaction that triggers speech on the initiating client. Empty uses this object's interactable.")]
        private UInteractableComponent interactable;
        [SerializeField]
        [Tooltip("Dedicated dialogue runner for the overhead bubble. Configure automatic advance for greetings.")]
        private DialogueRunner runner;

        private UInteractableComponent boundInteraction;
        private DialogueSession startedSession;
        public DialogueRunner Runner => runner;

        public void Configure(UInteractableComponent source, DialogueRunner dialogueRunner)
        {
            Unbind();
            CancelSpeech();
            interactable = source;
            runner = dialogueRunner;
            if (isActiveAndEnabled) Bind();
        }

        public void Speak() => TrySpeak();

        public bool TrySpeak()
        {
            if (!isActiveAndEnabled || runner == null || !runner.TryPlay()) return false;
            startedSession = runner.Session;
            return true;
        }

        public void CancelSpeech()
        {
            if (runner != null && startedSession != null && runner.Session == startedSession) runner.Cancel();
            startedSession = null;
        }

        private void HandleInteraction(UInteractableComponent.UInteractArgs args) => Speak();

        private void Bind()
        {
            Unbind();
            boundInteraction = interactable != null ? interactable : GetComponent<UInteractableComponent>();
            if (boundInteraction != null) boundInteraction.InteractedLocally += HandleInteraction;
        }

        private void Unbind()
        {
            if (boundInteraction != null) boundInteraction.InteractedLocally -= HandleInteraction;
            boundInteraction = null;
        }

        private void OnEnable() => Bind();
        private void OnDisable() { Unbind(); CancelSpeech(); }
    }
}
