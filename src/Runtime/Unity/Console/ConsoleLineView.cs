using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Lumera.ZRay
{
    // 콘솔 기록의 한 줄. 실행한 명령을 보여 주는 줄은 누르면 그 명령을 입력 줄로 다시 가져온다.
    public sealed class ConsoleLineView : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Image _background = null;
        [SerializeField] private TextMeshProUGUI _text = null;

        private Action<string> _recallOrNull;
        private string _recall = "";

        public void Set(TranscriptLine line, Action<string> recall)
        {
            _text.text = line.Text;
            _text.color = ZRayTheme.GetLineColor(line.Kind);
            bool bRecallable = line.Recall.Length > 0;
            if (_background != null)
            {
                _background.color = line.Kind == EConsoleLineKind.Echo ? ZRayTheme.ECHO_BG : ZRayTheme.TRANSPARENT;
                _background.raycastTarget = bRecallable;
                _text.raycastTarget = false;
            }
            else
            {
                _text.raycastTarget = bRecallable;
            }

            _recall = line.Recall;
            _recallOrNull = recall;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_recall.Length > 0)
            {
                _recallOrNull?.Invoke(_recall);
            }
        }
    }
}
