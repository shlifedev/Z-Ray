using System;
using System.Collections.Generic;

namespace Lumera.ZRay
{
    // 값을 글자로 남겨 두는 곳. Core는 저장 방법을 모른다(Unity 쪽은 PlayerPrefs로 구현한다).
    public interface IKeyValueStore
    {
        string? GetStringOrNull(string key);
        void SetString(string key, string value);
    }

    // 콘솔에 입력한 줄의 기록과 ↑/↓ 탐색 상태, 그리고 최근에 실행한 치트 id.
    // 최근 치트 id는 자동완성의 순위를 올리고 메뉴의 "최근 사용" 묶음을 만든다.
    public sealed class CommandHistory
    {
        public const int MAX_LINES = 100;
        public const int MAX_RECENT_CHEATS = 12;

        private const string LINES_KEY = "Lumera.ZRay.History.v1";
        private const string RECENT_KEY = "Lumera.ZRay.RecentCheats.v1";

        // 저장할 기록만 공유한다. 탐색 중에는 다른 콘솔이 줄을 추가하거나 지워도 같은 순서로 초안까지 돌아간다.
        private readonly HistoryEntries _entries;
        private readonly List<string> _navigationLines = new List<string>();
        // 탐색 중인 줄의 번호. 탐색 중이 아니면 -1이다.
        private int _cursor = -1;
        private string _draft = "";

        public IReadOnlyList<string> Lines => _entries.Lines;
        public IReadOnlyList<string> RecentCheatIds => _entries.RecentCheatIds;
        public bool IsNavigating => _cursor >= 0;
        // 탐색 중일 때 "n/N"의 n(가장 새 줄이 1)
        public int NavigationNumber => IsNavigating ? _navigationLines.Count - _cursor : 0;
        internal int NavigationCount => _navigationLines.Count;
        // 최근 치트 목록이 바뀔 때마다 오른다. 메뉴가 "최근 사용" 묶음을 다시 그릴지 판단한다.
        public int RecentVersion => _entries.RecentVersion;

        public CommandHistory() : this(new HistoryEntries())
        {
        }

        private CommandHistory(HistoryEntries entries)
        {
            _entries = entries;
        }

        internal CommandHistory CreateSessionHistory()
        {
            return new CommandHistory(_entries);
        }

        // 실행한 줄을 더한다. 바로 앞 줄과 같으면 더하지 않는다. 탐색은 끝난다.
        public void Add(string line)
        {
            ResetNavigation();
            string trimmed = (line ?? "").Trim();
            if (trimmed.Length == 0 || (_entries.Lines.Count > 0 && _entries.Lines[_entries.Lines.Count - 1] == trimmed))
            {
                return;
            }

            _entries.Lines.Add(trimmed);
            if (_entries.Lines.Count > MAX_LINES)
            {
                _entries.Lines.RemoveAt(0);
            }
        }

        public void RecordCheatUse(string id)
        {
            for (int index = _entries.RecentCheatIds.Count - 1; index >= 0; index--)
            {
                if (string.Equals(_entries.RecentCheatIds[index], id, StringComparison.OrdinalIgnoreCase))
                {
                    _entries.RecentCheatIds.RemoveAt(index);
                }
            }

            _entries.RecentCheatIds.Insert(0, id);
            if (_entries.RecentCheatIds.Count > MAX_RECENT_CHEATS)
            {
                _entries.RecentCheatIds.RemoveAt(_entries.RecentCheatIds.Count - 1);
            }

            _entries.RecentVersion++;
        }

        // 한 줄 더 옛 기록으로 간다. 탐색을 시작할 때 작성 중이던 입력(currentInput)을 보관한다.
        public bool TryNavigateOlder(string currentInput, out string line)
        {
            line = "";
            if (!IsNavigating)
            {
                if (_entries.Lines.Count == 0)
                {
                    return false;
                }

                _navigationLines.AddRange(_entries.Lines);
                _draft = currentInput ?? "";
                _cursor = _navigationLines.Count;
            }

            if (_cursor == 0)
            {
                return false;
            }

            _cursor--;
            line = _navigationLines[_cursor];
            return true;
        }

        // 한 줄 더 새 기록으로 간다. 가장 새 줄을 지나면 보관한 입력을 돌려주고 탐색을 끝낸다.
        public bool TryNavigateNewer(out string line)
        {
            line = "";
            if (!IsNavigating)
            {
                return false;
            }

            _cursor++;
            if (_cursor >= _navigationLines.Count)
            {
                line = _draft;
                ResetNavigation();
                return true;
            }

            line = _navigationLines[_cursor];
            return true;
        }

        public void ResetNavigation()
        {
            _navigationLines.Clear();
            _cursor = -1;
            _draft = "";
        }

        public void Load(IKeyValueStore store)
        {
            ResetNavigation();
            ReadLines(store.GetStringOrNull(LINES_KEY), _entries.Lines, MAX_LINES);
            ReadLines(store.GetStringOrNull(RECENT_KEY), _entries.RecentCheatIds, MAX_RECENT_CHEATS);
            _entries.RecentVersion++;
        }

        public void Save(IKeyValueStore store)
        {
            store.SetString(LINES_KEY, string.Join("\n", _entries.Lines));
            store.SetString(RECENT_KEY, string.Join("\n", _entries.RecentCheatIds));
        }

        // 입력 줄과 치트 id에는 줄바꿈이 들어갈 수 없어서 줄바꿈으로 나눠 저장한다.
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

        private sealed class HistoryEntries
        {
            // 오래된 줄이 앞, 새 줄이 뒤
            public readonly List<string> Lines = new List<string>();
            // 방금 쓴 치트가 앞
            public readonly List<string> RecentCheatIds = new List<string>();
            public int RecentVersion;
        }
    }
}
