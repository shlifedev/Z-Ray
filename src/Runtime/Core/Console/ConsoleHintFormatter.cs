using System.Collections.Generic;

namespace Lumera.ZRay
{
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
                first.Add(new StyledText($"기록 {history.NavigationNumber}/{history.Lines.Count}", ETextStyle.Accent));
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
