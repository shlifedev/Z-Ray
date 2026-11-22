using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Lumera.ZRay
{
    // 치트 메뉴 화면. 무엇을 보일지(분류, 묶음, 검색 순위, 펼친 치트, 입력 초안)는 CheatMenuModel이 정하고, 여기서는 그린다.
    // 행을 누르면 주 동작을 한다(실행, 입력칸 펴기, 스위치 뒤집기).
    // 키: 검색칸에서 ↑/↓로 행을 고르고 Enter로 실행하거나 편다.
    //     인자 입력칸에서는 Enter로 실행, ↑/↓로 후보를 고르고, Tab으로 다음 칸(Shift+Tab은 앞 칸)으로 간다.
    //     마지막 칸에서 Tab을 누르면 [실행] 버튼에 포커스가 가고, Space나 Enter로 실행한다.
    public sealed class CheatMenuView : MonoBehaviour
    {
        private const float POLL_SECONDS = 0.25f;

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

        private readonly List<CheatRowView> _rows = new List<CheatRowView>();
        private readonly List<GroupHeaderView> _headers = new List<GroupHeaderView>();
        private readonly List<CategoryRowView> _categoryRows = new List<CategoryRowView>();
        private readonly List<string> _previousArgs = new List<string>();
        private ZRayHost _host;
        private ZRayCanvas _canvas;
        private CheatMenuModel _model;
        // ↑/↓로 고른 행(_rows의 번호). 고른 것이 없으면 -1이다.
        private int _highlight = -1;
        private int _activeRowCount;
        private float _nextPollAt;
        private bool _bFocusPending;
        // 후보 목록을 띄운 입력칸과 그 행. 후보가 없으면 null이다.
        private ValueWidget _popupWidget;
        private CheatRowView _popupRow;
        private List<Suggestion> _popupSuggestions;
        private int _popupSelected = -1;
        private bool _bPopupDismissed;
        // 포커스를 받은 뒤로 사용자가 그 칸에 입력했다(또는 ↓로 후보를 청했다). 입력하기 전에는 후보를 띄우지 않는다.
        private bool _bPopupRequested;
        // 키보드 포커스가 [실행] 버튼에 있는 치트. 없으면 null이다.
        private CheatDefinition _executeFocusOrNull;
        private int _executeFocusFrame;
        // 입력칸이 키를 처리하는 도중에는 포커스를 옮기지 않고, 다음 Update에서 실행한다.
        private System.Action _deferredOrNull;
        private InputAction _confirm;
        private InputAction _tab;
        private InputAction _shift;

        public void Initialize(ZRayHost host, ZRayCanvas canvas)
        {
            _host = host;
            _canvas = canvas;
            _model = new CheatMenuModel(host.Registry, host.Favorites, host.History, host.Drafts);
            // [실행] 버튼에 포커스가 있을 때 쓰는 키. 입력칸에 포커스가 있을 때의 키는 입력칸이 직접 넘겨 준다(OnWidgetKey).
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
            _search.DeactivateInputField();
            gameObject.SetActive(false);
        }

        // 한 단계씩 닫는다: 후보 목록 → 인자 입력칸의 포커스 → 검색어. 닫을 것이 없으면 false를 돌려주고 호스트가 메뉴를 닫는다.
        public bool HandleEscape()
        {
            if (_popup.IsVisible)
            {
                // 어느 입력칸의 후보였는지는 기억해 둔다. 잊으면 다음 프레임에 새 입력칸으로 보고 다시 띄운다.
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

        // 행의 주 동작: 인자가 없으면 실행, 있으면 펴거나 접는다. 값 치트는 스위치면 뒤집고 아니면 입력칸에 포커스를 준다.
        // 행을 누르거나, 검색칸에서 고른 행에 Enter를 누르거나, 행의 버튼을 누를 때 불린다.
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

        // 행의 값으로 명령 줄을 만들어 콘솔과 같은 실행기로 실행한다. 결과는 토스트와 행, 콘솔 기록에 보인다.
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
        }

        // 인자 입력칸의 키. 후보 목록이 떠 있으면 Tab·↑/↓가 후보를 고르고, Enter는 고른 후보를 넣거나 실행한다.
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
                    // ↑/↓로 고른 후보가 있으면 넣고 넘어간다. Tab은 다음 입력칸으로, Shift+Tab은 앞 칸으로 간다.
                    // 마지막 칸에서는 [실행] 버튼으로 간다. 입력칸이 자기 키를 처리하는 중이라, 포커스는 다음 Update에서 옮긴다.
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
                    // 아직 입력하지 않아 후보가 떠 있지 않으면, ↓로 후보를 청할 수 있다(무엇을 넣을 수 있는지 볼 때).
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
                        // -1(고른 것 없음)을 포함해 한 바퀴 돈다.
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

            // 판이 바뀌어 치트가 다시 등록되거나, 콘솔에서 치트를 써서 최근 사용이 바뀌면 모델이 알아챈다.
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

            // 포커스가 다른 입력칸으로 옮겨 갔다. 새 칸의 후보는 사용자가 입력을 시작할 때까지 띄우지 않는다.
            ValueWidget editing = FindEditingWidgetOrNull(out CheatRowView editingRow);
            if (editing != _popupWidget)
            {
                HidePopup();
                _popupWidget = editing;
                _popupRow = editingRow;
            }
        }

        // 입력칸 사이를 옮긴다. 그쪽에 칸이 더 없으면, 앞으로 가던 중일 때만 [실행] 버튼으로 간다.
        private void MoveFocus(CheatRowView row, int fromIndex, int direction)
        {
            if (!row.FocusField(fromIndex, direction) && direction > 0)
            {
                SetExecuteFocus(row.Definition);
            }
        }

        // [실행] 버튼에 키보드 포커스를 둔다(null이면 뗀다). 입력칸은 포커스를 놓아 글자를 받지 않게 한다.
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

        // [실행] 버튼에 포커스가 있는 동안: Space/Enter는 실행, Tab은 첫 입력칸(Shift+Tab은 마지막 칸)으로.
        // 다른 입력칸이나 검색칸을 누르면 포커스가 그쪽으로 간 것이다.
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

            // 포커스를 옮긴 그 Tab이 같은 프레임에 한 번 더 읽히지 않게 한다.
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

        // 묶음이 하나라도 펴져 있으면 모두 접고, 모두 접혀 있으면 모두 편다.
        private void ToggleAllGroups()
        {
            bool bAnyExpanded = false;
            foreach (MenuEntry entry in _model.Entries)
            {
                bAnyExpanded |= entry.IsGroup && !entry.IsGroupCollapsed;
            }

            foreach (MenuEntry entry in new List<MenuEntry>(_model.Entries))
            {
                if (entry.IsGroup && entry.IsGroupCollapsed != bAnyExpanded)
                {
                    _model.ToggleCollapsed(entry.GroupKey);
                }
            }

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
            _highlight = -1;
            Rebuild();
            _listScroll.verticalNormalizedPosition = 1.0f;
            _bFocusPending = true;
        }

        private void OnHeaderClicked(string key)
        {
            _model.ToggleCollapsed(key);
            Rebuild();
        }

        // 줄마다 뷰를 붙이고 위에서부터 자리를 잡는다. 펼친 행은 그만큼 높다.
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
                    // 다시 그리기 전에 고른 행을 그대로 고른 채 둔다(즐겨찾기 묶음에 같은 치트가 또 있으면 먼저 나온 쪽).
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

            // 앞 인자에 따라 후보가 달라지는 공급자를 위해, 이 인자 앞의 값들을 함께 넘긴다.
            _previousArgs.Clear();
            for (int index = 0; index < widget.ParameterIndex && index < row.Drafts.Count; index++)
            {
                _previousArgs.Add(row.Drafts[index]);
            }

            _popupSuggestions = _host.Assist.SuggestArguments(row.Definition, widget.ParameterIndex, widget.Text, _previousArgs.ToArray());
            // 이미 그 값을 다 쳤으면 후보를 띄울 이유가 없다.
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

        // 후보 목록을 입력칸의 왼쪽 아래 모서리에 붙인다.
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
