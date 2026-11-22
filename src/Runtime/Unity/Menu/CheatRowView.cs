using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Lumera.ZRay
{
    // 치트 메뉴의 한 행. 행을 누르면 주 동작을 한다(인자 없는 치트는 실행, 인자 있는 치트는 입력칸 펴기, 스위치는 뒤집기).
    // 종류에 따라 오른쪽에 다른 것을 둔다.
    // - 인자 없는 치트: 초록 [실행]
    // - 인자 있는 치트: 파랑 [입력]. 누르면 행 아래로 인자 입력칸과 [실행]이 펼쳐진다. 입력하던 값은 접어도 남는다(CheatMenuModel).
    // - 값 치트: 타입에 맞는 위젯. 바꾸면 바로 쓴다.
    public sealed class CheatRowView : MonoBehaviour, IValueWidgetOwner, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public const float ARG_ROW_HEIGHT = 26.0f;

        private const float STATUS_SECONDS = 1.2f;
        private const float EDITOR_PADDING = 8.0f;

        [SerializeField] private Image _background = null;
        [SerializeField] private Button _favorite = null;
        [SerializeField] private TextMeshProUGUI _favoriteLabel = null;
        [SerializeField] private TextMeshProUGUI _title = null;
        [SerializeField] private TextMeshProUGUI _status = null;
        [SerializeField] private Button _run = null;
        [SerializeField] private TextMeshProUGUI _runLabel = null;
        [SerializeField] private ValueWidget _valueWidget = null;
        [SerializeField] private RectTransform _editor = null;
        [SerializeField] private ArgRowView _argPrefab = null;
        [SerializeField] private Button _execute = null;

        private readonly List<ArgRowView> _argRows = new List<ArgRowView>();
        private readonly StringBuilder _builder = new StringBuilder();
        private CheatMenuView _menu;
        private string[] _drafts;
        private bool _bExpanded;
        private bool _bListening;
        private bool _bHighlighted;
        private bool _bHovered;
        private float _statusHideAt;

        public CheatDefinition Definition { get; private set; }
        public float Height => ZRayTheme.MENU_ROW_HEIGHT + (_bExpanded ? (Definition.Parameters.Count + 1) * ARG_ROW_HEIGHT + EDITOR_PADDING : 0.0f);
        public IReadOnlyList<string> Drafts => _drafts;
        public string ValueText => _valueWidget.Text;

        // drafts: 이 치트의 인자마다 입력하던 글자(CheatMenuModel이 갖는다). 행이 다시 쓰여도 남는다.
        public void Bind(CheatMenuView menu, ZRayCanvas canvas, CheatDefinition definition, string query, bool bFavorite, bool bExpanded, string[] drafts)
        {
            if (!_bListening)
            {
                _bListening = true;
                _favorite.onClick.AddListener(() => _menu.ToggleFavorite(Definition));
                _run.onClick.AddListener(() => _menu.ActivateRow(this));
                _execute.onClick.AddListener(() => _menu.Run(this));
            }

            _menu = menu;
            // 같은 치트를 다시 그리는 것이면 방금 보인 실행 결과 표시를 지우지 않는다.
            if (Definition != definition)
            {
                _status.text = "";
            }

            Definition = definition;
            _drafts = drafts;
            bool bValue = definition.Kind == ECheatKind.Value;
            _bExpanded = bExpanded && !bValue && definition.HasParameters;

            _builder.Clear();
            RichText.AppendHighlighted(_builder, definition.Name, TextMatcher.FindSpans(definition.Name, query), ZRayTheme.HEX_MATCH);
            if (definition.Name != definition.Id)
            {
                _builder.Append("  <color=").Append(ZRayTheme.HEX_MUTED).Append('>');
                RichText.AppendHighlighted(_builder, definition.Id, TextMatcher.FindSpans(definition.Id, query), ZRayTheme.HEX_MATCH);
                _builder.Append("</color>");
            }

            _title.SetText(_builder);
            _favoriteLabel.text = bFavorite ? "★" : "☆";
            _favoriteLabel.color = bFavorite ? ZRayTheme.STAR : ZRayTheme.MUTED;

            _run.gameObject.SetActive(!bValue);
            bool bRunsNow = !definition.HasParameters;
            _runLabel.text = bRunsNow ? "실행" : (_bExpanded ? "접기" : "입력");
            _run.image.color = bRunsNow ? ZRayTheme.RUN : ZRayTheme.EDIT;
            _runLabel.color = bRunsNow ? ZRayTheme.RUN_TEXT : ZRayTheme.EDIT_TEXT;
            _valueWidget.gameObject.SetActive(bValue);
            if (bValue)
            {
                _valueWidget.Bind(canvas, this, definition.Parameters[0], 0, definition.CanWrite, ReadValueOrEmpty());
            }

            _editor.gameObject.SetActive(_bExpanded);
            if (_bExpanded)
            {
                BuildEditor(canvas);
                SetExecuteFocused(menu.IsExecuteFocused(definition));
            }

            RefreshBackground();
        }

        public void SetHighlighted(bool bHighlighted)
        {
            _bHighlighted = bHighlighted;
            RefreshBackground();
        }

        // 값 치트의 지금 값을 다시 읽는다. 입력하는 중이면 건드리지 않는다.
        public void RefreshValue()
        {
            if (Definition.Kind == ECheatKind.Value && !_valueWidget.IsEditing)
            {
                _valueWidget.SetText(ReadValueOrEmpty());
            }
        }

        public void ShowStatus(bool bSuccess)
        {
            _status.text = bSuccess ? "완료" : "실패";
            _status.color = bSuccess ? ZRayTheme.SUCCESS : ZRayTheme.ERROR;
            _statusHideAt = Time.unscaledTime + STATUS_SECONDS;
        }

        // 값 치트가 스위치면 뒤집는다(행을 눌렀을 때).
        public bool TryToggleValue()
        {
            return Definition.Kind == ECheatKind.Value && _valueWidget.TryToggle();
        }

        public void FocusFirstField()
        {
            if (Definition.Kind == ECheatKind.Value)
            {
                _valueWidget.Focus();
            }
            else
            {
                FocusField(-1, 1);
            }
        }

        public void FocusLastField()
        {
            FocusField(Definition.Parameters.Count, -1);
        }

        // fromIndex번째 인자에서 direction 쪽으로 가장 가까운 글자 입력칸에 포커스를 준다. 스위치와 선택지 버튼은 건너뛴다.
        // 그쪽에 입력칸이 더 없으면 false를 돌려준다.
        public bool FocusField(int fromIndex, int direction)
        {
            for (int index = fromIndex + direction; _bExpanded && index >= 0 && index < Definition.Parameters.Count; index += direction)
            {
                if (_argRows[index].Widget.HasField)
                {
                    _argRows[index].Widget.Focus();
                    return true;
                }
            }

            return false;
        }

        // 키보드 포커스가 [실행] 버튼에 있다는 것을 색으로 보인다. 이때 Space나 Enter로 실행한다(CheatMenuView).
        public void SetExecuteFocused(bool bFocused)
        {
            _execute.image.color = bFocused ? ZRayTheme.RUN_FOCUS : ZRayTheme.RUN;
        }

        public ValueWidget FindEditingWidgetOrNull()
        {
            if (Definition.Kind == ECheatKind.Value)
            {
                return _valueWidget.IsEditing ? _valueWidget : null;
            }

            for (int index = 0; _bExpanded && index < Definition.Parameters.Count; index++)
            {
                if (_argRows[index].Widget.IsEditing)
                {
                    return _argRows[index].Widget;
                }
            }

            return null;
        }

        public void OnWidgetChanged(ValueWidget widget)
        {
            if (Definition.Kind == ECheatKind.Action)
            {
                _drafts[widget.ParameterIndex] = widget.Text;
            }

            _menu.OnWidgetChanged(this, widget);
        }

        // 값 치트는 확정하는 즉시 쓴다. 치트의 인자는 [실행]이나 Enter로만 실행한다(스위치·선택지를 고른 것만으로 실행하지 않는다).
        public void OnWidgetCommitted(ValueWidget widget)
        {
            if (Definition.Kind == ECheatKind.Value)
            {
                _menu.Run(this);
            }
        }

        public bool OnWidgetKey(ValueWidget widget, Event keyEvent)
        {
            return _menu.OnWidgetKey(this, widget, keyEvent);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _bHovered = true;
            RefreshBackground();
            _menu.ShowFooter(Definition);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _bHovered = false;
            RefreshBackground();
        }

        // 행을 누르면 주 동작을 한다. 버튼과 입력칸은 자기 클릭을 따로 받으므로 여기로 오지 않는다.
        // 목록을 끌어 스크롤하면 uGUI가 클릭을 취소한다(누른 대상과 끄는 대상이 다르기 때문). dragging은 그 밖의 경우를 위한 확인이다.
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || eventData.dragging)
            {
                return;
            }

            // 펼친 입력칸 영역의 빈 곳을 눌러서는 접히지 않게, 맨 위 한 줄만 받는다.
            RectTransform rect = (RectTransform)transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position, eventData.pressEventCamera, out Vector2 local)
                || local.y < -ZRayTheme.MENU_ROW_HEIGHT)
            {
                return;
            }

            _menu.ActivateRow(this);
        }

        private void OnDisable()
        {
            _bHovered = false;
        }

        private void Update()
        {
            if (_status.text.Length > 0 && Time.unscaledTime >= _statusHideAt)
            {
                _status.text = "";
            }
        }

        private void RefreshBackground()
        {
            _background.color = _bHighlighted ? ZRayTheme.ROW_SELECTED : (_bHovered ? ZRayTheme.ROW_HOVER : ZRayTheme.TRANSPARENT);
        }

        private void BuildEditor(ZRayCanvas canvas)
        {
            IReadOnlyList<CheatParameter> parameters = Definition.Parameters;
            for (int index = 0; index < parameters.Count; index++)
            {
                if (index >= _argRows.Count)
                {
                    ArgRowView created = canvas.Create(_argPrefab, _editor);
                    RectTransform rect = (RectTransform)created.transform;
                    rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -index * ARG_ROW_HEIGHT);
                    _argRows.Add(created);
                }

                ArgRowView row = _argRows[index];
                row.gameObject.SetActive(true);
                row.SetLabel(parameters[index]);
                row.Widget.Bind(canvas, this, parameters[index], index, true, _drafts[index]);
            }

            for (int index = parameters.Count; index < _argRows.Count; index++)
            {
                _argRows[index].gameObject.SetActive(false);
            }

            RectTransform execute = (RectTransform)_execute.transform;
            execute.anchoredPosition = new Vector2(execute.anchoredPosition.x, -(parameters.Count * ARG_ROW_HEIGHT) - 2.0f);
        }

        // 값을 읽다 난 예외로 메뉴 전체가 멈추지 않게 한다. 실행하면 같은 예외가 오류로 보인다.
        private string ReadValueOrEmpty()
        {
            try
            {
                return Definition.FormatValue();
            }
            catch (System.Exception)
            {
                return "";
            }
        }
    }
}
