using System;
using System.Collections.Generic;

namespace Lumera.ZRay
{
    public enum EConsoleKey
    {
        Complete,
        Submit,
        ForceSubmit,
        Up,
        Down,
        AcceptGhost,
        Dismiss
    }

    public sealed class ConsoleSession
    {
        private readonly ConsoleAssist _assist;
        private readonly CheatExecutor _executor;
        private readonly CommandHistory _history;
        private bool _bDismissed;

        public string Text { get; private set; } = "";
        public int Caret { get; private set; }
        public AssistResult Analysis { get; private set; }
        public int Selected { get; private set; } = -1;
        public string Notice { get; private set; } = "";
        public int Version { get; private set; }
        public int InputVersion { get; private set; }

        public CommandHistory History => _history;
        public IReadOnlyList<Suggestion> Suggestions => Analysis.Suggestions;
        public bool IsSuggestionListVisible => Analysis.Suggestions.Count > 0 && !_bDismissed && !_history.IsNavigating;

        public string GhostText => IsSuggestionListVisible ? Analysis.GetGhostText(Selected >= 0 ? Selected : 0) : "";

        public string TrailingHint
        {
            get
            {
                string ghost = GhostText;
                return ghost.Length > 0 || !Analysis.IsCaretAtEnd ? ghost : Analysis.Placeholder;
            }
        }

        public ConsoleSession(ConsoleAssist assist, CheatExecutor executor, CommandHistory history)
        {
            _assist = assist ?? throw new ArgumentNullException(nameof(assist));
            _executor = executor ?? throw new ArgumentNullException(nameof(executor));
            _history = history ?? throw new ArgumentNullException(nameof(history));
            Analysis = _assist.Analyze("", 0);
        }

        public void Begin()
        {
            Selected = -1;
            _bDismissed = false;
            Notice = "";
            Analyze();
        }

        public void OnTextEdited(string text, int caret)
        {
            _history.ResetNavigation();
            Text = text ?? "";
            Caret = Clamp(caret, Text.Length);
            Selected = -1;
            _bDismissed = false;
            Notice = "";
            Analyze();
        }

        public void OnCaretMoved(int caret)
        {
            int clamped = Clamp(caret, Text.Length);
            if (clamped == Caret)
            {
                return;
            }

            Caret = clamped;
            Selected = -1;
            Analyze();
        }

        public bool HandleKey(EConsoleKey key)
        {
            switch (key)
            {
                case EConsoleKey.Complete:
                    Accept(Selected >= 0 ? Selected : 0);
                    return true;
                case EConsoleKey.Submit:
                    Submit(false);
                    return true;
                case EConsoleKey.ForceSubmit:
                    Submit(true);
                    return true;
                case EConsoleKey.Up:
                    Move(-1);
                    return true;
                case EConsoleKey.Down:
                    Move(1);
                    return true;
                case EConsoleKey.AcceptGhost:
                    if (Caret >= Text.Length && GhostText.Length > 0)
                    {
                        Accept(Selected >= 0 ? Selected : 0);
                        return true;
                    }

                    return false;
                case EConsoleKey.Dismiss:
                    if (!IsSuggestionListVisible)
                    {
                        return false;
                    }

                    _bDismissed = true;
                    Selected = -1;
                    Version++;
                    return true;
                default:
                    return false;
            }
        }

        public void Accept(int index)
        {
            if (index < 0 || index >= Analysis.Suggestions.Count)
            {
                return;
            }

            string applied = _assist.ApplySuggestion(Analysis, Analysis.Suggestions[index], out int caret);
            _history.ResetNavigation();
            SetText(applied, caret);
        }

        public void Recall(string line)
        {
            _history.ResetNavigation();
            SetText(line ?? "", (line ?? "").Length);
        }

        private void Submit(bool bForce)
        {
            if (!bForce && Selected >= 0 && IsSuggestionListVisible)
            {
                Accept(Selected);
                return;
            }

            string line = Text;
            if (line.Trim().Length == 0)
            {
                return;
            }

            CheatParameter? missing = bForce ? null : _assist.FindMissingRequiredOrNull(line);
            if (missing != null)
            {
                string padded = line.EndsWith(" ", StringComparison.Ordinal) ? line : line + " ";
                _history.ResetNavigation();
                SetText(padded, padded.Length);
                Notice = $"{missing.DisplayName} 입력 필요";
                return;
            }

            _history.Add(line);
            SetText("", 0);
            _executor.Execute(line);
        }

        private void Move(int delta)
        {
            if (!_history.IsNavigating && IsSuggestionListVisible)
            {
                int count = Analysis.Suggestions.Count;
                Selected = ((Selected + 1 + delta + count + 1) % (count + 1)) - 1;
                Version++;
                return;
            }

            string line;
            bool bMoved = delta < 0 ? _history.TryNavigateOlder(Text, out line) : _history.TryNavigateNewer(out line);
            if (bMoved)
            {
                SetText(line, line.Length);
            }
        }

        private void SetText(string text, int caret)
        {
            Text = text;
            Caret = Clamp(caret, text.Length);
            Selected = -1;
            _bDismissed = false;
            Notice = "";
            InputVersion++;
            Analyze();
        }

        private void Analyze()
        {
            Analysis = _assist.Analyze(Text, Caret);
            Version++;
        }

        private static int Clamp(int caret, int length)
        {
            return caret < 0 ? 0 : (caret > length ? length : caret);
        }
    }
}
