using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Lumera.ZRay
{
    public sealed class CheatMenuView : MonoBehaviour
    {
        private const float POLL_SECONDS = 0.25f;
        private const float PICKER_GAP = 2.0f;

        [SerializeField] private KeyFilteredInputField _search = null;
        [SerializeField] private Button _close = null;
        [SerializeField] private Button _openConsole = null;
        [SerializeField] private Button _collapseAll = null;
        [SerializeField] private RectTransform _categoryContent = null;
        [SerializeField] private CategoryRowView _categoryPrefab = null;
        [SerializeField] private ScrollRect _listScroll = null;
        [SerializeField] private RectTransform _listContent = null;
        [SerializeField] private CheatRowView _rowPrefab = null;
        [SerializeField] private GroupHeaderView _headerPrefab = null;
        [SerializeField] private TextMeshProUGUI _empty = null;
        [SerializeField] private TextMeshProUGUI _footer = null;
        [SerializeField] private SuggestionListView _popup = null;
        [SerializeField] private ColorPickerView _picker = null;

        private readonly List<CheatRowView> _rows = new List<CheatRowView>();
        private readonly List<GroupHeaderView> _headers = new List<GroupHeaderView>();
        private readonly List<CategoryRowView> _categoryRows = new List<CategoryRowView>();
        private readonly List<string> _previousArgs = new List<string>();
        private ZRayHost _host;
        private ZRayCanvas _canvas;
        private CheatMenuModel _model;
        private int _highlight = -1;
        private int _activeRowCount;
        private float _nextPollAt;
        private bool _bFocusPending;
        private ValueWidget _popupWidget;
        private CheatRowView _popupRow;
        private List<Suggestion> _popupSuggestions;
        private int _popupSelected = -1;
        private bool _bPopupDismissed;
        private bool _bPopupRequested;
        private CheatDefinition _executeFocusOrNull;
        private int _executeFocusFrame;
        private System.Action _deferredOrNull;
        private InputAction _confirm;
        private InputAction _tab;
        private InputAction _shift;
        private CheatDefinition _pickerDefinitionOrNull;
        private int _pickerParameterIndex;
        private bool _bPickerApplying;

        public void Initialize(ZRayHost host, ZRayCanvas canvas)
        {
            _host = host;
            _canvas = canvas;
            _model = new CheatMenuModel(host.Registry, host.Favorites, host.History, host.Drafts, host.MenuState);
            _confirm = new InputAction("Confirm", InputActionType.Button);
            _confirm.AddBinding("<Keyboard>/space");
            _confirm.AddBinding("<Keyboard>/enter");
            _confirm.AddBinding("<Keyboard>/numpadEnter");
            _tab = new InputAction("Tab", InputActionType.Button, "<Keyboard>/tab");
            _shift = new InputAction("Shift", InputActionType.Button, "<Keyboard>/shift");
            _search.KeyFilterOrNull = OnSearchKey;
            _search.onValueChanged.AddListener(OnSearchChanged);
            _close.onClick.AddListener(() => _canvas.Show(EDebuggerOverlay.None));
            _openConsole.onClick.AddListener(() => _canvas.Show(EDebuggerOverlay.Console));
            _openConsole.GetComponentInChildren<TextMeshProUGUI>(true).text = $"{host.ConsoleKeyLabel} 콘솔";
            _collapseAll.onClick.AddListener(ToggleAllGroups);
            _popup.Initialize(canvas.Create, OnPopupClicked);
            if (_picker != null)
            {
                _picker.Initialize(canvas, OnPickerChanged, OnPickerReleased, ClosePicker);
            }
        }

        public void Open()
        {
            gameObject.SetActive(true);
            _confirm.Enable();
            _tab.Enable();
            _shift.Enable();
            _bFocusPending = true;
            _highlight = -1;
            _footer.text = "";
            Rebuild();
        }

        public void Close()
        {
            _confirm.Disable();
            _tab.Disable();
            _shift.Disable();
            _executeFocusOrNull = null;
            _deferredOrNull = null;
            HidePopup();
            ClosePicker();
            _search.DeactivateInputField();
            gameObject.SetActive(false);
        }

        public bool HandleEscape()
        {
            if (_pickerDefinitionOrNull != null)
            {
                ClosePicker();
                return true;
            }

            if (_popup.IsVisible)
            {
                _bPopupDismissed = true;
                _popup.Hide();
                return true;
            }

            if (FindEditingWidgetOrNull(out _) != null || _executeFocusOrNull != null)
            {
                SetExecuteFocus(null);
                _bFocusPending = true;
                return true;
            }

            if (_model.IsSearching)
            {
                _search.SetTextAndCaret("", 0);
                OnSearchChanged("");
                _bFocusPending = true;
                return true;
            }

            return false;
        }

        public void ToggleFavorite(CheatDefinition definition)
        {
            _host.Favorites.Toggle(definition.Id);
            _host.SaveFavorites();
            Rebuild();
        }

        public void ActivateRow(CheatRowView row)
        {
            CheatDefinition definition = row.Definition;
            if (definition.Kind == ECheatKind.Value)
            {
                if (!row.TryToggleValue())
                {
                    row.FocusFirstField();
                }
            }
            else if (!definition.HasParameters)
            {
                Run(row);
            }
            else
            {
                bool bExpanded = _model.ToggleExpanded(definition);
                Rebuild();
                if (bExpanded)
                {
                    FindRowOrNull(definition)?.FocusFirstField();
                }
            }
        }

        public void Run(CheatRowView row)
        {
            CheatDefinition definition = row.Definition;
            string line = definition.Kind == ECheatKind.Value ? CheatMenuModel.BuildValueLine(definition, row.ValueText) : _model.BuildLine(definition);
            ExecutionResult result = _host.ExecuteFromMenu(line);
            _canvas.Toast.Show(result.Message, result.IsSuccess);
            row.ShowStatus(result.IsSuccess);
            row.RefreshValue();
            HidePopup();
        }

        public void ShowFooter(CheatDefinition definition)
        {
            _footer.text = definition.Description.Length == 0
                ? CheatFormatter.FormatSignature(definition)
                : $"{CheatFormatter.FormatSignature(definition)}  —  {definition.Description}";
        }

        public void OnWidgetChanged(CheatRowView row, ValueWidget widget)
        {
            _bPopupDismissed = false;
            _bPopupRequested = true;
            RefreshPopup(row, widget);
            if (!_bPickerApplying && _pickerDefinitionOrNull == row.Definition && _pickerParameterIndex == widget.ParameterIndex
                && ColorValueType.TryParseColor(widget.Text, out Color32 typed))
            {
                _picker.SetColor(typed);
            }
        }

        public void TogglePicker(CheatRowView row, ValueWidget widget)
        {
            if (_picker == null)
            {
                return;
            }

            bool bSame = _pickerDefinitionOrNull == row.Definition && _pickerParameterIndex == widget.ParameterIndex;
            ClosePicker();
            if (bSame)
            {
                return;
            }

            HidePopup();
            _pickerDefinitionOrNull = row.Definition;
            _pickerParameterIndex = widget.ParameterIndex;
            if (!ColorValueType.TryParseColor(widget.Text, out Color32 color))
            {
                color = new Color32(255, 255, 255, 255);
            }

            _picker.Show(color);
            PlacePicker(widget.PickerAnchor);
        }

        public bool OnWidgetKey(CheatRowView row, ValueWidget widget, Event keyEvent)
        {
            char character = keyEvent.character;
            if (character == '\t' || character == '\n' || character == '\r' || character == '`')
            {
                return true;
            }

            bool bPopup = _popup.IsVisible && _popupWidget == widget;
            switch (keyEvent.keyCode)
            {
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    if (bPopup && _popupSelected >= 0)
                    {
                        ApplyPopup(_popupSelected);
                    }
                    else
                    {
                        Run(row);
                    }

                    return true;
                case KeyCode.Tab:
                    if (bPopup && _popupSelected >= 0)
                    {
                        ApplyPopup(_popupSelected);
                    }

                    if (row.Definition.Kind == ECheatKind.Action)
                    {
                        int from = widget.ParameterIndex;
                        int direction = keyEvent.shift ? -1 : 1;
                        _deferredOrNull = () => MoveFocus(row, from, direction);
                    }

                    return true;
                case KeyCode.DownArrow:
                case KeyCode.UpArrow:
                    if (!bPopup && keyEvent.keyCode == KeyCode.DownArrow)
                    {
                        _bPopupDismissed = false;
                        _bPopupRequested = true;
                        RefreshPopup(row, widget);
                    }
                    else if (bPopup)
                    {
                        int count = _popupSuggestions.Count;
                        int step = keyEvent.keyCode == KeyCode.DownArrow ? 1 : -1;
                        _popupSelected = ((_popupSelected + 1 + step + count + 1) % (count + 1)) - 1;
                        _popup.Select(_popupSelected);
                    }

                    return true;
                case KeyCode.Escape:
                case KeyCode.BackQuote:
                    return true;
                default:
                    return false;
            }
        }

        public bool IsExecuteFocused(CheatDefinition definition)
        {
            return _executeFocusOrNull == definition;
        }

        private void Update()
        {
            if (_deferredOrNull != null)
            {
                System.Action deferred = _deferredOrNull;
                _deferredOrNull = null;
                deferred();
            }

            if (_bFocusPending)
            {
                _bFocusPending = false;
                _search.ActivateInputField();
                _search.caretPosition = _search.text.Length;
            }

            UpdateExecuteFocus();

            if (_model.Refresh())
            {
                Render();
            }

            if (Time.unscaledTime >= _nextPollAt)
            {
                _nextPollAt = Time.unscaledTime + POLL_SECONDS;
                for (int index = 0; index < _activeRowCount; index++)
                {
                    _rows[index].RefreshValue();
                }
            }

            ValueWidget editing = FindEditingWidgetOrNull(out CheatRowView editingRow);
            if (editing != _popupWidget)
            {
                HidePopup();
                _popupWidget = editing;
                _popupRow = editingRow;
            }
        }

        private void MoveFocus(CheatRowView row, int fromIndex, int direction)
        {
            if (!row.FocusField(fromIndex, direction) && direction > 0)
            {
                SetExecuteFocus(row.Definition);
            }
        }

        private void SetExecuteFocus(CheatDefinition definitionOrNull)
        {
            if (_executeFocusOrNull != null)
            {
                FindRowOrNull(_executeFocusOrNull)?.SetExecuteFocused(false);
            }

            _executeFocusOrNull = definitionOrNull;
            if (definitionOrNull == null)
            {
                return;
            }

            _executeFocusFrame = Time.frameCount;
            HidePopup();
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }

            FindRowOrNull(definitionOrNull)?.SetExecuteFocused(true);
            _footer.text = "Space 실행 · Tab 첫 입력칸으로 · Esc 검색으로";
        }

        private void UpdateExecuteFocus()
        {
            if (_executeFocusOrNull == null)
            {
                return;
            }

            CheatRowView row = FindRowOrNull(_executeFocusOrNull);
            if (row == null || !_model.IsExpanded(_executeFocusOrNull) || FindEditingWidgetOrNull(out _) != null || _search.isFocused)
            {
                SetExecuteFocus(null);
                return;
            }

            if (Time.frameCount == _executeFocusFrame)
            {
                return;
            }

            if (_confirm.WasPressedThisFrame())
            {
                Run(row);
            }
            else if (_tab.WasPressedThisFrame())
            {
                SetExecuteFocus(null);
                if (_shift.IsPressed())
                {
                    row.FocusLastField();
                }
                else
                {
                    row.FocusFirstField();
                }
            }
        }

        private void OnDestroy()
        {
            _confirm?.Dispose();
            _tab?.Dispose();
            _shift?.Dispose();
        }

        private void OnSearchChanged(string text)
        {
            _model.Query = text;
            _highlight = -1;
            Rebuild();
            _listScroll.verticalNormalizedPosition = 1.0f;
        }

        private bool OnSearchKey(Event keyEvent)
        {
            char character = keyEvent.character;
            if (character == '`' || character == '~' || character == '₩' || character == '\t' || character == '\n' || character == '\r')
            {
                return true;
            }

            switch (keyEvent.keyCode)
            {
                case KeyCode.DownArrow:
                    MoveHighlight(1);
                    return true;
                case KeyCode.UpArrow:
                    MoveHighlight(-1);
                    return true;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    if (_activeRowCount > 0)
                    {
                        ActivateRow(_rows[_highlight >= 0 ? _highlight : 0]);
                    }

                    return true;
                case KeyCode.Tab:
                case KeyCode.Escape:
                case KeyCode.BackQuote:
                    return true;
                default:
                    return false;
            }
        }

        private void MoveHighlight(int delta)
        {
            if (_activeRowCount == 0)
            {
                return;
            }

            if (_highlight >= 0 && _highlight < _activeRowCount)
            {
                _rows[_highlight].SetHighlighted(false);
            }

            _highlight = _highlight < 0 ? (delta > 0 ? 0 : _activeRowCount - 1) : Mathf.Clamp(_highlight + delta, 0, _activeRowCount - 1);
            CheatRowView row = _rows[_highlight];
            row.SetHighlighted(true);
            ShowFooter(row.Definition);
            ScrollIntoView((RectTransform)row.transform);
        }

        private void ToggleAllGroups()
        {
            _model.ToggleAllGroups();
            _host.SaveMenuState();
            Rebuild();
        }

        private void Rebuild()
        {
            _model.Refresh();
            Render();
        }

        private void Render()
        {
            CheatDefinition highlighted = _highlight >= 0 && _highlight < _activeRowCount ? _rows[_highlight].Definition : null;
            HidePopup();
            RenderCategories();
            RenderEntries(highlighted);
            if (_pickerDefinitionOrNull != null && FindPickerWidgetOrNull(out _) == null)
            {
                ClosePicker();
            }
        }

        private void RenderCategories()
        {
            IReadOnlyList<MenuCategory> categories = _model.Categories;
            for (int index = 0; index < categories.Count; index++)
            {
                if (index >= _categoryRows.Count)
                {
                    CategoryRowView created = _canvas.Create(_categoryPrefab, _categoryContent);
                    RectTransform rect = (RectTransform)created.transform;
                    rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -index * ZRayTheme.ROW_HEIGHT);
                    _categoryRows.Add(created);
                }

                CategoryRowView row = _categoryRows[index];
                row.gameObject.SetActive(true);
                row.Set(categories[index], categories[index].Key == _model.Category, OnCategoryClicked);
            }

            for (int index = categories.Count; index < _categoryRows.Count; index++)
            {
                _categoryRows[index].gameObject.SetActive(false);
            }

            _categoryContent.sizeDelta = new Vector2(_categoryContent.sizeDelta.x, categories.Count * ZRayTheme.ROW_HEIGHT);
        }

        private void OnCategoryClicked(string key)
        {
            _model.Category = key;
            _host.SaveMenuState();
            _highlight = -1;
            Rebuild();
            _listScroll.verticalNormalizedPosition = 1.0f;
            _bFocusPending = true;
        }

        private void OnHeaderClicked(string key)
        {
            _model.ToggleCollapsed(key);
            _host.SaveMenuState();
            Rebuild();
        }

        private void RenderEntries(CheatDefinition highlightedOrNull)
        {
            IReadOnlyList<MenuEntry> entries = _model.Entries;
            int rowCount = 0;
            int headerCount = 0;
            float y = 0.0f;
            _highlight = -1;
            for (int index = 0; index < entries.Count; index++)
            {
                MenuEntry entry = entries[index];
                RectTransform rect;
                float height;
                if (entry.IsGroup)
                {
                    if (headerCount >= _headers.Count)
                    {
                        _headers.Add(_canvas.Create(_headerPrefab, _listContent));
                    }

                    GroupHeaderView header = _headers[headerCount++];
                    header.gameObject.SetActive(true);
                    header.Set(entry, OnHeaderClicked);
                    rect = (RectTransform)header.transform;
                    height = ZRayTheme.GROUP_HEIGHT;
                }
                else
                {
                    if (rowCount >= _rows.Count)
                    {
                        _rows.Add(_canvas.Create(_rowPrefab, _listContent));
                    }

                    CheatDefinition definition = entry.DefinitionOrNull;
                    CheatRowView row = _rows[rowCount];
                    row.gameObject.SetActive(true);
                    row.Bind(this, _canvas, definition, _model.Query, _model.IsFavorite(definition), _model.IsExpanded(definition), _model.GetDrafts(definition));
                    bool bHighlighted = _highlight < 0 && definition == highlightedOrNull;
                    if (bHighlighted)
                    {
                        _highlight = rowCount;
                    }

                    row.SetHighlighted(bHighlighted);
                    rect = (RectTransform)row.transform;
                    height = row.Height;
                    rowCount++;
                }

                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -y);
                rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
                y += height;
            }

            for (int index = rowCount; index < _rows.Count; index++)
            {
                _rows[index].gameObject.SetActive(false);
            }

            for (int index = headerCount; index < _headers.Count; index++)
            {
                _headers[index].gameObject.SetActive(false);
            }

            _activeRowCount = rowCount;
            _listContent.sizeDelta = new Vector2(_listContent.sizeDelta.x, y);
            _empty.gameObject.SetActive(entries.Count == 0);
            _empty.text = _model.IsSearching ? $"'{_model.Query}'와 일치하는 치트가 없습니다." : "여기에 보일 치트가 없습니다.";
        }

        private void RefreshPopup(CheatRowView row, ValueWidget widget)
        {
            _popupWidget = widget;
            _popupRow = row;
            _popupSelected = -1;
            if (_bPopupDismissed || !_bPopupRequested || !widget.IsEditing)
            {
                _popup.Hide();
                return;
            }

            _previousArgs.Clear();
            for (int index = 0; index < widget.ParameterIndex && index < row.Drafts.Count; index++)
            {
                _previousArgs.Add(row.Drafts[index]);
            }

            _popupSuggestions = _host.Assist.SuggestArguments(row.Definition, widget.ParameterIndex, widget.Text, _previousArgs.ToArray());
            if (_popupSuggestions.Count == 0 || (_popupSuggestions.Count == 1 && _popupSuggestions[0].Insert == widget.Text))
            {
                _popup.Hide();
                return;
            }

            _popup.Show(_popupSuggestions, _popupSelected);
            PlacePopupUnder(widget.FieldRect);
        }

        private void ApplyPopup(int index)
        {
            if (_popupWidget == null || _popupSuggestions == null || index < 0 || index >= _popupSuggestions.Count)
            {
                return;
            }

            ValueWidget widget = _popupWidget;
            CheatRowView row = _popupRow;
            widget.SetText(_popupSuggestions[index].Insert);
            row.OnWidgetChanged(widget);
            _bPopupDismissed = true;
            _popup.Hide();
            widget.Focus();
        }

        private void OnPopupClicked(int index)
        {
            ApplyPopup(index);
        }

        private void HidePopup()
        {
            _popup.Hide();
            _popupWidget = null;
            _popupRow = null;
            _popupSelected = -1;
            _bPopupDismissed = false;
            _bPopupRequested = false;
        }

        private void OnPickerChanged(Color32 color)
        {
            ValueWidget widget = FindPickerWidgetOrNull(out CheatRowView row);
            if (widget == null)
            {
                ClosePicker();
                return;
            }

            _bPickerApplying = true;
            widget.SetText(ColorValueType.FormatColor(color));
            row.OnWidgetChanged(widget);
            _bPickerApplying = false;
        }

        private void OnPickerReleased()
        {
            ValueWidget widget = FindPickerWidgetOrNull(out CheatRowView row);
            if (widget != null)
            {
                row.OnWidgetCommitted(widget);
            }
        }

        private void ClosePicker()
        {
            _pickerDefinitionOrNull = null;
            if (_picker != null)
            {
                _picker.Hide();
            }
        }

        private ValueWidget FindPickerWidgetOrNull(out CheatRowView rowOrNull)
        {
            rowOrNull = _pickerDefinitionOrNull == null ? null : FindRowOrNull(_pickerDefinitionOrNull);
            ValueWidget widget = rowOrNull == null ? null : rowOrNull.FindWidgetOrNull(_pickerParameterIndex);
            return widget != null && widget.IsColor ? widget : null;
        }

        private void PlacePicker(RectTransform anchor)
        {
            Vector3[] corners = new Vector3[4];
            anchor.GetWorldCorners(corners);
            RectTransform panel = _picker.Panel;
            RectTransform parent = (RectTransform)panel.parent;
            Vector3 below = parent.InverseTransformPoint(corners[3]);
            Vector3 above = parent.InverseTransformPoint(corners[2]);
            Rect bounds = parent.rect;
            Vector2 size = panel.rect.size;
            float x = Mathf.Clamp(below.x, bounds.xMin + size.x, bounds.xMax);
            float y = below.y - PICKER_GAP;
            if (y - size.y < bounds.yMin)
            {
                y = above.y + PICKER_GAP + size.y;
            }

            panel.localPosition = new Vector3(Mathf.Round(x), Mathf.Round(Mathf.Min(y, bounds.yMax)), 0.0f);
        }

        private void PlacePopupUnder(RectTransform field)
        {
            Vector3[] corners = new Vector3[4];
            field.GetWorldCorners(corners);
            RectTransform parent = (RectTransform)_popup.Panel.parent;
            Vector3 local = parent.InverseTransformPoint(corners[0]);
            _popup.Panel.localPosition = new Vector3(Mathf.Round(local.x), Mathf.Round(local.y), 0.0f);
        }

        private void ScrollIntoView(RectTransform row)
        {
            float top = -row.anchoredPosition.y;
            float bottom = top + row.sizeDelta.y;
            float viewHeight = _listScroll.viewport.rect.height;
            float scrolled = _listContent.anchoredPosition.y;
            if (top < scrolled)
            {
                scrolled = top;
            }
            else if (bottom > scrolled + viewHeight)
            {
                scrolled = bottom - viewHeight;
            }

            _listScroll.StopMovement();
            _listContent.anchoredPosition = new Vector2(_listContent.anchoredPosition.x, scrolled);
        }

        private ValueWidget FindEditingWidgetOrNull(out CheatRowView rowOrNull)
        {
            for (int index = 0; index < _activeRowCount; index++)
            {
                ValueWidget widget = _rows[index].FindEditingWidgetOrNull();
                if (widget != null)
                {
                    rowOrNull = _rows[index];
                    return widget;
                }
            }

            rowOrNull = null;
            return null;
        }

        private CheatRowView FindRowOrNull(CheatDefinition definition)
        {
            for (int index = 0; index < _activeRowCount; index++)
            {
                if (_rows[index].Definition == definition)
                {
                    return _rows[index];
                }
            }

            return null;
        }
    }
}
