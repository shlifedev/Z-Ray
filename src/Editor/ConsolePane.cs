using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Lumera.ZRay.Editor
{
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
            _input.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            _input.RegisterCallback<NavigationMoveEvent>(evt => evt.StopPropagation(), TrickleDown.TrickleDown);
            _input.RegisterCallback<NavigationSubmitEvent>(evt => evt.StopPropagation(), TrickleDown.TrickleDown);
            _inputBox.Add(_input);

            Button run = new Button(() => Handle(EConsoleKey.Submit)) { text = "실행" };
            run.AddToClassList("run");
            run.AddToClassList("input-run");
            _inputBox.Add(run);
            dock.Add(_inputBox);

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

            if (_input.value != _session.Text)
            {
                _session.OnTextEdited(_input.value, GetCaret());
            }
            else if (_input.focusController?.focusedElement == _input)
            {
                _session.OnCaretMoved(_input.cursorIndex);
            }

            Render();
        }

        private void OnInputChanged(ChangeEvent<string> evt)
        {
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

            if (evt.character == '\t' || evt.character == '\n' || evt.character == '\r')
            {
                evt.StopImmediatePropagation();
                return;
            }

            if (evt.character == ' ' && Handle(EConsoleKey.AcceptSelected))
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
                _suggestions.schedule.Execute(() => _suggestions.ScrollTo(_suggestions.contentContainer[_session.Selected]));
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
