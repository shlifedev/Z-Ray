using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Lumera.ZRay
{
    public sealed class CategoryRowView : MonoBehaviour
    {
        private const float INDENT = 12.0f;
        private const float LEFT_PADDING = 10.0f;

        [SerializeField] private Button _button = null;
        [SerializeField] private Image _marker = null;
        [SerializeField] private TextMeshProUGUI _label = null;
        [SerializeField] private TextMeshProUGUI _count = null;

        private Action<string> _clickedOrNull;
        private string _key = "";

        public void Set(MenuCategory category, bool bSelected, Action<string> clicked)
        {
            if (_clickedOrNull == null)
            {
                _button.onClick.AddListener(OnClicked);
            }

            _key = category.Key;
            _clickedOrNull = clicked;
            _label.text = category.Key == CheatMenuModel.FAVORITES_KEY ? "★ " + category.Title : category.Title;
            _label.margin = new Vector4(LEFT_PADDING + (category.Depth * INDENT), 0.0f, 0.0f, 0.0f);
            _label.color = bSelected ? ZRayTheme.TEXT : (category.Depth == 0 ? ZRayTheme.TEXT : ZRayTheme.MUTED);
            _count.text = category.Count.ToString();
            _button.image.color = bSelected ? ZRayTheme.ROW_SELECTED : ZRayTheme.TRANSPARENT;
            _marker.enabled = bSelected;
        }

        private void OnClicked()
        {
            _clickedOrNull?.Invoke(_key);
        }
    }
}
