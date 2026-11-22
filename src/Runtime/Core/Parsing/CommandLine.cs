using System.Collections.Generic;
using System.Text;

namespace Lumera.DebuggerKit
{
    // 입력 줄의 토큰 하나. Start~End는 원문에서 따옴표까지 포함한 범위다(자동완성이 이 범위를 통째로 바꾼다).
    public readonly struct CommandToken
    {
        // 따옴표와 이스케이프를 푼 값
        public string Text { get; }
        public int Start { get; }
        public int End { get; }
        public bool IsQuoted { get; }

        public CommandToken(string text, int start, int end, bool bQuoted)
        {
            Text = text;
            Start = start;
            End = end;
            IsQuoted = bQuoted;
        }
    }

    // 커서가 가리키는 토큰. 커서가 공백 위나 줄 끝 공백 뒤에 있으면 아직 없는 새 토큰(빈 접두어)을 가리킨다.
    public readonly struct TokenPosition
    {
        public int Index { get; }
        // 토큰 시작부터 커서까지의 값(이스케이프를 푼 것). 자동완성은 이 접두어로 후보를 고른다.
        public string Prefix { get; }
        public int ReplaceStart { get; }
        public int ReplaceEnd { get; }

        public TokenPosition(int index, string prefix, int replaceStart, int replaceEnd)
        {
            Index = index;
            Prefix = prefix;
            ReplaceStart = replaceStart;
            ReplaceEnd = replaceEnd;
        }
    }

    // 콘솔 입력 한 줄을 토큰으로 나눈다. 셸과 비슷한 규칙이다.
    // - 공백으로 나눈다. "..." 안의 공백은 나누지 않는다. 따옴표는 토큰 중간에 와도 된다(a"b c" → ab c).
    // - \" 와 \\ 만 이스케이프다. 그 밖의 \ 는 글자 그대로 둔다(경로 입력이 편하도록).
    // - 닫는 따옴표가 없으면 줄 끝까지 한 토큰이다. 입력하는 중간 상태도 자동완성할 수 있어야 하기 때문이다.
    public sealed class CommandLine
    {
        private readonly List<CommandToken> _tokens;

        public string Text { get; }
        public IReadOnlyList<CommandToken> Tokens => _tokens;

        private CommandLine(string text, List<CommandToken> tokens)
        {
            Text = text;
            _tokens = tokens;
        }

        public static CommandLine Parse(string? text)
        {
            string line = text ?? "";
            List<CommandToken> tokens = new List<CommandToken>();
            StringBuilder builder = new StringBuilder();
            int index = 0;
            while (index < line.Length)
            {
                if (char.IsWhiteSpace(line[index]))
                {
                    index++;
                    continue;
                }

                int start = index;
                bool bInQuote = false;
                bool bQuoted = false;
                builder.Clear();
                while (index < line.Length && (bInQuote || !char.IsWhiteSpace(line[index])))
                {
                    char character = line[index];
                    if (character == '\\' && index + 1 < line.Length && (line[index + 1] == '"' || line[index + 1] == '\\'))
                    {
                        builder.Append(line[index + 1]);
                        index += 2;
                        continue;
                    }

                    if (character == '"')
                    {
                        bInQuote = !bInQuote;
                        bQuoted = true;
                    }
                    else
                    {
                        builder.Append(character);
                    }

                    index++;
                }

                tokens.Add(new CommandToken(builder.ToString(), start, index, bQuoted));
            }

            return new CommandLine(line, tokens);
        }

        public TokenPosition FindTokenAt(int caret)
        {
            int clamped = caret < 0 ? 0 : (caret > Text.Length ? Text.Length : caret);
            for (int index = 0; index < _tokens.Count; index++)
            {
                CommandToken token = _tokens[index];
                if (clamped >= token.Start && clamped <= token.End)
                {
                    string prefix = clamped == token.End ? token.Text : ReadPrefix(token.Start, clamped);
                    return new TokenPosition(index, prefix, token.Start, token.End);
                }

                if (clamped < token.Start)
                {
                    return new TokenPosition(index, "", clamped, clamped);
                }
            }

            return new TokenPosition(_tokens.Count, "", clamped, clamped);
        }

        // 공백이나 따옴표, 역슬래시가 든 값을 다시 읽어도 같은 값이 되도록 따옴표로 감싼다. 빈 값은 ""가 된다.
        public static string Quote(string value)
        {
            bool bNeedsQuote = value.Length == 0;
            for (int index = 0; index < value.Length && !bNeedsQuote; index++)
            {
                char character = value[index];
                bNeedsQuote = char.IsWhiteSpace(character) || character == '"' || character == '\\';
            }

            if (!bNeedsQuote)
            {
                return value;
            }

            StringBuilder builder = new StringBuilder(value.Length + 2);
            builder.Append('"');
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (character == '"' || character == '\\')
                {
                    builder.Append('\\');
                }

                builder.Append(character);
            }

            builder.Append('"');
            return builder.ToString();
        }

        private string ReadPrefix(int start, int caret)
        {
            CommandLine partial = Parse(Text.Substring(start, caret - start));
            return partial._tokens.Count == 0 ? "" : partial._tokens[0].Text;
        }
    }
}
