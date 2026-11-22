using System;
using System.Collections.Generic;
using System.Text;

namespace Lumera.ZRay
{
    public enum EConsoleLineKind
    {
        // 실행한 명령을 그대로 보여 주는 줄
        Echo,
        Info,
        // 안내처럼 흐리게 보일 줄
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

    // 명령 한 줄을 실행한 결과. 콘솔 기록에 더할 줄과, 화면·게임이 뒤처리에 쓰는 정보를 담는다.
    public sealed class ExecutionResult
    {
        private readonly List<ConsoleLine> _lines = new List<ConsoleLine>();

        public IReadOnlyList<ConsoleLine> Lines => _lines;
        // 실행한 명령 줄(앞뒤 공백을 뗀 것). 콘솔 기록에서 다시 불러올 때 쓴다.
        public string Line { get; internal set; } = "";
        public bool IsSuccess { get; internal set; }
        // 콘솔 기록을 비워야 한다(clear).
        public bool IsCleared { get; internal set; }
        // 실제로 실행까지 간 치트. 명령을 못 찾았거나 인자가 틀려 실행하지 않았으면 null이다.
        public CheatDefinition? DefinitionOrNull { get; internal set; }
        // 치트가 던진 예외. 호스트가 스택과 함께 로그로 남긴다.
        public Exception? ExceptionOrNull { get; internal set; }

        // 실행한 명령 줄을 뺀 결과 메시지. 메뉴 토스트에 쓴다.
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

        // 여러 줄 메시지를 줄마다 나눠 더한다.
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
