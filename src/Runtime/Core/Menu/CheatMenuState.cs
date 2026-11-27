using System;
using System.Collections.Generic;

namespace Lumera.ZRay
{
    public sealed class CheatMenuState
    {
        private const string CATEGORY_KEY = "Lumera.ZRay.MenuCategory.v1";
        private const string COLLAPSED_KEY = "Lumera.ZRay.CollapsedGroups.v1";

        private readonly HashSet<string> _collapsed = new HashSet<string>(StringComparer.Ordinal);
        private string _category = CheatMenuModel.ALL_KEY;

        public int Version { get; private set; }

        public string Category
        {
            get => _category;
            set
            {
                string category = value ?? CheatMenuModel.ALL_KEY;
                if (_category != category)
                {
                    _category = category;
                    Version++;
                }
            }
        }

        public bool IsCollapsed(string groupKey)
        {
            return _collapsed.Contains(groupKey);
        }

        public void SetCollapsed(string groupKey, bool bCollapsed)
        {
            if (bCollapsed ? _collapsed.Add(groupKey) : _collapsed.Remove(groupKey))
            {
                Version++;
            }
        }

        public void Load(IKeyValueStore store)
        {
            _category = Uri.UnescapeDataString(store.GetStringOrNull(CATEGORY_KEY) ?? "");
            List<string> keys = new List<string>();
            CommandHistory.ReadLines(store.GetStringOrNull(COLLAPSED_KEY), keys, int.MaxValue);
            _collapsed.Clear();
            foreach (string key in keys)
            {
                _collapsed.Add(Uri.UnescapeDataString(key));
            }

            Version++;
        }

        public void Save(IKeyValueStore store)
        {
            List<string> keys = new List<string>(_collapsed.Count);
            foreach (string key in _collapsed)
            {
                keys.Add(Uri.EscapeDataString(key));
            }

            keys.Sort(string.CompareOrdinal);
            store.SetString(CATEGORY_KEY, Uri.EscapeDataString(_category));
            store.SetString(COLLAPSED_KEY, string.Join("\n", keys));
        }
    }
}
