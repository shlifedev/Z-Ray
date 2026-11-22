using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Lumera.ZRay
{
    // 화면 아래의 개발자 콘솔. 입력 상태와 키 동작은 ConsoleSession, 힌트 문장은 ConsoleHintFormatter,
    // 기록은 호스트의 ConsoleTranscript가 갖는다. 여기서는 키를 읽어 넘기고 결과를 그린다.
    // 여닫는 키와 Esc는 호스트가 처리한다(ZRayHost.Update).
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

        // 후보 목록이 떠 있으면 그것만 닫는다. 닫을 것이 없으면 false를 돌려주고 호스트가 콘솔을 닫는다.
        public bool HandleEscape()
        {
            bool bHandled = _session.HandleKey(EConsoleKey.Dismiss);
            Render();
            return bHandled;
        }

        private void LateUpdate()
        {
            // 입력칸은 선택된 다음 프레임부터 글자를 받는다. 여는 키가 첫 글자로 들어가지 않게 한 프레임 늦춰 포커스를 준다.
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

        // true를 돌려주면 그 키는 입력칸이 처리하지 않는다.
        private bool OnKey(Event keyEvent)
        {
            char character = keyEvent.character;
            // 여닫는 키의 글자와, 콘솔이 따로 쓰는 키의 글자는 입력에 넣지 않는다.
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
            // 후보를 누르면 입력칸이 포커스를 잃는다. 다음 프레임에 되돌린다.
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
            // 세션이 글자를 바꿨으면(후보 넣기, 기록) 입력칸에 다시 쓴다.
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

        // 힌트가 없을 때는 하단 힌트 바를 숨기고 입력 줄을 콘솔 아래에 바로 붙인다.
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

        // 입력한 글자를 투명하게 한 번 더 쓰고 그 뒤에 회색 글자를 잇는다. 그러면 글자 폭을 재지 않아도 커서 뒤에 정확히 붙는다.
        private void RenderGhost()
        {
            // 조합 중인 한글까지 포함한, 지금 화면에 보이는 글자. TMP가 끝에 붙이는 폭 없는 공백은 뗀다.
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

        // 기록이 바뀌었거나 보이는 높이가 바뀌었을 때만 다시 그린다. 줄 높이가 같아서 번호로 자리를 잡는다.
        // 줄이 화면을 채우지 못하면 아래쪽(입력 줄 바로 위)부터 쌓이게 위를 비운다.
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

        // direction: 1이면 옛 줄 쪽으로 한 화면의 대부분만큼 올린다.
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
