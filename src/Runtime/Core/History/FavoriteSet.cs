using System;
using System.Collections.Generic;

namespace Lumera.DebuggerKit
{
    // 즐겨찾기한 치트 id. 더한 순서를 지킨다. 지금 등록되지 않은 id(다른 씬의 치트)도 지우지 않고 갖고 있는다.
    public sealed class FavoriteSet
    {
        private const string KEY = "Lumera.DebuggerKit.Favorites.v1";

        private readonly List<string> _ids = new List<string>();

        public IReadOnlyList<string> Ids => _ids;
        public int Version { get; private set; }

        public bool Contains(string id)
        {
            return IndexOf(id) >= 0;
        }

        // 있으면 빼고 없으면 더한다. 바뀐 뒤의 상태(즐겨찾기인가)를 돌려준다.
        public bool Toggle(string id)
        {
            int index = IndexOf(id);
            if (index >= 0)
            {
                _ids.RemoveAt(index);
            }
            else
            {
                _ids.Add(id);
            }

            Version++;
            return index < 0;
        }

        public void Load(IKeyValueStore store)
        {
            CommandHistory.ReadLines(store.GetStringOrNull(KEY), _ids, int.MaxValue);
            Version++;
        }

        public void Save(IKeyValueStore store)
        {
            store.SetString(KEY, string.Join("\n", _ids));
        }

        private int IndexOf(string id)
        {
            for (int index = 0; index < _ids.Count; index++)
            {
                if (string.Equals(_ids[index], id, StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }

            return -1;
        }
    }
}
