using System;
using System.Collections.Generic;

namespace Lumera.ZRay
{
    public readonly struct MenuCategory
    {
        public string Key { get; }
        public string Title { get; }
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

    public readonly struct MenuEntry
    {
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

    public sealed class CheatMenuModel
    {
        public const string ALL_KEY = "";
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
        private readonly HashSet<string> _expanded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly CheatDraftStore _drafts;
        private readonly CheatMenuState _state;
        private string _category;
        private string _query = "";
        private bool _bDirty = true;
        private int _registryVersion = -1;
        private int _favoritesVersion = -1;
        private int _recentVersion = -1;
        private int _stateVersion = -1;

        public IReadOnlyList<MenuCategory> Categories => _categories;
        public IReadOnlyList<MenuEntry> Entries => _entries;
        public int Version { get; private set; }

        public string Category
        {
            get => _category;
            set
            {
                string category = value ?? ALL_KEY;
                if (_category != category)
                {
                    _category = category;
                    _bDirty = true;
                }

                _state.Category = category;
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

        public CheatMenuModel(CheatRegistry registry, FavoriteSet favorites, CommandHistory history, CheatDraftStore? draftsOrNull = null, CheatMenuState? stateOrNull = null)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _favorites = favorites ?? throw new ArgumentNullException(nameof(favorites));
            _history = history ?? throw new ArgumentNullException(nameof(history));
            _drafts = draftsOrNull ?? new CheatDraftStore();
            _state = stateOrNull ?? new CheatMenuState();
            _category = _state.Category;
        }

        public bool Refresh()
        {
            if (!_bDirty && _registryVersion == _registry.Version && _favoritesVersion == _favorites.Version && _recentVersion == _history.RecentVersion
                && _stateVersion == _state.Version)
            {
                return false;
            }

            _bDirty = false;
            _registryVersion = _registry.Version;
            _favoritesVersion = _favorites.Version;
            _recentVersion = _history.RecentVersion;
            _stateVersion = _state.Version;
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
            _state.SetCollapsed(groupKey, !_state.IsCollapsed(groupKey));
        }

        public void ToggleAllGroups()
        {
            bool bAnyExpanded = false;
            for (int index = 0; index < _entries.Count; index++)
            {
                bAnyExpanded |= _entries[index].IsGroup && !_entries[index].IsGroupCollapsed;
            }

            for (int index = 0; index < _entries.Count; index++)
            {
                if (_entries[index].IsGroup)
                {
                    _state.SetCollapsed(_entries[index].GroupKey, bAnyExpanded);
                }
            }
        }

        public string[] GetDrafts(CheatDefinition definition)
        {
            return _drafts.Get(definition);
        }

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

        public static string BuildValueLine(CheatDefinition definition, string value)
        {
            return CheatExecutor.BuildLine(definition, new[] { value });
        }

        public static bool IsInCategory(CheatDefinition definition, string path)
        {
            string category = definition.Category;
            return path.Length == 0 || category == path
                || (category.Length > path.Length && category[path.Length] == '/' && category.StartsWith(path, StringComparison.Ordinal));
        }

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
            _category = _state.Category;
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

            if (_category == ALL_KEY)
            {
                List<CheatDefinition> favorites = new List<CheatDefinition>();
                CollectShown(_favorites.Ids, favorites);
                AddGroup(FAVORITES_KEY, FAVORITES_TITLE, favorites, 0, favorites.Count);
            }

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

            bool bCollapsed = _state.IsCollapsed(key);
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
