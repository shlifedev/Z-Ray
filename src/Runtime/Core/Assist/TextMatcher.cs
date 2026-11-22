using System;
using System.Collections.Generic;

namespace Lumera.DebuggerKit
{
    // 후보 글자 안에서 검색어와 일치한 구간. 화면이 이 구간을 강조한다.
    public readonly struct MatchSpan
    {
        public int Start { get; }
        public int Length { get; }

        public MatchSpan(int start, int length)
        {
            Start = start;
            Length = length;
        }
    }

    public enum EMatchKind
    {
        None,
        // 처음부터 일치: pla → player.gold.add
        Prefix,
        // 구분자 뒤 조각의 처음부터 일치: gold → player.gold.add
        SegmentPrefix,
        // 중간 어디든 일치: old → player.gold.add
        Substring,
        // 조각 머리글자를 순서대로: pga → player.gold.add
        Initials
    }

    // 콘솔 자동완성과 메뉴 검색이 함께 쓰는 일치 규칙. 대소문자는 무시한다.
    // 검색 규칙을 늘리려면(예: 한글 초성) 이 클래스만 고친다.
    public static class TextMatcher
    {
        private static readonly MatchSpan[] NO_SPANS = new MatchSpan[0];

        public static EMatchKind Match(string candidate, string query)
        {
            return Match(candidate, query, null);
        }

        // spansOrNull을 주면 일치한 구간을 더한다(지우지 않는다). 일치하지 않으면 아무것도 더하지 않는다.
        public static EMatchKind Match(string candidate, string query, List<MatchSpan>? spansOrNull)
        {
            if (string.IsNullOrEmpty(query))
            {
                return EMatchKind.Prefix;
            }

            if (string.IsNullOrEmpty(candidate))
            {
                return EMatchKind.None;
            }

            if (candidate.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            {
                spansOrNull?.Add(new MatchSpan(0, query.Length));
                return EMatchKind.Prefix;
            }

            int searchFrom = 0;
            int firstFound = -1;
            while (searchFrom < candidate.Length)
            {
                int found = candidate.IndexOf(query, searchFrom, StringComparison.OrdinalIgnoreCase);
                if (found < 0)
                {
                    break;
                }

                if (firstFound < 0)
                {
                    firstFound = found;
                }

                if (IsSegmentStart(candidate, found))
                {
                    spansOrNull?.Add(new MatchSpan(found, query.Length));
                    return EMatchKind.SegmentPrefix;
                }

                searchFrom = found + 1;
            }

            if (firstFound >= 0)
            {
                spansOrNull?.Add(new MatchSpan(firstFound, query.Length));
                return EMatchKind.Substring;
            }

            return MatchInitials(candidate, query, spansOrNull) ? EMatchKind.Initials : EMatchKind.None;
        }

        public static MatchSpan[] FindSpans(string candidate, string query)
        {
            if (string.IsNullOrEmpty(query))
            {
                return NO_SPANS;
            }

            List<MatchSpan> spans = new List<MatchSpan>();
            Match(candidate, query, spans);
            return spans.Count == 0 ? NO_SPANS : spans.ToArray();
        }

        // 검색어의 글자를 순서대로, 각 조각의 앞에서부터 이어 맞춘다. 한 조각에서 여러 글자를 이어 써도 된다(plgo → PLayer.GOld).
        // 조각 중간에서 새로 시작하는 일치는 받지 않는다. 그러면 거의 모든 후보가 걸려서 순위가 쓸모없어진다.
        private static bool MatchInitials(string candidate, string query, List<MatchSpan>? spansOrNull)
        {
            int spanCountBefore = spansOrNull?.Count ?? 0;
            int queryIndex = 0;
            int index = 0;
            while (index < candidate.Length && queryIndex < query.Length)
            {
                if (!IsSegmentStart(candidate, index) || !EqualsIgnoreCase(candidate[index], query[queryIndex]))
                {
                    index++;
                    continue;
                }

                int runStart = index;
                while (index < candidate.Length && queryIndex < query.Length && !IsSeparator(candidate[index]) && EqualsIgnoreCase(candidate[index], query[queryIndex]))
                {
                    index++;
                    queryIndex++;
                }

                spansOrNull?.Add(new MatchSpan(runStart, index - runStart));
            }

            if (queryIndex == query.Length)
            {
                return true;
            }

            spansOrNull?.RemoveRange(spanCountBefore, spansOrNull.Count - spanCountBefore);
            return false;
        }

        private static bool IsSegmentStart(string text, int index)
        {
            return index == 0 || (IsSeparator(text[index - 1]) && !IsSeparator(text[index]));
        }

        private static bool IsSeparator(char character)
        {
            return character == '.' || character == '_' || character == '-' || character == '/' || char.IsWhiteSpace(character);
        }

        private static bool EqualsIgnoreCase(char left, char right)
        {
            return char.ToUpperInvariant(left) == char.ToUpperInvariant(right);
        }
    }
}
