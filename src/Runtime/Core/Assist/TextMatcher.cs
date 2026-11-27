using System;
using System.Collections.Generic;

namespace Lumera.ZRay
{
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
        Prefix,
        SegmentPrefix,
        Substring,
        Initials
    }

    public static class TextMatcher
    {
        private static readonly MatchSpan[] NO_SPANS = new MatchSpan[0];

        public static EMatchKind Match(string candidate, string query)
        {
            return Match(candidate, query, null);
        }

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
