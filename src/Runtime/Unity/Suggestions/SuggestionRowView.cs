using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Lumera.ZRay
{
    public sealed class SuggestionRowView : MonoBehaviour, IPointerDownHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image _background = null;
        [SerializeField] private Image _marker = null;
        [SerializeField] private TextMeshProUGUI _label = null;
        [SerializeField] private TextMeshProUGUI _note = null;

        private readonly StringBuilder _builder = new StringBuilder();
        private Action<int> _clickedOrNull;
        private int _index;
        private bool _bSelected;
        private bool _bHovered;

        public void Set(int index, Suggestion suggestion, bool bSelected, Action<int> clicked)
        {
            _index = index;
            _clickedOrNull = clicked;
            _builder.Clear();
            RichText.AppendHighlighted(_builder, suggestion.Insert, suggestion.InsertSpans, ZRayTheme.HEX_MATCH);
            if (suggestion.Detail.Length > 0)
            {
                _builder.Append("  <color=").Append(ZRayTheme.HEX_MUTED).Append('>');
                RichText.AppendHighlighted(_builder, suggestion.Detail, suggestion.DetailSpans, ZRayTheme.HEX_MATCH);
                _builder.Append("</color>");
            }

            _label.SetText(_builder);
            _note.text = suggestion.Note;
            SetSelected(bSelected);
        }

        public void SetSelected(bool bSelected)
        {
            _bSelected = bSelected;
            RefreshBackground();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                _clickedOrNull?.Invoke(_index);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _bHovered = true;
            RefreshBackground();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _bHovered = false;
            RefreshBackground();
        }

        private void OnDisable()
        {
            _bHovered = false;
        }

        private void RefreshBackground()
        {
            _background.color = _bSelected ? ZRayTheme.ROW_SELECTED : (_bHovered ? ZRayTheme.ROW_HOVER : ZRayTheme.TRANSPARENT);
            if (_marker != null)
            {
                _marker.enabled = _bSelected;
            }
        }
    }
}
