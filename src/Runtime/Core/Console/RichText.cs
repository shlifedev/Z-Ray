using System;
using System.Collections.Generic;
using System.Text;

namespace Lumera.DebuggerKit
{
    // 화면에서 쓰는 글자 모양. 색은 화면마다 정한다(런타임 TMP, 에디터 UI Toolkit).
    public enum ETextStyle
    {
        Normal,
        Muted,
        Accent,
        Error
    }

    // 모양이 붙은 글자 조각. 힌트 바처럼 여러 모양이 섞인 한 줄을 화면을 모르고 만들 때 쓴다.
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

    // TMP와 UI Toolkit이 함께 읽는 리치 텍스트를 만든다.
    // 치트 이름이나 입력에 든 '<'가 태그로 읽히지 않도록 글자는 모두 noparse로 감싼다.
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

        // 일치한 구간만 색을 바꿔 쓴다. 구간은 시작 순서대로, 겹치지 않게 들어온다.
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

        // 조각마다 모양에 맞는 색(getHex)으로 쓴다.
        public static void AppendStyled(StringBuilder builder, IReadOnlyList<StyledText> parts, Func<ETextStyle, string> getHex)
        {
            for (int index = 0; index < parts.Count; index++)
            {
                AppendColored(builder, parts[index].Text, getHex(parts[index].Style));
            }
        }
    }
}
