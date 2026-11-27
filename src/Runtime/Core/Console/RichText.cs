using System;
using System.Collections.Generic;
using System.Text;

namespace Lumera.ZRay
{
    public enum ETextStyle
    {
        Normal,
        Muted,
        Accent,
        Error
    }

    public readonly struct StyledText
    {
        public string Text { get; }
        public ETextStyle Style { get; }

        public StyledText(string text, ETextStyle style)
        {
            Text = text ?? "";
            Style = style;
        }
    }

    public static class RichText
    {
        public static void AppendPlain(StringBuilder builder, string text)
        {
            if (!string.IsNullOrEmpty(text))
            {
                builder.Append("<noparse>").Append(text).Append("</noparse>");
            }
        }

        public static void AppendColored(StringBuilder builder, string text, string hex)
        {
            if (!string.IsNullOrEmpty(text))
            {
                builder.Append("<color=").Append(hex).Append('>');
                AppendPlain(builder, text);
                builder.Append("</color>");
            }
        }

        public static void AppendHighlighted(StringBuilder builder, string text, MatchSpan[] spans, string hex)
        {
            int position = 0;
            for (int index = 0; index < spans.Length; index++)
            {
                int start = spans[index].Start;
                int end = start + spans[index].Length;
                if (start < position || end > text.Length)
                {
                    continue;
                }

                AppendPlain(builder, text.Substring(position, start - position));
                AppendColored(builder, text.Substring(start, end - start), hex);
                position = end;
            }

            AppendPlain(builder, text.Substring(position));
        }

        public static void AppendStyled(StringBuilder builder, IReadOnlyList<StyledText> parts, Func<ETextStyle, string> getHex)
        {
            for (int index = 0; index < parts.Count; index++)
            {
                AppendColored(builder, parts[index].Text, getHex(parts[index].Style));
            }
        }
    }
}
