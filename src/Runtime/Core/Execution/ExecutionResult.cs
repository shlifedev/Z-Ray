using System;
using System.Collections.Generic;
using System.Text;

namespace Lumera.ZRay
{
    public enum EConsoleLineKind
    {
        Echo,
        Info,
        Muted,
        Success,
        Warning,
        Error
    }

    public readonly struct ConsoleLine
    {
        public string Text { get; }
        public EConsoleLineKind Kind { get; }

        public ConsoleLine(string text, EConsoleLineKind kind)
        {
            Text = text;
            Kind = kind;
        }
    }

    public sealed class ExecutionResult
    {
        private readonly List<ConsoleLine> _lines = new List<ConsoleLine>();

        public IReadOnlyList<ConsoleLine> Lines => _lines;
        public string Line { get; internal set; } = "";
        public bool IsSuccess { get; internal set; }
        public bool IsCleared { get; internal set; }
        public CheatDefinition? DefinitionOrNull { get; internal set; }
        public Exception? ExceptionOrNull { get; internal set; }

        public string Message
        {
            get
            {
                StringBuilder builder = new StringBuilder();
                for (int index = 0; index < _lines.Count; index++)
                {
                    if (_lines[index].Kind == EConsoleLineKind.Echo)
                    {
                        continue;
                    }

                    if (builder.Length > 0)
                    {
                        builder.Append('\n');
                    }

                    builder.Append(_lines[index].Text);
                }

                return builder.ToString();
            }
        }

        internal void Add(string text, EConsoleLineKind kind)
        {
            _lines.Add(new ConsoleLine(text, kind));
        }

        internal void AddLines(string text, EConsoleLineKind kind)
        {
            string[] parts = text.Split('\n');
            for (int index = 0; index < parts.Length; index++)
            {
                _lines.Add(new ConsoleLine(parts[index].TrimEnd('\r'), kind));
            }
        }
    }
}
