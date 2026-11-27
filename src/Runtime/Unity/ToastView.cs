using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Lumera.ZRay
{
    public sealed class ToastView : MonoBehaviour
    {
        private const float SHOWN_SECONDS = 2.5f;

        [SerializeField] private Image _background = null;
        [SerializeField] private TextMeshProUGUI _text = null;

        private float _hideAt;

        public void Show(string message, bool bSuccess)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            _text.text = message;
            _text.color = bSuccess ? ZRayTheme.SUCCESS : ZRayTheme.ERROR;
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
