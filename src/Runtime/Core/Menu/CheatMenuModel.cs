using System;
using System.Collections.Generic;

namespace Lumera.ZRay
{
    // 치트 메뉴 왼쪽의 분류 한 줄.
    public readonly struct MenuCategory
    {
        public string Key { get; }
        public string Title { get; }
        // '/'로 나눈 경로의 깊이(최상위가 0)
        public int Depth { get; }
        public int Count { get; }

        public MenuCategory(string key, string title, int depth, int count)
        {
            Key = key;
            Title = title;
            Depth = depth;
            Count = count;
        }
    }

    // 치트 메뉴 오른쪽 목록의 한 줄. 묶음 제목이거나 치트 하나다.
    public readonly struct MenuEntry
    {
        // 치트 줄이면 정의, 묶음 제목이면 null
        public CheatDefinition? DefinitionOrNull { get; }
        public string GroupKey { get; }
        public string GroupTitle { get; }
        public int GroupCount { get; }
        public bool IsGroupCollapsed { get; }

        public bool IsGroup => DefinitionOrNull == null;

        private MenuEntry(CheatDefinition? definitionOrNull, string groupKey, string groupTitle, int groupCount, bool bGroupCollapsed)
        {
            DefinitionOrNull = definitionOrNull;
            GroupKey = groupKey;
            GroupTitle = groupTitle;
            GroupCount = groupCount;
            IsGroupCollapsed = bGroupCollapsed;
        }

        public static MenuEntry Cheat(CheatDefinition definition)
        {
            return new MenuEntry(definition, "", "", 0, false);
        }

        public static MenuEntry Group(string key, string title, int count, bool bCollapsed)
        {
            return new MenuEntry(null, key, title, count, bCollapsed);
        }
    }

    // 치트 메뉴에 보일 것을 정한다: 분류 목록, 고른 분류와 검색어에 맞는 목록, 접힌 묶음, 펼친 치트, 입력 중인 인자 값.
    // 화면을 모른다. 런타임 메뉴와 에디터 창이 각자 하나씩 갖고 Entries를 그린다.
    // 등록된 정의에서 모두 만들어지므로, 치트를 더해도 이 클래스와 화면은 고칠 것이 없다.
    public sealed class CheatMenuModel
    {
        public const string ALL_KEY = "";
        // 분류 이름과 겹치지 않게 제어 문자로 시작한다.
        public const string FAVORITES_KEY = "\u0001favorites";
        public const string RECENT_KEY = "\u0001recent";
        public const string PROPERTIES_KEY = "\u0001properties";
        public const string ALL_TITLE = "전체";
        public const string FAVORITES_TITLE = "즐겨찾기";
        public const string RECENT_TITLE = "최근 사용";
        public const string PROPERTIES_TITLE = "프로퍼티";

        private static readonly Comparison<KeyValuePair<int, CheatDefinition>> RANK_ORDER = static (left, right) => left.Key.CompareTo(right.Key);

        private readonly CheatRegistry _registry;
        private readonly FavoriteSet _favorites;
        private readonly CommandHistory _history;
        private readonly List<MenuCategory> _categories = new List<MenuCategory>();
        private readonly List<MenuEntry> _entries = new List<MenuEntry>();
        private readonly List<CheatDefinition> _shown = new List<CheatDefinition>();
        private readonly List<string> _paths = new List<string>();
        private readonly List<KeyValuePair<int, CheatDefinition>> _ranked = new List<KeyValuePair<int, CheatDefinition>>();
        private readonly HashSet<string> _collapsed = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _expanded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // 치트마다 인자 입력칸에 쓰던 글자. 호스트의 것을 받으면 런타임 메뉴, 에디터 창, 콘솔 실행이 같은 값을 본다.
        private readonly CheatDraftStore _drafts;
        private string _category = ALL_KEY;
        private string _query = "";
        private bool _bDirty = true;
        private int _registryVersion = -1;
        private int _favoritesVersion = -1;
        private int _recentVersion = -1;

        public IReadOnlyList<MenuCategory> Categories => _categories;
        public IReadOnlyList<MenuEntry> Entries => _entries;
        // 목록을 다시 만들 때마다 오른다. 화면은 이 값이 바뀌었을 때만 다시 그린다.
        public int Version { get; private set; }

        public string Category
        {
            get => _category;
            set
            {
                if (_category != value)
                {
                    _category = value ?? ALL_KEY;
                    _bDirty = true;
                }
            }
        }

        public string Query
        {
            get => _query;
            set
            {
                string trimmed = (value ?? "").Trim();
                if (_query != trimmed)
                {
                    _query = trimmed;
                    _bDirty = true;
                }
            }
        }

        public bool IsSearching => _query.Length > 0;

        // draftsOrNull: 다른 화면과 함께 쓸 입력값 보관소. 없으면 이 모델만의 것을 만든다.
        public CheatMenuModel(CheatRegistry registry, FavoriteSet favorites, CommandHistory history, CheatDraftStore? draftsOrNull = null)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _favorites = favorites ?? throw new ArgumentNullException(nameof(favorites));
            _history = history ?? throw new ArgumentNullException(nameof(history));
            _drafts = draftsOrNull ?? new CheatDraftStore();
        }

