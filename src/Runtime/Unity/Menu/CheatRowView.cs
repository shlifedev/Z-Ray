using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Lumera.ZRay
{
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

        public void OnWidgetPicker(ValueWidget widget)
        {
            _menu.TogglePicker(this, widget);
        }

        public ValueWidget FindWidgetOrNull(int parameterIndex)
        {
            if (Definition.Kind == ECheatKind.Value)
            {
                return parameterIndex == 0 ? _valueWidget : null;
            }

            return _bExpanded && parameterIndex >= 0 && parameterIndex < Definition.Parameters.Count ? _argRows[parameterIndex].Widget : null;
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

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || eventData.dragging)
            {
                return;
            }

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
