using System.Collections.Generic;

namespace Lumera.DebuggerKit
{
    // 콘솔 기록의 한 줄. Recall은 눌렀을 때 입력 줄로 다시 가져올 명령이다(실행한 명령을 보여 주는 줄만 있다).
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

    // 콘솔에 보이는 기록. 호스트가 하나 갖고, 런타임 콘솔과 에디터 창이 같은 기록을 그린다.
    // 화면은 Version이 바뀌었을 때만 다시 그린다.
    public sealed class ConsoleTranscript
    {
        public const int MAX_LINES = 300;

        private readonly List<TranscriptLine> _lines = new List<TranscriptLine>();

        public IReadOnlyList<TranscriptLine> Lines => _lines;
        public int Version { get; private set; }

        // 실행 결과가 아닌 글(안내, 로그)을 쓴다. 여러 줄이면 줄마다 나눈다.
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