        // 바뀐 것이 있으면 목록을 다시 만들고 true를 돌려준다. 화면은 매 프레임 불러도 된다.
        // 판이 바뀌어 치트가 다시 등록되거나, 콘솔에서 치트를 써서 최근 사용이 바뀌는 것도 여기서 알아챈다.
        public bool Refresh()
        {
            if (!_bDirty && _registryVersion == _registry.Version && _favoritesVersion == _favorites.Version && _recentVersion == _history.RecentVersion)
            {
                return false;
            }

            _bDirty = false;
            _registryVersion = _registry.Version;
            _favoritesVersion = _favorites.Version;
            _recentVersion = _history.RecentVersion;
            BuildCategories();
            BuildEntries();
            Version++;
            return true;
        }

        public bool IsFavorite(CheatDefinition definition)
        {
            return _favorites.Contains(definition.Id);
        }

        public bool IsExpanded(CheatDefinition definition)
        {
            return _expanded.Contains(definition.Id);
        }

        // 인자 있는 치트의 입력칸을 펴거나 접는다. 바뀐 뒤의 상태를 돌려준다.
        public bool ToggleExpanded(CheatDefinition definition)
        {
            bool bExpand = !_expanded.Remove(definition.Id);
            if (bExpand)
            {
                _expanded.Add(definition.Id);
            }

            _bDirty = true;
            return bExpand;
        }

        public void ToggleCollapsed(string groupKey)
        {
            if (!_collapsed.Remove(groupKey))
            {
                _collapsed.Add(groupKey);
            }

            _bDirty = true;
        }

        // 치트의 인자마다 입력 중인 글자. 처음에는 인자의 기본값으로 채운다(그대로 실행하면 기본값이 쓰인다).
        // 돌려받은 배열을 고치면 그대로 남는다.
        public string[] GetDrafts(CheatDefinition definition)
        {
            return _drafts.Get(definition);
        }

        // 입력 중인 글자로 실행할 명령 줄을 만든다. 비워 둔 뒤쪽 인자는 생략해서 기본값이 쓰이게 한다.
        public string BuildLine(CheatDefinition definition)
        {
            string[] drafts = GetDrafts(definition);
            int count = drafts.Length;
            while (count > 0 && drafts[count - 1].Length == 0 && !definition.Parameters[count - 1].IsRequired)
            {
                count--;
            }

            string[] arguments = new string[count];
            Array.Copy(drafts, arguments, count);
            return CheatExecutor.BuildLine(definition, arguments);
        }

        // 값 치트에 값을 쓰는 명령 줄.
        public static string BuildValueLine(CheatDefinition definition, string value)
        {
            return CheatExecutor.BuildLine(definition, new[] { value });
        }

        // 분류를 고르면 그 하위 분류의 치트도 함께 보인다.
        public static bool IsInCategory(CheatDefinition definition, string path)
        {
            string category = definition.Category;
            return path.Length == 0 || category == path
                || (category.Length > path.Length && category[path.Length] == '/' && category.StartsWith(path, StringComparison.Ordinal));
        }

        // 고정 항목 셋(전체, 즐겨찾기, 최근 사용)과, 등록된 분류 경로(상위 경로 포함).
        private void BuildCategories()
        {
            IReadOnlyList<CheatDefinition> definitions = _registry.Definitions;
            _paths.Clear();
            int total = 0;
            for (int index = 0; index < definitions.Count; index++)
            {
                if (!definitions[index].IsShownInMenu)
                {
                    continue;
                }

                total++;
                string path = definitions[index].Category;
                for (int slash = path.IndexOf('/'); slash >= 0; slash = path.IndexOf('/', slash + 1))
                {
                    AddPath(path.Substring(0, slash));
                }

                AddPath(path);
            }

            _paths.Sort(string.CompareOrdinal);
            // 고른 분류가 사라졌으면(판이 바뀌어 치트가 빠짐) 전체로 돌아간다.
            if (_category != ALL_KEY && _category != FAVORITES_KEY && _category != RECENT_KEY && _category != PROPERTIES_KEY && !_paths.Contains(_category))
            {
                _category = ALL_KEY;
            }

            _categories.Clear();
            _categories.Add(new MenuCategory(ALL_KEY, ALL_TITLE, 0, total));
            _categories.Add(new MenuCategory(FAVORITES_KEY, FAVORITES_TITLE, 0, CountShown(_favorites.Ids)));
            _categories.Add(new MenuCategory(RECENT_KEY, RECENT_TITLE, 0, CountShown(_history.RecentCheatIds)));
            _categories.Add(new MenuCategory(PROPERTIES_KEY, PROPERTIES_TITLE, 0, CountProperties()));
            for (int index = 0; index < _paths.Count; index++)
            {
                string path = _paths[index];
                int depth = 0;
                int nameStart = 0;
                for (int position = 0; position < path.Length; position++)
                {
                    if (path[position] == '/')
                    {
                        depth++;
                        nameStart = position + 1;
                    }
                }

                _categories.Add(new MenuCategory(path, path.Substring(nameStart), depth, CountInCategory(path)));
            }
        }

