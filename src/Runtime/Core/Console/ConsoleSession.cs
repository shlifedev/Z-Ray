using System;
using System.Collections.Generic;

namespace Lumera.DebuggerKit
{
    // 콘솔이 따로 쓰는 키. 화면이 자기 입력 방식(uGUI, UI Toolkit)으로 읽어 이 값으로 넘긴다.
    public enum EConsoleKey
    {
        // Tab: 후보를 넣고 다음 인자로
        Complete,
        // Enter: 고른 후보를 넣거나, 필수 인자가 남았으면 다음 인자로, 아니면 실행
        Submit,
        // Ctrl+Enter: 바로 실행
        ForceSubmit,
        Up,
        Down,
        // →: 커서가 줄 끝이면 회색 완성을 받는다
        AcceptGhost,
        // Esc: 후보 목록을 닫는다
        Dismiss
    }

    // 콘솔 입력 줄 하나의 상태(글자, 커서, 고른 후보, 기록 탐색)와 키에 따른 동작.
    // 화면을 모른다. 런타임 콘솔과 에디터 창이 각자 하나씩 갖고, 입력칸의 값을 넘겨 주고 결과를 그린다.
    //
    // 후보 목록이 떠 있으면 ↑/↓는 후보를 고른다. 입력 줄까지 포함해 한 바퀴 돈다(입력 줄에서 ↓는 첫 후보, ↑는 맨 아래 후보).
    // 후보 목록이 없을 때(빈 줄, Esc로 닫은 뒤, 일치하는 후보가 없을 때)의 ↑는 기록 탐색이다. 기록을 보는 동안에는 ↑/↓가 모두 기록을 넘긴다.
    public sealed class ConsoleSession
    {
        private readonly ConsoleAssist _assist;
        private readonly CheatExecutor _executor;
        private readonly CommandHistory _history;
        // Esc로 후보 목록을 닫았다. 입력이 바뀔 때까지 다시 띄우지 않는다.
        private bool _bDismissed;

        public string Text { get; private set; } = "";
        public int Caret { get; private set; }
        public AssistResult Analysis { get; private set; }
        // 방향키로 고른 후보. -1이면 입력 줄에 있다.
        public int Selected { get; private set; } = -1;
        // 힌트 바에 한 번 보일 안내(예: "양 입력 필요"). 입력이 바뀌면 지운다.
        public string Notice { get; private set; } = "";
        // 보이는 것이 바뀔 때마다 오른다. 화면은 이 값이 바뀌었을 때만 다시 그린다.
        public int Version { get; private set; }
        // 세션이 글자나 커서를 바꿀 때마다 오른다. 화면은 이 값이 바뀌면 입력칸에 Text와 Caret을 다시 써 넣는다.
        public int InputVersion { get; private set; }

        public CommandHistory History => _history;
        public IReadOnlyList<Suggestion> Suggestions => Analysis.Suggestions;
        public bool IsSuggestionListVisible => Analysis.Suggestions.Count > 0 && !_bDismissed && !_history.IsNavigating;

        // 후보를 받아들였을 때 커서 뒤에 이어질 글자. 후보 목록이 보일 때만 있다.
        public string GhostText => IsSuggestionListVisible ? Analysis.GetGhostText(Selected >= 0 ? Selected : 0) : "";

        // 커서 뒤에 흐리게 보일 글자: 회색 완성이 먼저고, 없으면 남은 인자 자리 안내다.
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

        // 콘솔을 열 때 부른다. 입력하던 글자는 남기고, 고른 후보와 안내는 지운다.
        public void Begin()
        {
            Selected = -1;
            _bDismissed = false;
            Notice = "";
            Analyze();
        }

        // 사용자가 입력칸의 글자를 바꿨다.
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

        // 글자는 그대로이고 커서만 옮겼다(←/→, 클릭). 커서 자리에 따라 후보와 힌트가 달라진다.
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

        // 키를 처리했으면 true다. false면 입력칸이 원래대로 처리하게 둔다.
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

        // index번째 후보를 입력 줄에 넣는다(Tab, 후보 클릭).
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

        // 기록의 명령 줄을 입력 줄로 다시 가져온다.
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

            // 필수 인자가 남았으면 실행하지 않고 다음 인자를 입력할 자리로 옮긴다.
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
                // -1(입력 줄)을 포함해 한 바퀴 돈다.
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
