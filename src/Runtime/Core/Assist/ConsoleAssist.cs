using System;
using System.Collections.Generic;

namespace Lumera.DebuggerKit
{
    // 입력 줄과 커서 위치를 받아 자동완성 후보, 힌트, 인자 자리 안내를 만든다. 화면을 모르는 계산만 한다.
    // 화면은 입력이 바뀔 때마다 Analyze를 한 번 부르고, 후보를 고르면 ApplySuggestion으로 새 입력 줄을 받는다.
    public sealed class ConsoleAssist
    {
        public const int MAX_SUGGESTIONS = 50;

        // 최근에 쓴 치트를 같은 일치 방식 안에서 위로 올리는 점수. "처음부터 일치"와 "조각 처음부터 일치"의 차이(20)를 넘지 않게 둔다.
        private const int RECENT_BONUS_MAX = 15;
        private const int RECENT_BONUS_STEP = 3;
        private const string DEFAULT_NOTE = "기본값";
        private const string CURRENT_NOTE = "현재 값";

        private static readonly Comparison<Suggestion> SUGGESTION_ORDER = CompareSuggestions;
        private static readonly string[] NO_ARGS = new string[0];

        private readonly CheatRegistry _registry;
        private readonly CommandHistory? _historyOrNull;
        private readonly List<CandidateValue> _candidates = new List<CandidateValue>();
        private readonly List<string> _typeCandidates = new List<string>();

        public ConsoleAssist(CheatRegistry registry, CommandHistory? historyOrNull = null)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _historyOrNull = historyOrNull;
        }

        public AssistResult Analyze(string? line, int caret)
        {
            CommandLine commandLine = CommandLine.Parse(line);
            string text = commandLine.Text;
            int clampedCaret = caret < 0 ? 0 : (caret > text.Length ? text.Length : caret);
            TokenPosition position = commandLine.FindTokenAt(clampedCaret);
            IReadOnlyList<CommandToken> tokens = commandLine.Tokens;
            // 빈 줄에서는 후보를 띄우지 않는다. 빈 줄의 ↑/↓는 기록 탐색이다.
            if (tokens.Count == 0)
            {
                return new AssistResult(text, clampedCaret, null, position, null, null, "", null);
            }

            bool bCaretAtEnd = clampedCaret >= text.Length;
            CheatDefinition? definition = _registry.FindOrNull(tokens[0].Text);
            if (position.Index == 0)
            {
                List<Suggestion> commands = SuggestCommands(position.Prefix);
                CommandHint? commandHint = definition == null ? null : BuildHint(definition, tokens, -1);
                return new AssistResult(text, clampedCaret, commands, position, commandHint, null,
                    BuildPlaceholder(definition, tokens, position, bCaretAtEnd), null);
            }

            if (definition == null)
            {
                return new AssistResult(text, clampedCaret, null, position, null, $"'{tokens[0].Text}' 명령이 없습니다.", "", null);
            }

            int argumentIndex = position.Index - 1;
            bool bInRange = argumentIndex < definition.Parameters.Count;
            string? providerError = null;
            List<Suggestion>? arguments = bInRange
                ? SuggestArguments(definition, definition.Parameters[argumentIndex], position.Prefix, CollectPreviousArgs(tokens, argumentIndex), out providerError)
                : null;
            CommandHint hint = BuildHint(definition, tokens, bInRange ? argumentIndex : -1);
            return new AssistResult(text, clampedCaret, arguments, position, hint, null,
                BuildPlaceholder(definition, tokens, position, bCaretAtEnd), providerError);
        }

        // 후보를 입력 줄에 넣는다. 커서가 있는 토큰 전체를 바꾸고, 공백이 든 값은 따옴표로 감싼다.
        // 이어서 입력할 인자가 있으면 공백을 하나 두고 그 뒤로 커서를 옮긴다.
        public string ApplySuggestion(AssistResult result, Suggestion suggestion, out int caret)
        {
            string before = result.Line.Substring(0, result.ReplaceStart);
            string after = result.Line.Substring(result.ReplaceEnd);
            string applied = before + CommandLine.Quote(suggestion.Insert);
            caret = applied.Length;
            if (ExpectsMoreArguments(result, suggestion))
            {
                if (after.Length == 0 || !char.IsWhiteSpace(after[0]))
                {
                    applied += " ";
                }

                caret++;
            }

            return applied + after;
        }

