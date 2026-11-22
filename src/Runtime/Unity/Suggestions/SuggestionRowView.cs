using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Lumera.DebuggerKit
{
    // 자동완성 후보 한 줄. 왼쪽에 값과 설명, 오른쪽에 분류나 "기본값" 같은 짧은 표시를 보인다.
    // 클릭(뗄 때)이 아니라 누르는 순간에 고른다. 누르면 입력칸이 포커스를 잃고, 포커스를 잃은 입력칸의 후보 목록은
    // 다음 프레임에 닫힌다. 뗄 때를 기다리면 그 사이 목록이 사라져 클릭이 들어오지 않는다.
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
            RichText.AppendHighlighted(_builder, suggestion.Insert, suggestion.InsertSpans, DebuggerKitTheme.HEX_MATCH);
            if (suggestion.Detail.Length > 0)
            {
                _builder.Append("  <color=").Append(DebuggerKitTheme.HEX_MUTED).Append('>');
                RichText.AppendHighlighted(_builder, suggestion.Detail, suggestion.DetailSpans, DebuggerKitTheme.HEX_MATCH);
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
            _background.color = _bSelected ? DebuggerKitTheme.ROW_SELECTED : (_bHovered ? DebuggerKitTheme.ROW_HOVER : DebuggerKitTheme.TRANSPARENT);
            if (_marker != null)
            {
                _marker.enabled = _bSelected;
            }
        }
    }
}
