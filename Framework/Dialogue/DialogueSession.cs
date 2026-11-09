using System;

namespace UnscriptedEngine.Dialogue
{
    /// <summary>UI-independent progress. One instance per conversation; never writes to its asset.</summary>
    public sealed class DialogueSession
    {
        private readonly DialogueDefinition definition;
        private bool dispatching;
        private int lineIndex = -1;
        public event Action<DialogueLine> LineChanged;
        public event Action<bool> Ended;
        public bool IsRunning { get; private set; }
        public int LineIndex => lineIndex;
        public DialogueLine CurrentLine => IsRunning ? definition.Lines[lineIndex] : null;

        public DialogueSession(DialogueDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (!definition.TryValidate(out string error)) throw new ArgumentException(error, nameof(definition));
            this.definition = definition;
        }

        public bool Start()
        {
            if (dispatching || IsRunning || lineIndex >= 0) return false;
            IsRunning = true;
            MoveTo(0);
            return true;
        }

        public bool Advance()
        {
            if (dispatching || !IsRunning || CurrentLine.Choices.Count > 0) return false;
            int destination = CurrentLine.NextLine == -2 ? lineIndex + 1 : CurrentLine.NextLine;
            MoveTo(destination == definition.Lines.Count ? -1 : destination);
            return true;
        }

        public bool Choose(int index)
        {
            if (dispatching || !IsRunning || index < 0 || index >= CurrentLine.Choices.Count) return false;
            MoveTo(CurrentLine.Choices[index].NextLine);
            return true;
        }

        public void Cancel() { if (IsRunning) Finish(true); }

        private void MoveTo(int index)
        {
            if (index < 0) { Finish(false); return; }
            lineIndex = index;
            dispatching = true;
            try { LineChanged?.Invoke(CurrentLine); }
            finally { dispatching = false; }
        }

        private void Finish(bool cancelled)
        {
            IsRunning = false;
            dispatching = true;
            try { Ended?.Invoke(cancelled); }
            finally { dispatching = false; }
        }
    }
}
