using System;
using System.Collections.Generic;

namespace Lumera.ZRay
{
    public enum ESuggestionKind
    {
        Action,
        Value,
        Argument
    }

    public sealed class Suggestion
    {
        private static readonly MatchSpan[] NO_SPANS = new MatchSpan[0];

        public string Insert { get; }
        public string Detail { get; }
        public string Note { get; }
        public ESuggestionKind Kind { get; }
        public int Rank { get; }
        public int Order { get; }
        public MatchSpan[] InsertSpans { get; }
        public MatchSpan[] DetailSpans { get; }

        internal Suggestion(string insert, string detail, string note, ESuggestionKind kind, int rank, int order, MatchSpan[]? insertSpans, MatchSpan[]? detailSpans)
        {
            Insert = insert;
            Detail = detail;
            Note = note;
            Kind = kind;
            Rank = rank;
            Order = order;
            InsertSpans = insertSpans ?? NO_SPANS;
            DetailSpans = detailSpans ?? NO_SPANS;
        }
    }

    public readonly struct HintParameter
    {
        public CheatParameter Parameter { get; }
        public bool IsActive { get; }
        public string? ErrorOrNull { get; }

        internal HintParameter(CheatParameter parameter, bool bActive, string? errorOrNull)
        {
            Parameter = parameter;
            IsActive = bActive;
            ErrorOrNull = errorOrNull;
        }
    }

    public sealed class CommandHint
    {
        public CheatDefinition Definition { get; }
        public IReadOnlyList<HintParameter> Parameters { get; }
        public int ActiveIndex { get; }
        public string? ErrorOrNull { get; }

        public bool HasError
        {
            get
            {
                if (ErrorOrNull != null)
                {
                    return true;
                }

                for (int index = 0; index < Parameters.Count; index++)
                {
                    if (Parameters[index].ErrorOrNull != null)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        internal CommandHint(CheatDefinition definition, HintParameter[] parameters, int activeIndex, string? errorOrNull)
        {
            Definition = definition;
            Parameters = parameters;
            ActiveIndex = activeIndex;
            ErrorOrNull = errorOrNull;
        }
    }

    public sealed class AssistResult
    {
        private static readonly Suggestion[] NO_SUGGESTIONS = new Suggestion[0];

        public string Line { get; }
        public int Caret { get; }
        public IReadOnlyList<Suggestion> Suggestions { get; }
        public int TokenIndex { get; }
        public int ReplaceStart { get; }
        public int ReplaceEnd { get; }
        public string Prefix { get; }
        public CommandHint? HintOrNull { get; }
        public string? UnknownCommandOrNull { get; }
        public string Placeholder { get; }
        public string? ProviderErrorOrNull { get; }

        public bool IsCaretAtEnd => Caret >= Line.Length;

        internal AssistResult(string line, int caret, IReadOnlyList<Suggestion>? suggestionsOrNull, TokenPosition position,
            CommandHint? hintOrNull, string? unknownCommandOrNull, string placeholder, string? providerErrorOrNull)
        {
            Line = line;
            Caret = caret;
            Suggestions = suggestionsOrNull ?? NO_SUGGESTIONS;
            TokenIndex = position.Index;
            ReplaceStart = position.ReplaceStart;
            ReplaceEnd = position.ReplaceEnd;
            Prefix = position.Prefix;
            HintOrNull = hintOrNull;
            UnknownCommandOrNull = unknownCommandOrNull;
            Placeholder = placeholder;
            ProviderErrorOrNull = providerErrorOrNull;
        }

        public string GetGhostText(int suggestionIndex)
        {
            if (!IsCaretAtEnd || suggestionIndex < 0 || suggestionIndex >= Suggestions.Count || Prefix.Length == 0)
            {
                return "";
            }

            string insert = Suggestions[suggestionIndex].Insert;
            if (insert.Length <= Prefix.Length || !insert.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) || CommandLine.Quote(insert) != insert)
            {
                return "";
            }

            return insert.Substring(Prefix.Length);
        }
    }
}
