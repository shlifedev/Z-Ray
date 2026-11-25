using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Lumera.ZRay.Editor
{
    // 에디터 창의 콘솔. 입력 상태와 키 동작은 ConsoleSession, 힌트는 ConsoleHintFormatter, 기록은 호스트의 ConsoleTranscript다.
    // 런타임 콘솔과 기록·입력 기록을 함께 쓰고, 입력 줄은 따로 갖는다.
    internal sealed class ConsolePane
    {
        private readonly ScrollView _lines = new ScrollView();
        private readonly ScrollView _suggestions = new ScrollView();
        private readonly VisualElement _inputBox = new VisualElement();
        private readonly TextField _input = new TextField();
        private readonly Label _ghost = new Label();
        private readonly VisualElement _hintBar = new VisualElement();
        private readonly Label _hint = new Label();
        private readonly List<StyledText> _hintFirst = new List<StyledText>();
        private readonly List<StyledText> _hintSecond = new List<StyledText>();
        private readonly StringBuilder _builder = new StringBuilder();
        private ZRayHost _hostOrNull;
        private ConsoleSession _session;
        private int _shownTranscriptVersion = -1;
        private int _shownSessionVersion = -1;
        private int _shownInputVersion = -1;
        private string _shownGhost = "";

        public VisualElement Root { get; } = new VisualElement();

        public ConsolePane()
        {
            Root.AddToClassList("console");

            VisualElement logSurface = new VisualElement();
            logSurface.AddToClassList("console-surface");
            _lines.AddToClassList("lines");
            _lines.mode = ScrollViewMode.Vertical;
            _lines.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            logSurface.Add(_lines);

            _suggestions.AddToClassList("suggestions");
            _suggestions.mode = ScrollViewMode.Vertical;
            _suggestions.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _suggestions.style.display = DisplayStyle.None;
            logSurface.Add(_suggestions);
            Root.Add(logSurface);

            VisualElement dock = new VisualElement();
            dock.AddToClassList("console-dock");

            _inputBox.AddToClassList("input-box");
            Label prompt = new Label(">");
            prompt.AddToClassList("prompt");
            _inputBox.Add(prompt);

            _input.AddToClassList("input");
            _input.textEdition.placeholder = "명령을 입력하세요 (help)";
            _input.textEdition.hidePlaceholderOnFocus = false;
            _input.RegisterValueChangedCallback(OnInputChanged);
            _input.RegisterCallback<FocusInEvent>(_ => _inputBox.AddToClassList("focused"));
            _input.RegisterCallback<FocusOutEvent>(_ => _inputBox.RemoveFromClassList("focused"));
            // TextField보다 먼저 키를 본다. Tab·Enter·↑↓는 콘솔이 쓰고, 입력칸의 기본 동작(포커스 이동, 제출)은 막는다.
            _input.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            _input.RegisterCallback<NavigationMoveEvent>(evt => evt.StopPropagation(), TrickleDown.TrickleDown);
            _input.RegisterCallback<NavigationSubmitEvent>(evt => evt.StopPropagation(), TrickleDown.TrickleDown);
            _inputBox.Add(_input);

            Button run = new Button(() => Handle(EConsoleKey.Submit)) { text = "실행" };
            run.AddToClassList("run");
            run.AddToClassList("input-run");
            _inputBox.Add(run);
            dock.Add(_inputBox);

            // 회색 완성: 입력 글자와 같은 자리에 투명한 입력 글자 + 회색 글자를 겹쳐 쓴다.
            _ghost.AddToClassList("ghost");
            _ghost.pickingMode = PickingMode.Ignore;
            _ghost.enableRichText = true;
            _input.RegisterCallback<GeometryChangedEvent>(_ => PlaceGhost());

            _hintBar.AddToClassList("hint-bar");
            _hintBar.style.display = DisplayStyle.None;
            _hint.AddToClassList("hint");
            _hint.enableRichText = true;
            _hintBar.Add(_hint);
            dock.Add(_hintBar);

            Root.Add(dock);
        }

        public void Bind(ZRayHost hostOrNull)
        {
            _hostOrNull = hostOrNull;
            _session = hostOrNull == null ? null : new ConsoleSession(hostOrNull.Assist, hostOrNull.Executor, hostOrNull.History);
            _shownTranscriptVersion = -1;
            _shownSessionVersion = -1;
            _shownInputVersion = -1;
            _lines.Clear();
            _suggestions.Clear();
            _suggestions.style.display = DisplayStyle.None;
            _hintBar.style.display = DisplayStyle.None;
            _input.SetValueWithoutNotify("");
        }

        public void Focus()
        {
            _input.schedule.Execute(() => _input.Focus());
        }

        public void Tick()
        {
            if (_session == null)
            {
                return;
            }

            // 값 변경 이벤트가 오지 않은 글자 변화(다른 도구가 넣은 값, 창이 포커스를 갖기 전의 변경)도 따라간다.
            if (_input.value != _session.Text)
            {
                _session.OnTextEdited(_input.value, GetCaret());
            }
            // 글자는 그대로인데 커서만 옮겼을 때(←/→, 클릭)도 후보와 힌트가 달라진다.
            else if (_input.focusController?.focusedElement == _input)
            {
                _session.OnCaretMoved(_input.cursorIndex);
            }

            Render();
        }

        private void OnInputChanged(ChangeEvent<string> evt)
        {
            // 회색 완성 라벨이 입력칸 안에 있어서, 라벨 글자를 바꿀 때 나는 변경 이벤트도 올라온다. 입력칸 자신의 것만 받는다.
            if (_session == null || evt.target != _input)
            {
                return;
            }

            _session.OnTextEdited(evt.newValue, GetCaret());
            Render();
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (_session == null)
            {
                return;
            }

            // Tab과 Enter는 글자 이벤트로도 한 번 더 온다. 그것도 입력칸에 넣지 않는다.
            if (evt.character == '\t' || evt.character == '\n' || evt.character == '\r')
            {
                evt.StopImmediatePropagation();
                return;
            }

            bool bHandled;
            switch (evt.keyCode)
            {
                case KeyCode.Tab:
                    bHandled = Handle(EConsoleKey.Complete);
                    break;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    bHandled = Handle(evt.actionKey ? EConsoleKey.ForceSubmit : EConsoleKey.Submit);
                    break;
                case KeyCode.UpArrow:
                    bHandled = Handle(EConsoleKey.Up);
                    break;
                case KeyCode.DownArrow:
                    bHandled = Handle(EConsoleKey.Down);
                    break;
                case KeyCode.RightArrow:
                    bHandled = Handle(EConsoleKey.AcceptGhost);
                    break;
                case KeyCode.Escape:
                    bHandled = Handle(EConsoleKey.Dismiss);
                    break;
                default:
                    return;
            }

            if (bHandled)
            {
                evt.StopImmediatePropagation();
            }
        }

        // 포커스가 없을 때 바뀐 글자(코드가 넣은 값)는 커서가 0에 남아 있다. 그때는 줄 끝을 커서로 본다.
        private int GetCaret()
        {
            bool bFocused = _input.focusController?.focusedElement == _input;
            return bFocused || _input.cursorIndex > 0 ? _input.cursorIndex : _input.value.Length;
        }

        private bool Handle(EConsoleKey key)
        {
            if (_session == null)
            {
                return false;
            }

            bool bHandled = _session.HandleKey(key);
            Render();
            return bHandled;
        }

        private void Render()
        {
            // 세션이 글자를 바꿨으면(후보 넣기, 기록) 입력칸에 다시 쓴다.
            if (_shownInputVersion != _session.InputVersion)
            {
                _shownInputVersion = _session.InputVersion;
                _input.SetValueWithoutNotify(_session.Text);
                _input.SelectRange(_session.Caret, _session.Caret);
            }

            if (_shownSessionVersion != _session.Version)
            {
                _shownSessionVersion = _session.Version;
                RenderSuggestions();
                ConsoleHintFormatter.Build(_session, _hostOrNull.Registry, _hintFirst, _hintSecond);
                bool bHasHint = _hintFirst.Count > 0 || _hintSecond.Count > 0;
                _hintBar.style.display = bHasHint ? DisplayStyle.Flex : DisplayStyle.None;
                if (bHasHint)
                {
                    _builder.Clear();
                    RichText.AppendStyled(_builder, _hintFirst, GetHex);
                    if (_hintFirst.Count > 0 && _hintSecond.Count > 0)
                    {
                        _builder.Append('\n');
                    }

                    RichText.AppendStyled(_builder, _hintSecond, GetHex);
                    _hint.text = _builder.ToString();
                }
            }

            RenderGhost();
            RenderLines();
        }

        private void RenderSuggestions()
        {
            _suggestions.Clear();
            bool bVisible = _session.IsSuggestionListVisible;
            _suggestions.style.display = bVisible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!bVisible)
            {
                return;
            }

            string match = GetMatchHex();
            string muted = GetHex(ETextStyle.Muted);
            IReadOnlyList<Suggestion> suggestions = _session.Suggestions;
            for (int index = 0; index < suggestions.Count; index++)
            {
                Suggestion suggestion = suggestions[index];
                VisualElement row = new VisualElement();
                row.AddToClassList("suggestion");
                row.EnableInClassList("selected", index == _session.Selected);
                _builder.Clear();
                RichText.AppendHighlighted(_builder, suggestion.Insert, suggestion.InsertSpans, match);
                if (suggestion.Detail.Length > 0)
                {
                    _builder.Append("  <color=").Append(muted).Append('>');
                    RichText.AppendHighlighted(_builder, suggestion.Detail, suggestion.DetailSpans, match);
                    _builder.Append("</color>");
                }

                Label label = new Label(_builder.ToString()) { enableRichText = true };
                label.AddToClassList("suggestion-label");
                row.Add(label);
                if (suggestion.Note.Length > 0)
                {
                    Label note = new Label(suggestion.Note);
                    note.AddToClassList("suggestion-note");
                    row.Add(note);
                }

                int captured = index;
                row.RegisterCallback<ClickEvent>(_ =>
                {
                    _session.Accept(captured);
                    Render();
                    Focus();
                });
                _suggestions.Add(row);
            }

            if (_session.Selected >= 0)
            {
                VisualElement selectedRow = _suggestions.contentContainer[_session.Selected];
                _suggestions.schedule.Execute(() =>
                {
                    if (selectedRow.parent == _suggestions.contentContainer)
                    {
                        _suggestions.ScrollTo(selectedRow);
                    }
                });
            }
        }

        private void RenderGhost()
        {
            string tail = _session.TrailingHint;
            string source = _input.value + "\n" + tail;
            if (source == _shownGhost)
            {
                return;
            }

            _shownGhost = source;
            _builder.Clear();
            if (tail.Length > 0)
            {
                RichText.AppendColored(_builder, _input.value, "#00000000");
                RichText.AppendColored(_builder, tail, GetHex(ETextStyle.Muted));
            }

            _ghost.text = _builder.ToString();
        }

        // 회색 글자를 입력칸의 글자 영역에 붙인다. 입력 글자와 같은 글꼴·위치라 이어 붙는다.
        private void PlaceGhost()
        {
            VisualElement text = _input.Q<TextElement>();
            if (text == null)
            {
                return;
            }

            if (_ghost.parent != text.parent)
            {
                text.parent.Add(_ghost);
            }

            _ghost.style.left = text.layout.x;
            _ghost.style.top = text.layout.y;
            _ghost.style.height = text.layout.height;
            _ghost.style.paddingLeft = text.resolvedStyle.paddingLeft;
            _ghost.style.paddingTop = text.resolvedStyle.paddingTop;
            _ghost.style.unityTextAlign = text.resolvedStyle.unityTextAlign;
        }

        private void RenderLines()
        {
            ConsoleTranscript transcript = _hostOrNull.Transcript;
            if (_shownTranscriptVersion == transcript.Version)
            {
                return;
            }

            _shownTranscriptVersion = transcript.Version;
            _lines.Clear();
            foreach (TranscriptLine line in transcript.Lines)
            {
                bool bIndented = line.Text.StartsWith("  ");
                Label label = new Label(bIndented ? line.Text.Substring(2) : line.Text) { enableRichText = false };
                label.AddToClassList("line");
                label.AddToClassList(GetLineClass(line.Kind));
                if (bIndented)
                {
                    label.AddToClassList("indented");
                }

                label.selection.isSelectable = true;
                if (line.Recall.Length > 0)
                {
                    string recall = line.Recall;
                    label.tooltip = "누르면 입력 줄로 가져온다";
                    label.RegisterCallback<ClickEvent>(_ =>
                    {
                        _session.Recall(recall);
                        Render();
                        Focus();
                    });
                }

                _lines.Add(label);
            }

            _lines.schedule.Execute(() => _lines.scrollOffset = new Vector2(0.0f, float.MaxValue));
        }

        private static string GetLineClass(EConsoleLineKind kind)
        {
            switch (kind)
            {
                case EConsoleLineKind.Echo:
                    return "echo";
                case EConsoleLineKind.Muted:
                    return "muted";
                case EConsoleLineKind.Success:
                    return "success";
                case EConsoleLineKind.Warning:
                    return "warning";
                case EConsoleLineKind.Error:
                    return "error";
                default:
                    return "info";
            }
        }

        private static string GetHex(ETextStyle style)
        {
            bool bDark = EditorGUIUtility.isProSkin;
            switch (style)
            {
                case ETextStyle.Muted:
                    return bDark ? "#A6B2C7" : "#5A6270";
                case ETextStyle.Accent:
                    return bDark ? "#7EC2FF" : "#0054BE";
                case ETextStyle.Error:
                    return bDark ? "#FF867D" : "#B4281E";
                default:
                    return bDark ? "#F0F4FA" : "#1D222B";
            }
        }

        private static string GetMatchHex()
        {
            return EditorGUIUtility.isProSkin ? "#FFD873" : "#A65E00";
        }
    }
}
