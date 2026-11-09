using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnscriptedEngine.Dialogue
{
    [Serializable]
    public sealed class DialogueChoice
    {
        [SerializeField]
        [Tooltip("Text displayed on this choice button.")]
        private string text;
        [SerializeField]
        [Tooltip("Zero-based destination line. -1 ends the conversation.")]
        private int nextLine = -1;

        public string Text => text;
        public int NextLine => nextLine;
        public DialogueChoice(string text, int nextLine) { this.text = text; this.nextLine = nextLine; }
    }

    [Serializable]
    public sealed class DialogueLine
    {
        [SerializeField]
        [Tooltip("Speaker ID resolved against the participants supplied to the session.")]
        private string speakerId;
        [SerializeField]
        [TextArea(2, 6)]
        [Tooltip("Complete line of dialogue. TextMeshPro rich text is supported by the UI presenters.")]
        private string text;
        [SerializeField]
        [Tooltip("Destination when advancing. -2 means following line; -1 ends the conversation.")]
        private int nextLine = -2;
        [SerializeField]
        [Tooltip("Optional choices. When present, advancing waits for a selection.")]
        private DialogueChoice[] choices = Array.Empty<DialogueChoice>();

        public string SpeakerId => speakerId;
        public string Text => text;
        public int NextLine => nextLine;
        public IReadOnlyList<DialogueChoice> Choices => choices ?? Array.Empty<DialogueChoice>();
        public DialogueLine(string speakerId, string text, int nextLine = -2, params DialogueChoice[] choices)
        {
            this.speakerId = speakerId;
            this.text = text;
            this.nextLine = nextLine;
            this.choices = choices == null ? Array.Empty<DialogueChoice>() : (DialogueChoice[])choices.Clone();
        }
    }

    [CreateAssetMenu(menuName = "UnscriptedEngine/Dialogue/Conversation")]
    public sealed class DialogueDefinition : ScriptableObject
    {
        [SerializeField]
        [Tooltip("Conversation lines. Entry point is line 0; destinations use zero-based indices.")]
        private DialogueLine[] lines = Array.Empty<DialogueLine>();

        public IReadOnlyList<DialogueLine> Lines => lines ?? Array.Empty<DialogueLine>();

        /// <summary>Creates transient data for procedural conversations and examples.</summary>
        public static DialogueDefinition CreateRuntime(params DialogueLine[] lines)
        {
            DialogueDefinition definition = CreateInstance<DialogueDefinition>();
            definition.lines = lines == null ? Array.Empty<DialogueLine>() : (DialogueLine[])lines.Clone();
            return definition;
        }

        public bool TryValidate(out string error)
        {
            if (Lines.Count == 0) { error = "Conversation has no lines."; return false; }
            for (int i = 0; i < Lines.Count; i++)
            {
                DialogueLine line = Lines[i];
                if (line == null) { error = $"Line {i} is missing."; return false; }
                if (line.NextLine < -2 || line.NextLine >= Lines.Count)
                { error = $"Line {i} has an invalid destination."; return false; }
                foreach (DialogueChoice choice in line.Choices)
                    if (choice == null || choice.NextLine < -1 || choice.NextLine >= Lines.Count)
                    { error = $"Line {i} has an invalid choice destination."; return false; }
            }
            error = null;
            return true;
        }
    }
}
