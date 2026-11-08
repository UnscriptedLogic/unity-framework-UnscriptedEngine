using System.Text;

namespace UnscriptedEngine.UI
{
    /// <summary>Implement on components beside DebugEntity. Called only when a debug view needs data.</summary>
    public interface IDebugInfoSource
    {
        void CollectDebugInfo(DebugInfoWriter writer);
    }

    /// <summary>Reusable, bounded plain-text output shared by every entity debug view.</summary>
    public sealed class DebugInfoWriter
    {
        private const int MaximumCharacters = 4096;
        private const int MaximumLines = 24;
        private readonly StringBuilder text = new StringBuilder(512);
        private int lines;

        public void Add<T>(string name, T value)
        {
            if (lines >= MaximumLines || text.Length >= MaximumCharacters) return;
            if (text.Length > 0) text.Append('\n');
            Append(name);
            Append(": ");
            Append(value == null ? "null" : value.ToString());
            lines++;
        }

        private void Append(string value)
        {
            if (string.IsNullOrEmpty(value)) return;
            int count = System.Math.Min(value.Length, System.Math.Max(0, MaximumCharacters - text.Length));
            // Keep one value per line, even when user-provided strings contain line breaks.
            for (int index = 0; index < count; index++)
                text.Append(value[index] == '\n' || value[index] == '\r' ? ' ' : value[index]);
        }

        internal void Clear() { text.Clear(); lines = 0; }
        public override string ToString() => text.ToString();
    }
}