        private void AddPath(string path)
        {
            if (!_paths.Contains(path))
            {
                _paths.Add(path);
            }
        }

        // 고른 분류와 검색어로 보일 줄을 순서대로 만든다.
        private void BuildEntries()
        {
            _entries.Clear();
            _shown.Clear();
            if (_category == FAVORITES_KEY)
            {
                CollectShown(_favorites.Ids, _shown);
            }
            else if (_category == RECENT_KEY)
            {
                CollectShown(_history.RecentCheatIds, _shown);
            }
            else if (_category == PROPERTIES_KEY)
            {
                IReadOnlyList<CheatDefinition> definitions = _registry.Definitions;
                for (int index = 0; index < definitions.Count; index++)
                {
                    if (definitions[index].IsShownInMenu && definitions[index].Kind == ECheatKind.Value)
                    {
                        _shown.Add(definitions[index]);
                    }
                }
            }
            else
            {
                IReadOnlyList<CheatDefinition> definitions = _registry.Definitions;
                for (int index = 0; index < definitions.Count; index++)
                {
                    if (definitions[index].IsShownInMenu && IsInCategory(definitions[index], _category))
                    {
                        _shown.Add(definitions[index]);
                    }
                }
            }

            if (IsSearching)
            {
                AddRanked();
                return;
            }

            if (_category == FAVORITES_KEY || _category == RECENT_KEY)
            {
                for (int index = 0; index < _shown.Count; index++)
                {
                    _entries.Add(MenuEntry.Cheat(_shown[index]));
                }

                return;
            }

            // 전체를 볼 때는 맨 위에 즐겨찾기 묶음을 둔다.
            if (_category == ALL_KEY)
            {
                List<CheatDefinition> favorites = new List<CheatDefinition>();
                CollectShown(_favorites.Ids, favorites);
                AddGroup(FAVORITES_KEY, FAVORITES_TITLE, favorites, 0, favorites.Count);
            }

            // 정의는 분류 순으로 정렬되어 있어서, 분류가 바뀌는 자리마다 묶음을 끊는다.
            int groupStart = 0;
            for (int index = 1; index <= _shown.Count; index++)
            {
                if (index == _shown.Count || _shown[index].Category != _shown[groupStart].Category)
                {
                    string category = _shown[groupStart].Category;
                    AddGroup(category, category.Replace("/", " / "), _shown, groupStart, index);
                    groupStart = index;
                }
            }
        }

        // 검색 중에는 묶음을 풀고 일치 순위대로 보인다(콘솔 자동완성과 같은 순위). 순위가 같으면 원래 순서를 지킨다.
        private void AddRanked()
        {
            _ranked.Clear();
            for (int index = 0; index < _shown.Count; index++)
            {
                int rank = CheatSearch.Rank(_shown[index], _query);
                if (rank != CheatSearch.NO_MATCH)
                {
                    _ranked.Add(new KeyValuePair<int, CheatDefinition>((rank * 1024) + index, _shown[index]));
                }
            }

            _ranked.Sort(RANK_ORDER);
            for (int index = 0; index < _ranked.Count; index++)
            {
                _entries.Add(MenuEntry.Cheat(_ranked[index].Value));
            }
        }

        private void AddGroup(string key, string title, List<CheatDefinition> definitions, int start, int end)
        {
            if (end <= start)
            {
                return;
            }

            bool bCollapsed = _collapsed.Contains(key);
            _entries.Add(MenuEntry.Group(key, title, end - start, bCollapsed));
            if (bCollapsed)
            {
                return;
            }

            for (int index = start; index < end; index++)
            {
                _entries.Add(MenuEntry.Cheat(definitions[index]));
            }
        }

        // 저장된 id 중 지금 등록되어 있고 메뉴에 보이는 것만, 저장된 순서대로.
        private void CollectShown(IReadOnlyList<string> ids, List<CheatDefinition> output)
        {
            for (int index = 0; index < ids.Count; index++)
            {
                CheatDefinition? definition = _registry.FindOrNull(ids[index]);
                if (definition != null && definition.IsShownInMenu)
                {
                    output.Add(definition);
                }
            }
        }

        private int CountShown(IReadOnlyList<string> ids)
        {
            int count = 0;
            for (int index = 0; index < ids.Count; index++)
            {
                CheatDefinition? definition = _registry.FindOrNull(ids[index]);
                if (definition != null && definition.IsShownInMenu)
                {
                    count++;
                }
            }

            return count;
        }

        private int CountInCategory(string path)
        {
            IReadOnlyList<CheatDefinition> definitions = _registry.Definitions;
            int count = 0;
            for (int index = 0; index < definitions.Count; index++)
            {
                if (definitions[index].IsShownInMenu && IsInCategory(definitions[index], path))
                {
                    count++;
                }
            }

            return count;
        }

        private int CountProperties()
        {
            IReadOnlyList<CheatDefinition> definitions = _registry.Definitions;
            int count = 0;
            for (int index = 0; index < definitions.Count; index++)
            {
                if (definitions[index].IsShownInMenu && definitions[index].Kind == ECheatKind.Value)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
