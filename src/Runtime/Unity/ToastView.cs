using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Lumera.DebuggerKit
{
    // 메뉴에서 실행한 치트의 결과를 화면 아래에 잠깐 보인다. 성공도 실패도 보인다.
    public sealed class ToastView : MonoBehaviour
    {
        private const float SHOWN_SECONDS = 2.5f;

        [SerializeField] private Image _background = null;
        [SerializeField] private TextMeshProUGUI _text = null;

        // 게임 시간이 멈춰도 사라지도록 unscaled 시간으로 잰다.
        private float _hideAt;

        public void Show(string message, bool bSuccess)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            _text.text = message;
            _text.color = bSuccess ? DebuggerKitTheme.SUCCESS : DebuggerKitTheme.ERROR;
            _hideAt = Time.unscaledTime + SHOWN_SECONDS;
            _background.gameObject.SetActive(true);
        }

        public void Hide()
        {
            _background.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_background.gameObject.activeSelf && Time.unscaledTime >= _hideAt)
            {
                Hide();
            }
        }
    }
}
