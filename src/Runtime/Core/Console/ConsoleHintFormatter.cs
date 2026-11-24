using System.Collections.Generic;

namespace Lumera.ZRay
{
    // 콘솔 입력 줄 아래의 힌트 두 줄을 만든다. 첫 줄은 시그니처(입력 중인 인자를 강조, 틀린 인자는 빨강),
    // 둘째 줄은 지금 가장 필요한 설명 하나다. 화면은 이 조각을 자기 색으로 그린다.
    // 빈 줄처럼 보여 줄 힌트가 없으면 두 목록을 비워 둔다(화면이 힌트 영역을 숨긴다).
    public static class ConsoleHintFormatter
    {
        public static void Build(ConsoleSession session, CheatRegistry registry, List<StyledText> first, List<StyledText> second)
        {
            first.Clear();
            second.Clear();
            AssistResult analysis = session.Analysis;
            CommandHistory history = session.History;
            CommandHint? hint = analysis.HintOrNull;
            if (history.IsNavigating)
            {
                first.Add(new StyledText($"기록 {history.NavigationNumber}/{history.NavigationCount}", ETextStyle.Accent));
                second.Add(new StyledText("↑↓ 이동 · 입력하면 탐색을 끝낸다", ETextStyle.Muted));
            }
            else if (analysis.UnknownCommandOrNull != null)
            {
                first.Add(new StyledText(analysis.UnknownCommandOrNull, ETextStyle.Error));
                second.Add(new StyledText("help로 명령 목록을 본다", ETextStyle.Muted));
            }
            else if (hint != null)
            {
                AppendSignature(hint.Definition, hint, first);
                AppendDetail(session, hint, second);
            }
            else
            {
                TryAppendPreview(session, registry, first, second);
            }
        }

        private static void AppendSignature(CheatDefinition definition, CommandHint? hintOrNull, List<StyledText> output)
        {
            output.Add(new StyledText(definition.Id, hintOrNull == null ? ETextStyle.Muted : ETextStyle.Normal));
            for (int index = 0; index < definition.Parameters.Count; index++)
            {
                ETextStyle style = ETextStyle.Muted;
                if (hintOrNull != null && hintOrNull.Parameters[index].ErrorOrNull != null)
                {
                    style = ETextStyle.Error;
                }
                else if (hintOrNull != null && hintOrNull.Parameters[index].IsActive)
                {
                    style = ETextStyle.Accent;
                }

                output.Add(new StyledText(" ", ETextStyle.Muted));
                output.Add(new StyledText(CheatFormatter.FormatParameter(definition.Parameters[index], true), style));
            }
        }

        private static void AppendDetail(ConsoleSession session, CommandHint hint, List<StyledText> output)
        {
            if (session.Notice.Length > 0)
            {
                output.Add(new StyledText(session.Notice, ETextStyle.Accent));
                return;
            }

            for (int index = 0; index < hint.Parameters.Count; index++)
            {
                HintParameter parameter = hint.Parameters[index];
                if (parameter.ErrorOrNull != null)
                {
                    output.Add(new StyledText($"{parameter.Parameter.DisplayName}: {parameter.ErrorOrNull}", ETextStyle.Error));
                    return;
                }
            }

            string? lineError = hint.ErrorOrNull ?? session.Analysis.ProviderErrorOrNull;
            if (lineError != null)
            {
                output.Add(new StyledText(lineError, ETextStyle.Error));
                return;
            }

            if (hint.ActiveIndex >= 0)
            {
                CheatParameter active = hint.Parameters[hint.ActiveIndex].Parameter;
                output.Add(new StyledText(active.DisplayName, ETextStyle.Accent));
                if (active.Description.Length > 0)
                {
                    output.Add(new StyledText($" — {active.Description}", ETextStyle.Muted));
                }

                return;
            }

            AppendDescription(hint.Definition, output);
        }

        // 명령 이름을 치는 중이면 Tab으로 들어갈 후보의 시그니처를 흐리게 미리 보인다.
        private static bool TryAppendPreview(ConsoleSession session, CheatRegistry registry, List<StyledText> first, List<StyledText> second)
        {
            AssistResult analysis = session.Analysis;
            int previewIndex = session.Selected >= 0 ? session.Selected : 0;
            if (analysis.TokenIndex != 0 || !session.IsSuggestionListVisible || previewIndex >= analysis.Suggestions.Count)
            {
                return false;
            }

            CheatDefinition? preview = registry.FindOrNull(analysis.Suggestions[previewIndex].Insert);
            if (preview == null)
            {
                return false;
            }

            AppendSignature(preview, null, first);
            AppendDescription(preview, second);
            return true;
        }

        private static void AppendDescription(CheatDefinition definition, List<StyledText> output)
        {
            output.Add(new StyledText(definition.Name, ETextStyle.Normal));
            if (definition.Description.Length > 0)
            {
                output.Add(new StyledText($" — {definition.Description}", ETextStyle.Muted));
            }
        }
    }
}
