using System;
using System.Collections.Generic;

namespace Lumera.DebuggerKit
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

        private const string LINES_KEY = "Lumera.DebuggerKit.History.v1";
        private const string RECENT_KEY = "Lumera.DebuggerKit.RecentCheats.v1";

        // 오래된 줄이 앞, 새 줄이 뒤
        private readonly List<string> _lines = new List<string>();
        // 방금 쓴 치트가 앞
        private readonly List<string> _recentCheatIds = new List<string>();
        // 탐색 중인 줄의 번호. 탐색 중이 아니면 -1이다.
        private int _cursor = -1;
        private string _draft = "";

        public IReadOnlyList<string> Lines => _lines;
        public IReadOnlyList<string> RecentCheatIds => _recentCheatIds;
        public bool IsNavigating => _cursor >= 0;
        // 탐색 중일 때 "n/N"의 n(가장 새 줄이 1)
        public int NavigationNumber => IsNavigating ? _lines.Count - _cursor : 0;
        // 최근 치트 목록이 바뀔 때마다 오른다. 메뉴가 "최근 사용" 묶음을 다시 그릴지 판단한다.
        public int RecentVersion { get; private set; }

        // 실행한 줄을 더한다. 바로 앞 줄과 같으면 더하지 않는다. 탐색은 끝난다.
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

        // 한 줄 더 옛 기록으로 간다. 탐색을 시작할 때 작성 중이던 입력(currentInput)을 보관한다.
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

        // 한 줄 더 새 기록으로 간다. 가장 새 줄을 지나면 보관한 입력을 돌려주고 탐색을 끝낸다.
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
    }
}
