using System;
using System.Collections.Generic;
using System.Text;

namespace Lumera.DebuggerKit
{
    // 콘솔 자체의 명령(help, clear, history). 다른 치트와 같은 방식으로 선언하고 등록한다. 메뉴에는 보이지 않는다.
    public sealed class BuiltInCheats
    {
        private const string CATEGORY = "콘솔";
        private const int HISTORY_SHOWN = 20;

        private readonly CheatRegistry _registry;
        private readonly CommandHistory? _historyOrNull;

        private BuiltInCheats(CheatRegistry registry, CommandHistory? historyOrNull)
        {
            _registry = registry;
            _historyOrNull = historyOrNull;
        }

        public static IDisposable Register(CheatRegistry registry, CommandHistory? historyOrNull = null)
        {
            return registry.Register(new BuiltInCheats(registry, historyOrNull));
        }

        [Cheat("help", Category = CATEGORY, Name = "도움말", Description = "명령 목록 또는 명령 하나의 사용법을 보여 준다.", IsShownInMenu = false)]
        private CheatResult Help([Arg("명령", Description = "사용법을 볼 명령. 비우면 전체 목록."), Suggest(typeof(CheatIdSuggestions))] string id = "")
        {
            if (id.Length == 0)
            {
                return CheatResult.Ok(FormatList());
            }

            CheatDefinition? definition = _registry.FindOrNull(id);
            return definition == null ? CheatResult.Fail($"'{id}' 명령이 없습니다.") : CheatResult.Ok(FormatDetail(definition));
        }

        [Cheat("clear", Category = CATEGORY, Name = "기록 비우기", Description = "콘솔 기록을 비운다.", IsShownInMenu = false)]
        private CheatResult Clear()
        {
            return CheatResult.Clear();
        }

        [Cheat("history", Category = CATEGORY, Name = "입력 기록", Description = "최근에 입력한 명령을 보여 준다.", IsShownInMenu = false)]
        private CheatResult History()
        {
            if (_historyOrNull == null || _historyOrNull.Lines.Count == 0)
            {
                return CheatResult.Ok("입력 기록이 없습니다.");
            }

            IReadOnlyList<string> lines = _historyOrNull.Lines;
            StringBuilder builder = new StringBuilder();
            for (int index = Math.Max(0, lines.Count - HISTORY_SHOWN); index < lines.Count; index++)
            {
                if (builder.Length > 0)
                {
                    builder.Append('\n');
                }

                builder.Append("  ").Append(lines[index]);
            }

            return CheatResult.Ok(builder.ToString());
        }

        [Cheat("props", Category = CATEGORY, Name = "프로퍼티 목록", Description = "등록된 치트 프로퍼티와 지금 값을 보여 준다.", IsShownInMenu = false)]
        private CheatResult Properties([Arg("검색어", Description = "id나 이름으로 거른다. 비우면 전체.")] string filter = "")
        {
            string query = (filter ?? "").Trim();
            IReadOnlyList<CheatDefinition> definitions = _registry.Definitions;
            StringBuilder builder = new StringBuilder();
            int count = 0;
            for (int index = 0; index < definitions.Count; index++)
            {
                CheatDefinition definition = definitions[index];
                if (definition.Kind != ECheatKind.Value)
                {
                    continue;
                }

                if (query.Length > 0 && CheatSearch.Rank(definition, query) == CheatSearch.NO_MATCH)
                {
                    continue;
                }

                count++;
                string value = ReadPropertyValueSafe(definition);
                builder.Append("\n  ").Append(definition.Id).Append(" = ").Append(value);
                if (definition.Name != definition.Id)
                {
                    builder.Append("  — ").Append(definition.Name);
                }

                if (!definition.CanWrite)
                {
                    builder.Append("  [읽기 전용]");
                }
            }

            if (count == 0)
            {
                return CheatResult.Ok(query.Length == 0 ? "등록된 치트 프로퍼티가 없습니다." : $"'{query}'와 일치하는 치트 프로퍼티가 없습니다.");
            }

            return CheatResult.Ok($"프로퍼티 {count}개{builder}");
        }

        private static string ReadPropertyValueSafe(CheatDefinition definition)
        {
            try
            {
                return definition.FormatValue();
            }
            catch (Exception exception)
            {
                return $"<오류: {exception.Message}>";
            }
        }

        private string FormatList()
        {
            IReadOnlyList<CheatDefinition> definitions = _registry.Definitions;
            StringBuilder builder = new StringBuilder();
            builder.Append("명령 ").Append(definitions.Count).Append("개. 사용법은 help <명령>");
            string category = "";
            for (int index = 0; index < definitions.Count; index++)
            {
                CheatDefinition definition = definitions[index];
                if (definition.Category != category)
                {
                    category = definition.Category;
                    builder.Append("\n[").Append(category).Append(']');
                }

                builder.Append("\n  ").Append(CheatFormatter.FormatSignature(definition));
                if (definition.Name != definition.Id)
                {
                    builder.Append("  — ").Append(definition.Name);
                }
            }

            return builder.ToString();
        }

        private static string FormatDetail(CheatDefinition definition)
        {
            StringBuilder builder = new StringBuilder(CheatFormatter.FormatSignature(definition));
            builder.Append("\n  ").Append(definition.Name).Append("  [").Append(definition.Category).Append(']');
            if (definition.Description.Length > 0)
            {
                builder.Append("\n  ").Append(definition.Description);
            }

            if (definition.Kind == ECheatKind.Value)
            {
                builder.Append(definition.CanWrite ? "\n  값을 주면 쓰고, 생략하면 읽는다." : "\n  읽기 전용 값이다.");
            }

            for (int index = 0; index < definition.Parameters.Count; index++)
            {
                CheatParameter parameter = definition.Parameters[index];
                builder.Append("\n  ").Append(parameter.Name);
                if (parameter.DisplayName != parameter.Name)
                {
                    builder.Append(" (").Append(parameter.DisplayName).Append(')');
                }

                if (parameter.Description.Length > 0)
                {
                    builder.Append(": ").Append(parameter.Description);
                }
            }

            return builder.ToString();
        }

        // help의 인자 후보: 등록된 모든 명령.
        private sealed class CheatIdSuggestions : ISuggestionProvider
        {
            public void AddCandidates(SuggestionContext context, List<CandidateValue> output)
            {
                if (!(context.TargetOrNull is BuiltInCheats builtIn))
                {
                    return;
                }

                IReadOnlyList<CheatDefinition> definitions = builtIn._registry.Definitions;
                for (int index = 0; index < definitions.Count; index++)
                {
                    output.Add(new CandidateValue(definitions[index].Id, definitions[index].Name));
                }
            }
        }
    }
}
