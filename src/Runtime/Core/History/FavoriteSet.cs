using System;
using System.Collections.Generic;

namespace Lumera.ZRay
{
    public sealed class FavoriteSet
    {
        private const string KEY = "Lumera.ZRay.Favorites.v1";

        private readonly List<string> _ids = new List<string>();

        public IReadOnlyList<string> Ids => _ids;
        public int Version { get; private set; }

        public bool Contains(string id)
        {
            return IndexOf(id) >= 0;
        }

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