        // 아직 입력하지 않은 필수 인자 중 첫 번째. 없거나 명령을 모르면 null이다.
        // Enter를 눌렀을 때 실행할지, 다음 인자를 입력하게 할지 정하는 데 쓴다.
        public CheatParameter? FindMissingRequiredOrNull(string? line)
        {
            IReadOnlyList<CommandToken> tokens = CommandLine.Parse(line).Tokens;
            CheatDefinition? definition = tokens.Count == 0 ? null : _registry.FindOrNull(tokens[0].Text);
            if (definition == null)
            {
                return null;
            }

            for (int index = tokens.Count - 1; index < definition.Parameters.Count; index++)
            {
                if (definition.Parameters[index].IsRequired)
                {
                    return definition.Parameters[index];
                }
            }

            return null;
        }

        // 명령 이름 후보. 등록된 모든 치트를 검색어로 순위 매긴다.
        public List<Suggestion> SuggestCommands(string query)
        {
            List<Suggestion> suggestions = new List<Suggestion>();
            IReadOnlyList<CheatDefinition> definitions = _registry.Definitions;
            for (int index = 0; index < definitions.Count; index++)
            {
                CheatDefinition definition = definitions[index];
                int rank = CheatSearch.Rank(definition, query);
                if (rank == CheatSearch.NO_MATCH)
                {
                    continue;
                }

                string detail = definition.Name == definition.Id ? "" : definition.Name;
                suggestions.Add(new Suggestion(definition.Id, detail, definition.Category,
                    definition.Kind == ECheatKind.Action ? ESuggestionKind.Action : ESuggestionKind.Value,
                    rank - GetRecentBonus(definition.Id), 0,
                    TextMatcher.FindSpans(definition.Id, query), TextMatcher.FindSpans(detail, query)));
            }

            return Finish(suggestions);
        }

        // 인자 하나의 후보. 메뉴의 입력칸이 콘솔과 같은 후보를 띄울 때 쓴다.
        public List<Suggestion> SuggestArguments(CheatDefinition definition, int parameterIndex, string prefix, IReadOnlyList<string> previousArgs)
        {
            return SuggestArguments(definition, definition.Parameters[parameterIndex], prefix, previousArgs, out _);
        }

        private List<Suggestion> SuggestArguments(CheatDefinition definition, CheatParameter parameter, string prefix, IReadOnlyList<string> previousArgs, out string? providerErrorOrNull)
        {
            providerErrorOrNull = null;
            _candidates.Clear();
            if (parameter.SuggestionProviderOrNull != null)
            {
                try
                {
                    parameter.SuggestionProviderOrNull.AddCandidates(new SuggestionContext(definition, parameter, prefix, previousArgs), _candidates);
                }
                catch (Exception exception)
                {
                    _candidates.Clear();
                    providerErrorOrNull = $"후보를 만들지 못했습니다: {exception.Message}";
                }
            }
            else
            {
                _typeCandidates.Clear();
                parameter.ValueType.AddCandidates(_typeCandidates);
                for (int index = 0; index < _typeCandidates.Count; index++)
                {
                    _candidates.Add(new CandidateValue(_typeCandidates[index]));
                }
            }

            List<Suggestion> suggestions = new List<Suggestion>();
            // 기본값과 현재 값을 먼저 넣어 같은 값의 후보보다 앞서게 한다(중복은 Finish가 앞의 것만 남긴다).
            if (parameter.HasDefault)
            {
                AddArgument(suggestions, parameter.FormatDefault(), "", DEFAULT_NOTE, prefix, -2);
            }

            if (definition.Kind == ECheatKind.Value)
            {
                AddArgument(suggestions, ReadCurrentValueOrEmpty(definition), "", CURRENT_NOTE, prefix, -1);
            }

            for (int index = 0; index < _candidates.Count; index++)
            {
                AddArgument(suggestions, _candidates[index].Value, _candidates[index].Description, "", prefix, index);
            }

            return Finish(suggestions);
        }

        private static void AddArgument(List<Suggestion> suggestions, string value, string description, string note, string prefix, int order)
        {
            if (value.Length == 0)
            {
                return;
            }

            int rank = CheatSearch.RankValue(value, description, prefix);
            if (rank == CheatSearch.NO_MATCH)
            {
                return;
            }

            suggestions.Add(new Suggestion(value, description, note, ESuggestionKind.Argument, rank, order,
                TextMatcher.FindSpans(value, prefix), TextMatcher.FindSpans(description, prefix)));
        }

        // 값을 읽다가 난 예외는 삼킨다. 자동완성 때문에 입력이 막히면 안 된다(실행할 때는 오류로 보인다).
        private static string ReadCurrentValueOrEmpty(CheatDefinition definition)
        {
            try
            {
                return definition.FormatValue();
            }
            catch (Exception)
            {
                return "";
            }
        }

