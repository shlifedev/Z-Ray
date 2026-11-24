using System.Collections.Generic;

namespace Lumera.ZRay
{
    public readonly struct TranscriptLine
    {
        public string Text { get; }
        public EConsoleLineKind Kind { get; }
        public string Recall { get; }

        public TranscriptLine(string text, EConsoleLineKind kind, string recall)
        {
            Text = text;
            Kind = kind;
            Recall = recall ?? "";
        }
    }

    public sealed class ConsoleTranscript
    {
        public const int MAX_LINES = 300;

        private readonly List<TranscriptLine> _lines = new List<TranscriptLine>();

        public IReadOnlyList<TranscriptLine> Lines => _lines;
        public int Version { get; private set; }

        public void Write(string text, EConsoleLineKind kind = EConsoleLineKind.Info)
        {
            string[] parts = (text ?? "").Split('\n');
            for (int index = 0; index < parts.Length; index++)
            {
                Add(new TranscriptLine(parts[index].TrimEnd('\r'), kind, ""));
            }

            Version++;
        }

        public void Append(ExecutionResult result)
        {
            if (result.IsCleared)
            {
                Clear();
                return;
            }

            if (result.Lines.Count == 0)
            {
                return;
            }

            for (int index = 0; index < result.Lines.Count; index++)
            {
                ConsoleLine line = result.Lines[index];
                Add(new TranscriptLine(line.Text, line.Kind, line.Kind == EConsoleLineKind.Echo ? result.Line : ""));
            }

            Version++;
        }

        public void Clear()
        {
            _lines.Clear();
            Version++;
        }

        private void Add(TranscriptLine line)
        {
            _lines.Add(line);
            if (_lines.Count > MAX_LINES)
            {
                _lines.RemoveAt(0);
            }
        }
    }
}
