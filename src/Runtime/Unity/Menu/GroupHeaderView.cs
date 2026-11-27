using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Lumera.ZRay
{
    public sealed class GroupHeaderView : MonoBehaviour
    {
        [SerializeField] private Button _button = null;
        [SerializeField] private TextMeshProUGUI _label = null;
        [SerializeField] private TextMeshProUGUI _count = null;

        private Action<string> _clickedOrNull;
        private string _key = "";

        public void Set(MenuEntry entry, Action<string> clicked)
        {
            if (_clickedOrNull == null)
            {
                _button.onClick.AddListener(OnClicked);
            }

            _key = entry.GroupKey;
            _clickedOrNull = clicked;
            bool bFavorites = entry.GroupKey == CheatMenuModel.FAVORITES_KEY;
            _label.text = (entry.IsGroupCollapsed ? "▸ " : "▾ ") + (bFavorites ? "★ " : "") + entry.GroupTitle;
            _count.text = entry.GroupCount.ToString();
        }

        private void OnClicked()
        {
            _clickedOrNull?.Invoke(_key);
        }
    }
}
