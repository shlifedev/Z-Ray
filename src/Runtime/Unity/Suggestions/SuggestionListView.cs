using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Lumera.ZRay
{
    public sealed class SuggestionListView : MonoBehaviour
    {
        [SerializeField] private RectTransform _panel = null;
        [SerializeField] private ScrollRect _scroll = null;
        [SerializeField] private RectTransform _content = null;
        [SerializeField] private SuggestionRowView _rowPrefab = null;

        private readonly List<SuggestionRowView> _rows = new List<SuggestionRowView>();
        private Action<int> _clicked;
        private Func<SuggestionRowView, RectTransform, SuggestionRowView> _createRow;
        private int _count;
        private int _selectedIndex = -1;

        public bool IsVisible => _panel.gameObject.activeSelf;
        public int Count => IsVisible ? _count : 0;
        public RectTransform Panel => _panel;

        public void Initialize(Func<SuggestionRowView, RectTransform, SuggestionRowView> createRow, Action<int> clicked)
        {
            _createRow = createRow;
            _clicked = clicked;
            Hide();
        }

        public void Show(IReadOnlyList<Suggestion> suggestions, int selectedIndex)
        {
            _count = suggestions.Count;
            if (_count == 0)
            {
                Hide();
                return;
            }

            _panel.gameObject.SetActive(true);
            for (int index = 0; index < _count; index++)
            {
                if (index >= _rows.Count)
                {
                    SuggestionRowView row = _createRow(_rowPrefab, _content);
                    RectTransform rect = (RectTransform)row.transform;
                    rect.anchorMin = new Vector2(0.0f, 1.0f);
                    rect.anchorMax = new Vector2(1.0f, 1.0f);
                    rect.pivot = new Vector2(0.5f, 1.0f);
                    rect.sizeDelta = new Vector2(0.0f, ZRayTheme.ROW_HEIGHT);
                    rect.anchoredPosition = new Vector2(0.0f, -index * ZRayTheme.ROW_HEIGHT);
                    _rows.Add(row);
                }

                _rows[index].gameObject.SetActive(true);
                _rows[index].Set(index, suggestions[index], index == selectedIndex, _clicked);
            }

            for (int index = _count; index < _rows.Count; index++)
            {
                _rows[index].gameObject.SetActive(false);
            }

            int visibleRows = Mathf.Min(_count, ZRayTheme.MAX_VISIBLE_SUGGESTIONS);
            _content.sizeDelta = new Vector2(_content.sizeDelta.x, _count * ZRayTheme.ROW_HEIGHT);
            _panel.sizeDelta = new Vector2(_panel.sizeDelta.x, visibleRows * ZRayTheme.ROW_HEIGHT);
            _selectedIndex = selectedIndex;
            ScrollToSelected();
        }

        public void Select(int selectedIndex)
        {
            if (_selectedIndex >= 0 && _selectedIndex < _count)
            {
                _rows[_selectedIndex].SetSelected(false);
            }

            _selectedIndex = selectedIndex;
            if (_selectedIndex >= 0 && _selectedIndex < _count)
            {
                _rows[_selectedIndex].SetSelected(true);
            }

            ScrollToSelected();
        }

        public void Hide()
        {
            _count = 0;
            _selectedIndex = -1;
            _panel.gameObject.SetActive(false);
        }

        private void ScrollToSelected()
        {
            float viewHeight = _panel.sizeDelta.y;
            float top = _content.anchoredPosition.y;
            if (_selectedIndex < 0)
            {
                top = 0.0f;
            }
            else
            {
                float rowTop = _selectedIndex * ZRayTheme.ROW_HEIGHT;
                if (rowTop < top)
                {
                    top = rowTop;
                }
                else if (rowTop + ZRayTheme.ROW_HEIGHT > top + viewHeight)
                {
                    top = rowTop + ZRayTheme.ROW_HEIGHT - viewHeight;
                }
            }

            _scroll.StopMovement();
            _content.anchoredPosition = new Vector2(_content.anchoredPosition.x, top);
        }
    }
}