        private static CommandHint BuildHint(CheatDefinition definition, IReadOnlyList<CommandToken> tokens, int activeIndex)
        {
            IReadOnlyList<CheatParameter> parameters = definition.Parameters;
            HintParameter[] hints = new HintParameter[parameters.Count];
            for (int index = 0; index < parameters.Count; index++)
            {
                string? error = null;
                if (index + 1 < tokens.Count)
                {
                    parameters[index].TryParse(tokens[index + 1].Text, out _, out error);
                }

                hints[index] = new HintParameter(parameters[index], index == activeIndex, error);
            }

            int argumentCount = tokens.Count - 1;
            string? lineError = null;
            if (argumentCount > parameters.Count)
            {
                lineError = parameters.Count == 0 ? "이 명령은 인자를 받지 않습니다." : $"인자가 너무 많습니다. 최대 {parameters.Count}개입니다.";
            }
            else if (definition.Kind == ECheatKind.Value && !definition.CanWrite && argumentCount > 0)
            {
                lineError = "읽기 전용 값입니다.";
            }

            return new CommandHint(definition, hints, activeIndex, lineError);
        }

        // 커서가 줄 끝일 때, 아직 입력하지 않은 인자 자리를 흐린 글자로 안내한다.
        private static string BuildPlaceholder(CheatDefinition? definitionOrNull, IReadOnlyList<CommandToken> tokens, TokenPosition position, bool bCaretAtEnd)
        {
            if (definitionOrNull == null || !bCaretAtEnd)
            {
                return "";
            }

            string remaining = CheatFormatter.FormatRemainingParameters(definitionOrNull, tokens.Count - 1);
            // 커서가 토큰 끝에 붙어 있으면 한 칸 띄운다. 새 토큰 자리(공백 뒤)면 바로 이어 쓴다.
            bool bOnNewToken = position.Index >= tokens.Count;
            return remaining.Length == 0 || bOnNewToken ? remaining : " " + remaining;
        }

        private bool ExpectsMoreArguments(AssistResult result, Suggestion suggestion)
        {
            if (result.TokenIndex == 0)
            {
                CheatDefinition? definition = _registry.FindOrNull(suggestion.Insert);
                if (definition == null)
                {
                    return false;
                }

                // 읽기 전용 값은 이름만으로 완성된다.
                return definition.Kind == ECheatKind.Value ? definition.CanWrite : definition.HasParameters;
            }

            return result.HintOrNull != null && result.TokenIndex < result.HintOrNull.Definition.Parameters.Count;
        }

        private int GetRecentBonus(string id)
        {
            if (_historyOrNull == null)
            {
                return 0;
            }

            IReadOnlyList<string> recent = _historyOrNull.RecentCheatIds;
            for (int index = 0; index < recent.Count; index++)
            {
                if (string.Equals(recent[index], id, StringComparison.OrdinalIgnoreCase))
                {
                    int bonus = RECENT_BONUS_MAX - (index * RECENT_BONUS_STEP);
                    return bonus > 0 ? bonus : 0;
                }
            }

            return 0;
        }

        private static IReadOnlyList<string> CollectPreviousArgs(IReadOnlyList<CommandToken> tokens, int argumentIndex)
        {
            if (argumentIndex <= 0)
            {
                return NO_ARGS;
            }

            string[] previous = new string[argumentIndex];
            for (int index = 0; index < argumentIndex; index++)
            {
                previous[index] = tokens[index + 1].Text;
            }

            return previous;
        }

        // 정렬하고, 같은 값은 앞선 것만 남기고, 개수를 자른다.
        private static List<Suggestion> Finish(List<Suggestion> suggestions)
        {
            suggestions.Sort(SUGGESTION_ORDER);
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int kept = 0;
            for (int index = 0; index < suggestions.Count && kept < MAX_SUGGESTIONS; index++)
            {
                if (seen.Add(suggestions[index].Insert))
                {
                    suggestions[kept] = suggestions[index];
                    kept++;
                }
            }

            suggestions.RemoveRange(kept, suggestions.Count - kept);
            return suggestions;
        }

        // 일치 점수가 먼저다. 점수가 같으면 선언·공급 순서, 그다음 글자 순이다.
        private static int CompareSuggestions(Suggestion left, Suggestion right)
        {
            int rank = left.Rank.CompareTo(right.Rank);
            if (rank != 0)
            {
                return rank;
            }

            int order = left.Order.CompareTo(right.Order);
            return order != 0 ? order : StringComparer.OrdinalIgnoreCase.Compare(left.Insert, right.Insert);
        }
    }
}
