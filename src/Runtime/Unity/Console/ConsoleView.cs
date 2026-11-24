using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Lumera.ZRay
{
    public sealed class ConsoleView : MonoBehaviour
    {
        public const float HEADER_HEIGHT = 28.0f;
        public const float INPUT_HEIGHT = 28.0f;
        public const float HINT_HEIGHT = 38.0f;
        public const float PAD = 10.0f;
        private const float GAP = 6.0f;

        [SerializeField] private ScrollRect _linesScroll = null;
        [SerializeField] private RectTransform _linesContent = null;
        [SerializeField] private ConsoleLineView _linePrefab = null;
        [SerializeField] private RectTransform _inputRow = null;
        [SerializeField] private KeyFilteredInputField _input = null;
        [SerializeField] private TextMeshProUGUI _ghost = null;
        [SerializeField] private RectTransform _hintBar = null;
        [SerializeField] private TextMeshProUGUI _hint = null;
        [SerializeField] private SuggestionListView _suggestions = null;
        [SerializeField] private Button _run = null;
        [SerializeField] private Button _clear = null;
        [SerializeField] private Button _close = null;
        [SerializeField] private Button _openMenu = null;

        private readonly List<ConsoleLineView> _lineViews = new List<ConsoleLineView>();
        private readonly List<StyledText> _hintFirst = new List<StyledText>();
        private readonly List<StyledText> _hintSecond = new List<StyledText>();
        private readonly StringBuilder _builder = new StringBuilder();
        private ZRayHost _host;
        private ZRayCanvas _canvas;
        private ConsoleSession _session;
        private int _shownSessionVersion = -1;
        private int _shownInputVersion;
        private int _shownTranscriptVersion = -1;
        private float _shownViewHeight = -1.0f;
        private string _shownGhostSource = "";
        private bool _bFocusPending;
        private bool _bHintVisible;

        public void Initialize(ZRayHost host, ZRayCanvas canvas)
        {
            _host = host;
            _canvas = canvas;
            _session = new ConsoleSession(host.Assist, host.Executor, host.History);
            _shownInputVersion = _session.InputVersion;
            _input.KeyFilterOrNull = OnKey;
            _input.onValueChanged.AddListener(OnInputChanged);
            _suggestions.Initialize(canvas.Create, OnSuggestionClicked);
            _run.onClick.AddListener(OnRunClicked);
            _clear.onClick.AddListener(OnClearClicked);
            _close.onClick.AddListener(() => _canvas.Show(EDebuggerOverlay.None));
            _openMenu.onClick.AddListener(() => _canvas.Show(EDebuggerOverlay.Menu));
            _openMenu.GetComponentInChildren<TextMeshProUGUI>(true).text = $"{host.MenuKeyLabel} 치트 메뉴";
            ApplyHintLayout(false);
        }

        public void Open()
        {
            gameObject.SetActive(true);
            _bFocusPending = true;
            _session.Begin();
            Render();
        }

        public void Close()
        {
            _host.History.ResetNavigation();
            _input.DeactivateInputField();
            gameObject.SetActive(false);
        }

        public bool HandleEscape()
        {
            bool bHandled = _session.HandleKey(EConsoleKey.Dismiss);
            Render();
            return bHandled;
        }

        private void LateUpdate()
        {
            if (_bFocusPending)
            {
                _bFocusPending = false;
                _input.ActivateInputField();
                _input.caretPosition = _session.Caret;
            }

            if (_input.isFocused)
            {
                _session.OnCaretMoved(_input.caretPosition);
            }

            Render();
        }

        private void OnInputChanged(string text)
        {
            _session.OnTextEdited(text, _input.caretPosition);
            Render();
        }

        private bool OnKey(Event keyEvent)
        {
            char character = keyEvent.character;
            if (character == '`' || character == '~' || character == '₩' || character == '\t' || character == '\n' || character == '\r')
            {
                return true;
            }

            bool bHandled;
            switch (keyEvent.keyCode)
            {
                case KeyCode.Tab:
                    bHandled = _session.HandleKey(EConsoleKey.Complete);
                    break;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    bHandled = _session.HandleKey(keyEvent.control || keyEvent.command ? EConsoleKey.ForceSubmit : EConsoleKey.Submit);
                    break;
                case KeyCode.UpArrow:
                    bHandled = _session.HandleKey(EConsoleKey.Up);
                    break;
                case KeyCode.DownArrow:
                    bHandled = _session.HandleKey(EConsoleKey.Down);
                    break;
                case KeyCode.RightArrow:
                    bHandled = _session.HandleKey(EConsoleKey.AcceptGhost);
                    break;
                case KeyCode.PageUp:
                    ScrollLines(1);
                    return true;
                case KeyCode.PageDown:
                    ScrollLines(-1);
                    return true;
                case KeyCode.Escape:
                case KeyCode.BackQuote:
                    return true;
                default:
                    return false;
            }

            Render();
            return bHandled;
        }

        private void OnSuggestionClicked(int index)
        {
            _session.Accept(index);
            Render();
            _bFocusPending = true;
        }

        private void OnLineRecalled(string command)
        {
            _session.Recall(command);
            Render();
            _bFocusPending = true;
        }

        private void OnRunClicked()
        {
            _session.HandleKey(EConsoleKey.Submit);
            Render();
            _bFocusPending = true;
        }

        private void OnClearClicked()
        {
            _host.Transcript.Clear();
            Render();
            _bFocusPending = true;
        }

        private void Render()
        {
            if (_shownInputVersion != _session.InputVersion)
            {
                _shownInputVersion = _session.InputVersion;
                _input.SetTextAndCaret(_session.Text, _session.Caret);
            }

            if (_shownSessionVersion != _session.Version)
            {
                _shownSessionVersion = _session.Version;
                if (_session.IsSuggestionListVisible)
                {
                    _suggestions.Show(_session.Suggestions, _session.Selected);
                }
                else
                {
                    _suggestions.Hide();
                }

                ConsoleHintFormatter.Build(_session, _host.Registry, _hintFirst, _hintSecond);
                bool bHasHint = _hintFirst.Count > 0 || _hintSecond.Count > 0;
                if (bHasHint != _bHintVisible)
                {
                    ApplyHintLayout(bHasHint);
                }

                if (bHasHint)
                {
                    _builder.Clear();
                    RichText.AppendStyled(_builder, _hintFirst, ZRayTheme.GetHex);
                    if (_hintFirst.Count > 0 && _hintSecond.Count > 0)
                    {
                        _builder.Append('\n');
                    }

                    RichText.AppendStyled(_builder, _hintSecond, ZRayTheme.GetHex);
                    _hint.SetText(_builder);
                }
            }

            RenderLines();
            RenderGhost();
        }

        private void ApplyHintLayout(bool bShowHint)
        {
            _bHintVisible = bShowHint;
            _hintBar.gameObject.SetActive(bShowHint);
            float inputBottom = bShowHint ? HINT_HEIGHT + GAP : PAD;
            _inputRow.offsetMin = new Vector2(PAD, inputBottom);
            _inputRow.offsetMax = new Vector2(-PAD, inputBottom + INPUT_HEIGHT);

            RectTransform linesRect = (RectTransform)_linesScroll.transform;
            linesRect.offsetMin = new Vector2(PAD, inputBottom + INPUT_HEIGHT + GAP);
            linesRect.offsetMax = new Vector2(-PAD, -(HEADER_HEIGHT + GAP));

            RectTransform suggestionsRect = (RectTransform)_suggestions.transform;
            suggestionsRect.anchoredPosition = new Vector2(PAD + 22.0f, inputBottom + INPUT_HEIGHT + 2.0f);
            _shownViewHeight = -1.0f;
        }

        private void RenderGhost()
        {
            string shown = _input.textComponent.text.TrimEnd('\u200B');
            string tail = _session.TrailingHint;
            string source = shown + "\n" + tail;
            if (source == _shownGhostSource)
            {
                return;
            }

            _shownGhostSource = source;
            _builder.Clear();
            if (tail.Length > 0)
            {
                RichText.AppendColored(_builder, shown, ZRayTheme.HEX_INVISIBLE);
                RichText.AppendColored(_builder, tail, ZRayTheme.HEX_GHOST);
            }

            _ghost.SetText(_builder);
        }

        private void RenderLines()
        {
            ConsoleTranscript transcript = _host.Transcript;
            float viewHeight = _linesScroll.viewport.rect.height;
            if (_shownTranscriptVersion == transcript.Version && Mathf.Approximately(_shownViewHeight, viewHeight))
            {
                return;
            }

            _shownTranscriptVersion = transcript.Version;
            _shownViewHeight = viewHeight;
            IReadOnlyList<TranscriptLine> lines = transcript.Lines;
            while (_lineViews.Count < lines.Count)
            {
                _lineViews.Add(_canvas.Create(_linePrefab, _linesContent));
            }

            float linesHeight = lines.Count * ZRayTheme.LINE_HEIGHT;
            float top = Mathf.Max(0.0f, viewHeight - linesHeight);
            for (int index = 0; index < _lineViews.Count; index++)
            {
                ConsoleLineView view = _lineViews[index];
                bool bUsed = index < lines.Count;
                view.gameObject.SetActive(bUsed);
                if (bUsed)
                {
                    view.Set(lines[index], OnLineRecalled);
                    RectTransform rect = (RectTransform)view.transform;
                    rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -(top + (index * ZRayTheme.LINE_HEIGHT)));
                }
            }

            _linesContent.sizeDelta = new Vector2(_linesContent.sizeDelta.x, Mathf.Max(linesHeight, viewHeight));
            _linesScroll.StopMovement();
            _linesScroll.verticalNormalizedPosition = 0.0f;
        }

        private void ScrollLines(int direction)
        {
            float hidden = _linesContent.rect.height - _linesScroll.viewport.rect.height;
            if (hidden <= 0.0f)
            {
                return;
            }

            float step = (_linesScroll.viewport.rect.height * 0.8f) / hidden;
            _linesScroll.verticalNormalizedPosition = Mathf.Clamp01(_linesScroll.verticalNormalizedPosition + (direction * step));
        }
    }
}
