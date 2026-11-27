using System.Collections.Generic;
using System.Text;

namespace Lumera.ZRay
{
    public readonly struct CommandToken
    {
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

    public readonly struct TokenPosition
    {
        public int Index { get; }
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
