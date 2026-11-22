using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Lumera.DebuggerKit.Editor
{
    // 에디터 창의 치트 메뉴. 무엇을 보일지는 CheatMenuModel이 정하고, 실행은 런타임 메뉴와 같은 경로(DebuggerKitHost.ExecuteFromMenu)로 한다.
    internal sealed class CheatMenuPane
    {
        private const double POLL_SECONDS = 0.25;

        private readonly ScrollView _categories = new ScrollView();
        private readonly ToolbarSearchField _search = new ToolbarSearchField();
        private readonly ScrollView _list = new ScrollView();
        private readonly Label _footer = new Label();
        private readonly List<ValueRow> _valueRows = new List<ValueRow>();
        private readonly StringBuilder _builder = new StringBuilder();
        private DebuggerKitHost _hostOrNull;
        private CheatMenuModel _model;
        private int _shownVersion = -1;
        private double _nextPollAt;

        public VisualElement Root { get; } = new VisualElement();

        // 값 치트 한 행. 화면에 보이는 동안 주기적으로 값을 다시 읽는다.
        private sealed class ValueRow
        {
            public CheatDefinition Definition;
            public System.Action<string> SetShown;
            public System.Func<bool> IsEditing;
        }

        public CheatMenuPane()
        {
            Root.AddToClassList("menu");
            _categories.AddToClassList("sidebar");
            _categories.mode = ScrollViewMode.Vertical;
            Root.Add(_categories);

            VisualElement listArea = new VisualElement();
            listArea.AddToClassList("list-area");
            VisualElement searchRow = new VisualElement();
            searchRow.AddToClassList("search-row");
            _search.AddToClassList("search");
            _search.style.flexGrow = 1.0f;
            _search.style.flexShrink = 1.0f;
            _search.style.width = StyleKeyword.Auto;
            _search.style.minWidth = 60.0f;
            _search.RegisterValueChangedCallback(evt => Refresh(() => _model.Query = evt.newValue));
            searchRow.Add(_search);
            Button collapseAll = new Button(ToggleAllGroups) { text = "접기" };
            collapseAll.AddToClassList("collapse-btn");
            collapseAll.style.flexShrink = 0.0f;
            searchRow.Add(collapseAll);
            listArea.Add(searchRow);
            // 가로로는 스크롤하지 않는다. 행은 창 폭에 맞춰 줄어든다.
            _list.mode = ScrollViewMode.Vertical;
            _list.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _list.AddToClassList("grow");
            listArea.Add(_list);
            _footer.AddToClassList("footer");
            _footer.text = "";
            listArea.Add(_footer);
            Root.Add(listArea);
        }

        public void Bind(DebuggerKitHost hostOrNull)
        {
            _hostOrNull = hostOrNull;
            _model = hostOrNull == null ? null : new CheatMenuModel(hostOrNull.Registry, hostOrNull.Favorites, hostOrNull.History, hostOrNull.Drafts);
            _model?.Refresh();
            if (_model != null)
            {
                _model.Query = _search.value;
            }

            _shownVersion = -1;
            _categories.Clear();
            _list.Clear();
            _valueRows.Clear();
        }

        public void Tick()
        {
            if (_model == null)
            {
                return;
            }

            _model.Refresh();
            if (_shownVersion != _model.Version)
            {
                Render();
            }

            if (EditorApplication.timeSinceStartup >= _nextPollAt)
            {
                _nextPollAt = EditorApplication.timeSinceStartup + POLL_SECONDS;
                foreach (ValueRow row in _valueRows)
                {
                    if (!row.IsEditing())
                    {
                        row.SetShown(ReadValueOrEmpty(row.Definition));
                    }
                }
            }
        }

        private void Refresh(System.Action change)
        {
            if (_model == null)
            {
                return;
            }

            change();
            _model.Refresh();
            Render();
        }

        private void Render()
        {
            _shownVersion = _model.Version;
            RenderCategories();
            RenderList();
        }

        private void RenderCategories()
        {
            _categories.Clear();
            foreach (MenuCategory category in _model.Categories)
            {
                VisualElement row = new VisualElement();
                row.AddToClassList("category");
                row.EnableInClassList("selected", category.Key == _model.Category);
                Label title = new Label(category.Key == CheatMenuModel.FAVORITES_KEY ? "★ " + category.Title : category.Title);
                title.AddToClassList("category-title");
                title.style.paddingLeft = 8 + (category.Depth * 12);
                if (category.Depth > 0)
                {
                    title.AddToClassList("muted");
                }

                Label count = new Label(category.Count.ToString());
                count.AddToClassList("category-count");
                row.Add(title);
                row.Add(count);
                string key = category.Key;
                row.RegisterCallback<ClickEvent>(_ => Refresh(() => _model.Category = key));
                _categories.Add(row);
            }
        }

        private void RenderList()
        {
            _list.Clear();
            _valueRows.Clear();
            IReadOnlyList<MenuEntry> entries = _model.Entries;
            if (entries.Count == 0)
            {
                Label empty = new Label(_model.IsSearching ? $"'{_model.Query}'와 일치하는 치트가 없습니다." : "여기에 보일 치트가 없습니다.");
                empty.AddToClassList("empty");
                _list.Add(empty);
                return;
            }

            foreach (MenuEntry entry in entries)
            {
                _list.Add(entry.IsGroup ? BuildGroup(entry) : BuildRow(entry.DefinitionOrNull));
            }
        }

        private VisualElement BuildGroup(MenuEntry entry)
        {
            VisualElement group = new VisualElement();
            group.AddToClassList("group");
            string star = entry.GroupKey == CheatMenuModel.FAVORITES_KEY ? "★ " : "";
            Label title = new Label((entry.IsGroupCollapsed ? "▸ " : "▾ ") + star + entry.GroupTitle);
            title.AddToClassList("group-title");
            Label count = new Label(entry.GroupCount.ToString());
            count.AddToClassList("group-count");
            group.Add(title);
            group.Add(count);
            string key = entry.GroupKey;
            group.RegisterCallback<ClickEvent>(_ => Refresh(() => _model.ToggleCollapsed(key)));
            return group;
        }

        private VisualElement BuildRow(CheatDefinition definition)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("row");
            row.RegisterCallback<MouseEnterEvent>(_ => ShowFooter(definition));
            VisualElement main = new VisualElement();
            main.AddToClassList("row-main");
            row.Add(main);

            bool bFavorite = _model.IsFavorite(definition);
            Button star = new Button(() => ToggleFavorite(definition)) { text = bFavorite ? "★" : "☆" };
            star.AddToClassList("star");
            star.EnableInClassList("on", bFavorite);
            main.Add(star);

            Label title = new Label(BuildTitle(definition));
            title.AddToClassList("row-title");
            main.Add(title);
            Label status = new Label();
            status.AddToClassList("row-status");
            main.Add(status);

            // 행을 눌렀을 때의 주 동작. 런타임 메뉴와 같다: 실행, 입력칸 펴기, 스위치 뒤집기.
            System.Action primaryOrNull = null;
            if (definition.Kind == ECheatKind.Value)
            {
                VisualElement value = BuildValue(definition, status);
                main.Add(value);
                Toggle toggleOrNull = value.Q<Toggle>();
                if (toggleOrNull != null)
                {
                    primaryOrNull = () => toggleOrNull.value = !toggleOrNull.value;
                }
            }
            else if (!definition.HasParameters)
            {
                primaryOrNull = () => Run(definition, _model.BuildLine(definition), status);
                Button run = new Button(primaryOrNull) { text = "실행" };
                run.AddToClassList("run");
                main.Add(run);
            }
            else
            {
                bool bExpanded = _model.IsExpanded(definition);
                primaryOrNull = () => Refresh(() => _model.ToggleExpanded(definition));
                Button edit = new Button(primaryOrNull) { text = bExpanded ? "접기" : "입력" };
                edit.AddToClassList("edit");
                main.Add(edit);
                if (bExpanded)
                {
                    row.Add(BuildEditor(definition, status));
                }
            }

            // 버튼과 입력칸이 아닌 곳(행의 빈 곳, 이름)을 눌렀을 때만 받는다.
            if (primaryOrNull != null)
            {
                main.RegisterCallback<ClickEvent>(evt =>
                {
                    if (evt.target == main || evt.target == title || evt.target == status)
                    {
                        primaryOrNull();
                    }
                });
            }

            return row;
        }

        // 펼친 치트의 인자 입력칸. 입력한 값은 모델의 초안에 남아서 접었다 펴도 그대로다.
        private VisualElement BuildEditor(CheatDefinition definition, Label status)
        {
            VisualElement editor = new VisualElement();
            editor.AddToClassList("editor");
            string[] drafts = _model.GetDrafts(definition);
            for (int index = 0; index < definition.Parameters.Count; index++)
            {
                CheatParameter parameter = definition.Parameters[index];
                int captured = index;
                VisualElement arg = new VisualElement();
                arg.AddToClassList("arg");
                string range = CheatFormatter.FormatRange(parameter);
                Label label = new Label(range.Length == 0 ? parameter.DisplayName : $"{parameter.DisplayName}  {range}");
                label.AddToClassList("arg-label");
                label.EnableInClassList("required", parameter.IsRequired);
                label.tooltip = parameter.Description;
                arg.Add(label);
                arg.Add(BuildInput(definition, parameter, drafts[index], text => drafts[captured] = text, () => Run(definition, _model.BuildLine(definition), status), captured, drafts));
                editor.Add(arg);
            }

            Button run = new Button(() => Run(definition, _model.BuildLine(definition), status)) { text = "실행" };
            run.AddToClassList("run");
            run.AddToClassList("arg-run");
            editor.Add(run);
            return editor;
        }

        // 인자 하나의 입력. bool은 토글, 열거형은 드롭다운, 그 밖은 글자 입력(숫자는 ±, 후보가 있으면 ▾ 목록).
        private VisualElement BuildInput(CheatDefinition definition, CheatParameter parameter, string text, System.Action<string> changed, System.Action submit, int index, string[] drafts)
        {
            VisualElement value = new VisualElement();
            value.AddToClassList("value");
            List<string> choices = new List<string>();
            parameter.ValueType.AddCandidates(choices);
            if (parameter.Type == typeof(bool))
            {
                Toggle toggle = new Toggle { value = text == "true" };
                toggle.RegisterValueChangedCallback(evt => changed(evt.newValue ? "true" : "false"));
                value.Add(toggle);
                return value;
            }

            if (choices.Count > 0)
            {
                DropdownField dropdown = new DropdownField(choices, Mathf.Max(0, choices.IndexOf(text)));
                dropdown.AddToClassList("value-dropdown");
                changed(dropdown.value);
                dropdown.RegisterValueChangedCallback(evt => changed(evt.newValue));
                value.Add(dropdown);
                return value;
            }

            TextField field = new TextField { value = text };
            field.AddToClassList("value-field");
            field.RegisterValueChangedCallback(evt => changed(evt.newValue));
            field.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    submit();
                }
            });
            value.Add(field);
            if (parameter.IsNumber)
            {
                AddStepper(value, parameter, field);
            }

            if (parameter.SuggestionProviderOrNull != null)
            {
                Button pick = new Button { text = "▾", tooltip = "후보에서 고르기" };
                pick.AddToClassList("arg-pick");
                pick.clicked += () => ShowCandidates(definition, index, drafts, field);
                value.Add(pick);
            }

            return value;
        }

        // 값 치트의 위젯. 바꾸면 바로 쓴다(콘솔에서 "id 값"을 친 것과 같다).
        private VisualElement BuildValue(CheatDefinition definition, Label status)
        {
            CheatParameter parameter = definition.Parameters[0];
            string current = ReadValueOrEmpty(definition);
            VisualElement value = new VisualElement();
            value.AddToClassList("value");
            if (!definition.CanWrite)
            {
                Label shown = new Label(current);
                shown.AddToClassList("value-readonly");
                value.Add(shown);
                _valueRows.Add(new ValueRow { Definition = definition, SetShown = text => shown.text = text, IsEditing = () => false });
                return value;
            }

            List<string> choices = new List<string>();
            parameter.ValueType.AddCandidates(choices);
            if (parameter.Type == typeof(bool))
            {
                Toggle toggle = new Toggle { value = current == "true" };
                toggle.RegisterValueChangedCallback(evt => Run(definition, CheatMenuModel.BuildValueLine(definition, evt.newValue ? "true" : "false"), status));
                value.Add(toggle);
                _valueRows.Add(new ValueRow { Definition = definition, SetShown = text => toggle.SetValueWithoutNotify(text == "true"), IsEditing = () => false });
                return value;
            }

            if (choices.Count > 0)
            {
                DropdownField dropdown = new DropdownField(choices, Mathf.Max(0, choices.IndexOf(current)));
                dropdown.AddToClassList("value-dropdown");
                dropdown.RegisterValueChangedCallback(evt => Run(definition, CheatMenuModel.BuildValueLine(definition, evt.newValue), status));
                value.Add(dropdown);
                _valueRows.Add(new ValueRow { Definition = definition, SetShown = dropdown.SetValueWithoutNotify, IsEditing = () => false });
                return value;
            }

            // 글자는 Enter를 누르거나 칸을 떠날 때 쓴다(isDelayed).
            TextField field = new TextField { value = current, isDelayed = true };
            field.AddToClassList("value-field");
            field.RegisterValueChangedCallback(evt => Run(definition, CheatMenuModel.BuildValueLine(definition, evt.newValue), status));
            value.Add(field);
            if (parameter.IsNumber)
            {
                AddStepper(value, parameter, field);
            }

            if (parameter.SuggestionProviderOrNull != null)
            {
                Button pick = new Button { text = "▾", tooltip = "후보에서 고르기" };
                pick.AddToClassList("arg-pick");
                pick.clicked += () => ShowCandidates(definition, 0, new string[0], field);
                value.Add(pick);
            }

            _valueRows.Add(new ValueRow { Definition = definition, SetShown = field.SetValueWithoutNotify, IsEditing = () => field.focusController?.focusedElement == field });
            return value;
        }

        private static void AddStepper(VisualElement value, CheatParameter parameter, TextField field)
        {
            Button minus = new Button(() => field.value = parameter.StepText(field.value, -1)) { text = "-" };
            Button plus = new Button(() => field.value = parameter.StepText(field.value, 1)) { text = "+" };
            minus.AddToClassList("value-step");
            plus.AddToClassList("value-step");
            value.Add(minus);
            value.Add(plus);
        }

        // 콘솔과 같은 후보(공급자, 기본값)를 메뉴로 띄운다.
        private void ShowCandidates(CheatDefinition definition, int index, string[] drafts, TextField field)
        {
            string[] previous = new string[index];
            System.Array.Copy(drafts, previous, index);
            List<Suggestion> suggestions = _hostOrNull.Assist.SuggestArguments(definition, index, "", previous);
            GenericMenu menu = new GenericMenu();
            foreach (Suggestion suggestion in suggestions)
            {
                string insert = suggestion.Insert;
                string label = suggestion.Detail.Length > 0 ? $"{insert}  ({suggestion.Detail})" : insert;
                menu.AddItem(new GUIContent(label.Replace('/', '∕')), insert == field.value, () => field.value = insert);
            }

            if (suggestions.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("후보 없음"));
            }

            menu.ShowAsContext();
        }

        private void Run(CheatDefinition definition, string line, Label status)
        {
            ExecutionResult result = _hostOrNull.ExecuteFromMenu(line);
            status.text = result.IsSuccess ? "완료" : "실패";
            status.EnableInClassList("success", result.IsSuccess);
            status.EnableInClassList("error", !result.IsSuccess);
            status.tooltip = result.Message;
            _footer.text = result.Message.Replace('\n', ' ');
            _nextPollAt = 0.0;
        }

        private void ToggleFavorite(CheatDefinition definition)
        {
            _hostOrNull.Favorites.Toggle(definition.Id);
            _hostOrNull.SaveFavorites();
            Refresh(() => { });
        }

        // 묶음이 하나라도 펴져 있으면 모두 접고, 모두 접혀 있으면 모두 편다.
        private void ToggleAllGroups()
        {
            if (_model == null)
            {
                return;
            }

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

            Refresh(() => { });
        }

        private void ShowFooter(CheatDefinition definition)
        {
            _footer.text = definition.Description.Length == 0
                ? CheatFormatter.FormatSignature(definition)
                : $"{CheatFormatter.FormatSignature(definition)}  —  {definition.Description}";
        }

        // 이름(검색어 강조)과 흐린 id.
        private string BuildTitle(CheatDefinition definition)
        {
            string match = EditorGUIUtility.isProSkin ? "#FFD27A" : "#AA6400";
            string muted = EditorGUIUtility.isProSkin ? "#8C93A0" : "#646870";
            _builder.Clear();
            RichText.AppendHighlighted(_builder, definition.Name, TextMatcher.FindSpans(definition.Name, _model.Query), match);
            if (definition.Name != definition.Id)
            {
                _builder.Append("  <color=").Append(muted).Append('>');
                RichText.AppendHighlighted(_builder, definition.Id, TextMatcher.FindSpans(definition.Id, _model.Query), match);
                _builder.Append("</color>");
            }

            return _builder.ToString();
        }

        private static string ReadValueOrEmpty(CheatDefinition definition)
        {
            try
            {
                return definition.FormatValue();
            }
            catch (System.Exception)
            {
                return "";
            }
        }
    }
}
