using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Lumera.DebuggerKit
{
    // 명령 한 줄을 실행한다. 콘솔과 메뉴가 모두 이 경로 하나로 실행한다.
    // 메뉴는 위젯의 값을 BuildLine으로 명령 줄로 만들어 넘긴다. 그래서 검증, 기본값, 결과 메시지가 어디서 실행하든 같고,
    // 메뉴에서 실행한 것도 콘솔 기록에 같은 명령 줄로 남는다.
    public sealed class CheatExecutor
    {
        private const int SIMILAR_COMMAND_COUNT = 3;

        private readonly CheatRegistry _registry;
        private readonly CommandHistory? _historyOrNull;
        private readonly CheatDraftStore? _draftsOrNull;

        // 줄이 비어 있지 않은 모든 실행 뒤에 불린다(실패 포함).
        public event Action<ExecutionResult>? Executed;

        // draftsOrNull: 실행한 인자 글자를 기억해 둘 곳. 메뉴의 입력칸이 마지막으로 실행한 값으로 시작하게 한다.
        public CheatExecutor(CheatRegistry registry, CommandHistory? historyOrNull = null, CheatDraftStore? draftsOrNull = null)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _historyOrNull = historyOrNull;
            _draftsOrNull = draftsOrNull;
        }

        // sourceLabel: 실행한 명령 줄 앞에 붙일 출처(예: "[메뉴] "). 콘솔에서 직접 친 줄은 비운다.
        public ExecutionResult Execute(string? line, string sourceLabel = "")
        {
            ExecutionResult result = new ExecutionResult();
            IReadOnlyList<CommandToken> tokens = CommandLine.Parse(line).Tokens;
            if (tokens.Count == 0)
            {
                return result;
            }

            result.Line = line!.Trim();
            string echo = $"{sourceLabel}> {result.Line}";
            CheatDefinition? definition = _registry.FindOrNull(tokens[0].Text);
            if (definition == null)
            {
                result.Add(echo, EConsoleLineKind.Echo);
                result.Add($"'{tokens[0].Text}' 명령이 없습니다.", EConsoleLineKind.Error);
                AddSimilarCommands(result, tokens[0].Text);
            }
            else if (TryBind(definition, tokens, out object?[] arguments, out string defaultsUsed, out string? bindError))
            {
                result.Add(defaultsUsed.Length == 0 ? echo : $"{echo}   (기본값: {defaultsUsed})", EConsoleLineKind.Echo);
                RememberArguments(definition, tokens);
                Run(definition, arguments, tokens.Count - 1, result);
            }
            else
            {
                result.Add(echo, EConsoleLineKind.Echo);
                result.Add(bindError!, EConsoleLineKind.Error);
            }

            Executed?.Invoke(result);
            return result;
        }

        // 인자가 올바르게 읽힌 실행만 기억한다. 값 치트는 지금 값이 곧 입력칸의 값이라 기억할 것이 없다.
        private void RememberArguments(CheatDefinition definition, IReadOnlyList<CommandToken> tokens)
        {
            if (_draftsOrNull == null || definition.Kind != ECheatKind.Action || tokens.Count <= 1)
            {
                return;
            }

            string[] texts = new string[tokens.Count - 1];
            for (int index = 0; index < texts.Length; index++)
            {
                texts[index] = tokens[index + 1].Text;
            }

            _draftsOrNull.Remember(definition, texts);
        }

        // 정의와 인자 글자로 명령 줄을 만든다. 공백이 든 값은 따옴표로 감싼다.
        public static string BuildLine(CheatDefinition definition, IReadOnlyList<string> argumentTexts)
        {
            StringBuilder builder = new StringBuilder(definition.Id);
            for (int index = 0; index < argumentTexts.Count; index++)
            {
                builder.Append(' ');
                builder.Append(CommandLine.Quote(argumentTexts[index]));
            }

            return builder.ToString();
        }

        // 인자는 위치 순서로 꽂는다. 빠진 인자는 기본값으로 채우고, 채운 기본값을 defaultsUsed에 적는다.
        private static bool TryBind(CheatDefinition definition, IReadOnlyList<CommandToken> tokens, out object?[] arguments, out string defaultsUsed, out string? errorOrNull)
        {
            IReadOnlyList<CheatParameter> parameters = definition.Parameters;
            int argumentCount = tokens.Count - 1;
            arguments = new object?[parameters.Count];
            defaultsUsed = "";
            errorOrNull = null;
            if (argumentCount > parameters.Count)
            {
                errorOrNull = parameters.Count == 0
                    ? "이 명령은 인자를 받지 않습니다."
                    : $"인자가 너무 많습니다. 사용법: {CheatFormatter.FormatSignature(definition)}";
                return false;
            }

            StringBuilder defaults = new StringBuilder();
            for (int index = 0; index < parameters.Count; index++)
            {
                CheatParameter parameter = parameters[index];
                if (index < argumentCount)
                {
                    if (!parameter.TryParse(tokens[index + 1].Text, out arguments[index], out string? parseError))
                    {
                        errorOrNull = $"{parameter.DisplayName}({parameter.Name}): {parseError}";
                        return false;
                    }
                }
                else if (parameter.HasDefault)
                {
                    arguments[index] = parameter.DefaultValue;
                    if (defaults.Length > 0)
                    {
                        defaults.Append(", ");
                    }

                    defaults.Append(parameter.Name).Append('=').Append(CommandLine.Quote(parameter.FormatDefault()));
                }
                else if (parameter.IsRequired)
                {
                    errorOrNull = $"'{parameter.DisplayName}' 인자가 필요합니다. 사용법: {CheatFormatter.FormatSignature(definition)}";
                    return false;
                }
            }

            defaultsUsed = defaults.ToString();
            return true;
        }

        private void Run(CheatDefinition definition, object?[] arguments, int argumentCount, ExecutionResult result)
        {
            result.DefinitionOrNull = definition;
            try
            {
                CheatResult cheatResult = definition.Kind == ECheatKind.Action
                    ? definition.Invoke(arguments)
                    : RunValue(definition, arguments, argumentCount, result);
                Report(definition, cheatResult, result);
            }
            catch (Exception exception)
            {
                // 리플렉션 호출이 감싼 예외를 풀어 실제 원인을 보인다.
                Exception cause = exception is TargetInvocationException && exception.InnerException != null ? exception.InnerException : exception;
                if (cause is CheatException)
                {
                    // 치트가 일부러 알린 실패다. 메시지만 보인다.
                    result.Add(cause.Message, EConsoleLineKind.Error);
                }
                else
                {
                    result.ExceptionOrNull = cause;
                    result.Add($"{cause.GetType().Name}: {cause.Message}", EConsoleLineKind.Error);
                }
            }

            // 콘솔 전용 명령(help, clear)은 최근 사용에 넣지 않는다.
            if (definition.IsShownInMenu)
            {
                _historyOrNull?.RecordCheatUse(definition.Id);
            }
        }

        // 인자가 없으면 읽고, 있으면 쓴 뒤 다시 읽어 실제로 들어간 값을 보인다(setter가 값을 고칠 수 있다).
        private static CheatResult RunValue(CheatDefinition definition, object?[] arguments, int argumentCount, ExecutionResult result)
        {
            if (argumentCount == 0)
            {
                result.Add($"{definition.Id} = {definition.FormatValue()}", EConsoleLineKind.Info);
                return CheatResult.Ok("");
            }

            if (!definition.CanWrite)
            {
                return CheatResult.Fail($"{definition.Id}는 읽기 전용입니다.");
            }

            definition.WriteValue(arguments[0]);
            return CheatResult.Ok($"{definition.Id} = {definition.FormatValue()}");
        }

        private static void Report(CheatDefinition definition, CheatResult cheatResult, ExecutionResult result)
        {
            result.IsSuccess = cheatResult.IsSuccess;
            if (cheatResult.IsClearRequested)
            {
                result.IsCleared = true;
                return;
            }

            if (cheatResult.Message.Length > 0)
            {
                result.AddLines(cheatResult.Message, cheatResult.IsSuccess ? EConsoleLineKind.Success : EConsoleLineKind.Error);
            }
            else if (!cheatResult.IsSuccess)
            {
                result.Add($"{definition.Name} 실패", EConsoleLineKind.Error);
            }
            else if (definition.Kind == ECheatKind.Action)
            {
                // 메시지를 돌려주지 않는 치트도 실행됐다는 것은 보인다.
                result.Add($"{definition.Name} 완료", EConsoleLineKind.Success);
            }
        }

        private void AddSimilarCommands(ExecutionResult result, string query)
        {
            IReadOnlyList<CheatDefinition> definitions = _registry.Definitions;
            List<KeyValuePair<int, string>> ranked = new List<KeyValuePair<int, string>>();
            for (int index = 0; index < definitions.Count; index++)
            {
                int rank = CheatSearch.Rank(definitions[index], query);
                if (rank != CheatSearch.NO_MATCH)
                {
                    ranked.Add(new KeyValuePair<int, string>(rank, definitions[index].Id));
                }
            }

            if (ranked.Count == 0)
            {
                return;
            }

            ranked.Sort(static (left, right) => left.Key != right.Key ? left.Key.CompareTo(right.Key) : string.CompareOrdinal(left.Value, right.Value));
            StringBuilder builder = new StringBuilder("비슷한 명령: ");
            for (int index = 0; index < ranked.Count && index < SIMILAR_COMMAND_COUNT; index++)
            {
                if (index > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(ranked[index].Value);
            }

            result.Add(builder.ToString(), EConsoleLineKind.Info);
        }
    }
}
