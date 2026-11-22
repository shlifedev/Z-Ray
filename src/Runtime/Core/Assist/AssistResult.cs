using System;
using System.Collections.Generic;

namespace Lumera.ZRay
{
    public enum ESuggestionKind
    {
        Action,
        Value,
        // 인자 자리에 넣을 값
        Argument
    }

    // 자동완성 후보 한 줄.
    public sealed class Suggestion
    {
        private static readonly MatchSpan[] NO_SPANS = new MatchSpan[0];

        // 입력 줄에 들어갈 값(따옴표를 붙이기 전)
        public string Insert { get; }
        // Insert 옆에 보이는 설명(치트 이름, 후보 설명)
        public string Detail { get; }
        // 오른쪽 끝에 보이는 짧은 표시(분류, "기본값")
        public string Note { get; }
        public ESuggestionKind Kind { get; }
        // 작을수록 위. 일치 방식과 일치한 칸으로 정한다.
        public int Rank { get; }
        // Rank가 같을 때의 순서(선언 순서, 공급자가 준 순서)
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

    // 힌트 바에 보이는 인자 하나.
    public readonly struct HintParameter
    {
        public CheatParameter Parameter { get; }
        // 커서가 이 인자를 입력하는 중이다.
        public bool IsActive { get; }
        // 이미 입력한 값이 틀렸을 때의 이유
        public string? ErrorOrNull { get; }

        internal HintParameter(CheatParameter parameter, bool bActive, string? errorOrNull)
        {
            Parameter = parameter;
            IsActive = bActive;
            ErrorOrNull = errorOrNull;
        }
    }

    // 지금 입력한 명령의 시그니처와 검증 결과.
    public sealed class CommandHint
    {
        public CheatDefinition Definition { get; }
        public IReadOnlyList<HintParameter> Parameters { get; }
        // 커서가 있는 인자 번호. 명령 이름 위에 있거나 인자가 넘쳤으면 -1이다.
        public int ActiveIndex { get; }
        // 인자 하나에 붙일 수 없는 오류(인자가 너무 많음)
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

    // 입력 줄과 커서 위치 하나에 대한 콘솔 보조 정보 전부. 화면은 키 입력마다 이것 하나를 받아 그린다.
    public sealed class AssistResult
    {
        private static readonly Suggestion[] NO_SUGGESTIONS = new Suggestion[0];

        public string Line { get; }
        public int Caret { get; }
        public IReadOnlyList<Suggestion> Suggestions { get; }
        // 커서가 있는 토큰의 번호. 0이면 명령 이름, 1부터는 인자다.
        public int TokenIndex { get; }
        // 후보를 고르면 바뀌는 원문 범위(커서가 있는 토큰 전체, 따옴표 포함)
        public int ReplaceStart { get; }
        public int ReplaceEnd { get; }
        // 커서가 있는 토큰에서 커서 앞까지의 값
        public string Prefix { get; }
        public CommandHint? HintOrNull { get; }
        // 명령 이름이 등록되지 않았을 때의 안내. 이때 HintOrNull은 null이다.
        public string? UnknownCommandOrNull { get; }
        // 커서 뒤에 흐리게 보일 남은 인자 자리(예: "<amount:int> [reason=cheat]"). 커서가 줄 끝일 때만 채운다.
        public string Placeholder { get; }
        // 후보 공급자가 예외를 던졌을 때의 안내. 이때 그 인자의 후보는 비어 있다.
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

        // 후보를 받아들였을 때 커서 뒤에 이어질 글자(회색 완성). 커서가 줄 끝이고 후보가 지금 입력으로 시작할 때만 있다.
        public string GetGhostText(int suggestionIndex)
        {
            if (!IsCaretAtEnd || suggestionIndex < 0 || suggestionIndex >= Suggestions.Count || Prefix.Length == 0)
            {
                return "";
            }

            string insert = Suggestions[suggestionIndex].Insert;
            // 따옴표가 필요한 값은 이어 붙이는 것만으로 완성되지 않아서 회색 완성을 보이지 않는다.
            if (insert.Length <= Prefix.Length || !insert.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) || CommandLine.Quote(insert) != insert)
            {
                return "";
            }

            return insert.Substring(Prefix.Length);
        }
    }
}
