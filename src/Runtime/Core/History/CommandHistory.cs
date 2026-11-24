using System;
using System.Collections.Generic;

namespace Lumera.ZRay
{
    public interface IKeyValueStore
    {
        string? GetStringOrNull(string key);
        void SetString(string key, string value);
    }

    public sealed class CommandHistory
    {
        public const int MAX_LINES = 100;
        public const int MAX_RECENT_CHEATS = 12;

        private const string LINES_KEY = "Lumera.ZRay.History.v1";
        private const string RECENT_KEY = "Lumera.ZRay.RecentCheats.v1";

        private readonly List<string> _lines = new List<string>();
        private readonly List<string> _recentCheatIds = new List<string>();
        private int _cursor = -1;
        private string _draft = "";

        public IReadOnlyList<string> Lines => _lines;
        public IReadOnlyList<string> RecentCheatIds => _recentCheatIds;
        public bool IsNavigating => _cursor >= 0;
        public int NavigationNumber => IsNavigating ? _lines.Count - _cursor : 0;
        public int RecentVersion { get; private set; }

        public void Add(string line)
        {
            ResetNavigation();
            string trimmed = (line ?? "").Trim();
            if (trimmed.Length == 0 || (_lines.Count > 0 && _lines[_lines.Count - 1] == trimmed))
            {
                return;
            }

            _lines.Add(trimmed);
            if (_lines.Count > MAX_LINES)
            {
                _lines.RemoveAt(0);
            }
        }

        public void RecordCheatUse(string id)
        {
            for (int index = _recentCheatIds.Count - 1; index >= 0; index--)
            {
                if (string.Equals(_recentCheatIds[index], id, StringComparison.OrdinalIgnoreCase))
                {
                    _recentCheatIds.RemoveAt(index);
                }
            }

            _recentCheatIds.Insert(0, id);
            if (_recentCheatIds.Count > MAX_RECENT_CHEATS)
            {
                _recentCheatIds.RemoveAt(_recentCheatIds.Count - 1);
            }

            RecentVersion++;
        }

        public bool TryNavigateOlder(string currentInput, out string line)
        {
            line = "";
            if (_lines.Count == 0)
            {
                return false;
            }

            if (!IsNavigating)
            {
                _draft = currentInput ?? "";
                _cursor = _lines.Count;
            }

            if (_cursor == 0)
            {
                return false;
            }

            _cursor--;
            line = _lines[_cursor];
            return true;
        }

        public bool TryNavigateNewer(out string line)
        {
            line = "";
            if (!IsNavigating)
            {
                return false;
            }

            _cursor++;
            if (_cursor >= _lines.Count)
            {
                line = _draft;
                ResetNavigation();
                return true;
            }

            line = _lines[_cursor];
            return true;
        }

        public void ResetNavigation()
        {
            _cursor = -1;
            _draft = "";
        }

        public void Load(IKeyValueStore store)
        {
            ResetNavigation();
            ReadLines(store.GetStringOrNull(LINES_KEY), _lines, MAX_LINES);
            ReadLines(store.GetStringOrNull(RECENT_KEY), _recentCheatIds, MAX_RECENT_CHEATS);
            RecentVersion++;
        }

        public void Save(IKeyValueStore store)
        {
            store.SetString(LINES_KEY, string.Join("\n", _lines));
            store.SetString(RECENT_KEY, string.Join("\n", _recentCheatIds));
        }

        internal static void ReadLines(string? textOrNull, List<string> output, int maxCount)
        {
            output.Clear();
            if (string.IsNullOrEmpty(textOrNull))
            {
                return;
            }

            string[] parts = textOrNull!.Split('\n');
            for (int index = 0; index < parts.Length && output.Count < maxCount; index++)
            {
                if (parts[index].Length > 0)
                {
                    output.Add(parts[index]);
                }
            }
        }
    }
}
