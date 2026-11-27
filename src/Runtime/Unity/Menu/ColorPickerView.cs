using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Lumera.ZRay
{
    public sealed class ColorPickerView : MonoBehaviour
    {
        private const int AREA_TEXTURE_SIZE = 64;
        private const int BAR_TEXTURE_SIZE = 128;

        [SerializeField] private Button _blocker = null;
        [SerializeField] private RectTransform _panel = null;
        [SerializeField] private RawImage _area = null;
        [SerializeField] private DragPad _areaPad = null;
        [SerializeField] private RectTransform _areaCursor = null;
        [SerializeField] private RawImage _hueBar = null;
        [SerializeField] private DragPad _huePad = null;
        [SerializeField] private RectTransform _hueCursor = null;
        [SerializeField] private RawImage _alphaChecker = null;
        [SerializeField] private RawImage _alphaBar = null;
        [SerializeField] private DragPad _alphaPad = null;
        [SerializeField] private RectTransform _alphaCursor = null;
        [SerializeField] private RawImage _previewChecker = null;
        [SerializeField] private Image _previewBefore = null;
        [SerializeField] private Image _previewAfter = null;
        [SerializeField] private TextMeshProUGUI _hex = null;
        [SerializeField] private Button _close = null;

        private readonly Color32[] _areaPixels = new Color32[AREA_TEXTURE_SIZE * AREA_TEXTURE_SIZE];
        private Texture2D _areaTexture;
        private Texture2D _hueTexture;
        private Texture2D _alphaTexture;
        private Action<Color32> _changed;
        private Action _released;
        private float _hue;
        private float _saturation;
        private float _value;
        private float _alpha;
        private float _areaHue = -1.0f;

        public bool IsVisible => gameObject.activeSelf;
        public RectTransform Panel => _panel;

        public Color32 Current
        {
            get
            {
                Color color = Color.HSVToRGB(_hue, _saturation, _value);
                color.a = _alpha;
                return color;
            }
        }

        public void Initialize(ZRayCanvas canvas, Action<Color32> changed, Action released, Action closeRequested)
        {
            _changed = changed;
            _released = released;
            _areaTexture = CreateTexture("ZRayColorArea", AREA_TEXTURE_SIZE, AREA_TEXTURE_SIZE);
            _hueTexture = CreateTexture("ZRayColorHue", 1, BAR_TEXTURE_SIZE);
            _alphaTexture = CreateTexture("ZRayColorAlpha", 1, BAR_TEXTURE_SIZE);
            Color32[] hues = new Color32[BAR_TEXTURE_SIZE];
            Color32[] alphas = new Color32[BAR_TEXTURE_SIZE];
            for (int index = 0; index < BAR_TEXTURE_SIZE; index++)
            {
                float ratio = index / (BAR_TEXTURE_SIZE - 1.0f);
                hues[index] = Color.HSVToRGB(ratio, 1.0f, 1.0f);
                alphas[index] = new Color(1.0f, 1.0f, 1.0f, ratio);
            }

            _hueTexture.SetPixels32(hues);
            _hueTexture.Apply(false, true);
            _alphaTexture.SetPixels32(alphas);
            _alphaTexture.Apply(false, true);
            _area.texture = _areaTexture;
            _hueBar.texture = _hueTexture;
            _alphaBar.texture = _alphaTexture;
            SetChecker(canvas, _alphaChecker);
            SetChecker(canvas, _previewChecker);

            _areaPad.Moved += OnAreaMoved;
            _huePad.Moved += OnHueMoved;
            _alphaPad.Moved += OnAlphaMoved;
            _areaPad.Released += OnReleased;
            _huePad.Released += OnReleased;
            _alphaPad.Released += OnReleased;
            _blocker.onClick.AddListener(() => closeRequested());
            _close.onClick.AddListener(() => closeRequested());
            gameObject.SetActive(false);
        }

        public void Show(Color32 color)
        {
            SetColor(color);
            _previewBefore.color = color;
            gameObject.SetActive(true);
        }

        public void SetColor(Color32 color)
        {
            Color.RGBToHSV(color, out float hue, out float saturation, out float value);
            if (saturation > 0.0f && value > 0.0f)
            {
                _hue = hue;
            }

            if (value > 0.0f)
            {
                _saturation = saturation;
            }

            _value = value;
            _alpha = color.a / 255.0f;
            Refresh();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_areaPad != null)
            {
                _areaPad.Moved -= OnAreaMoved;
                _areaPad.Released -= OnReleased;
            }

            if (_huePad != null)
            {
                _huePad.Moved -= OnHueMoved;
                _huePad.Released -= OnReleased;
            }

            if (_alphaPad != null)
            {
                _alphaPad.Moved -= OnAlphaMoved;
                _alphaPad.Released -= OnReleased;
            }

            Destroy(_areaTexture);
            Destroy(_hueTexture);
            Destroy(_alphaTexture);
        }

        private void OnAreaMoved(Vector2 position)
        {
            _saturation = position.x;
            _value = position.y;
            Refresh();
            _changed(Current);
        }

        private void OnHueMoved(Vector2 position)
        {
            _hue = position.y;
            Refresh();
            _changed(Current);
        }

        private void OnAlphaMoved(Vector2 position)
        {
            _alpha = position.y;
            Refresh();
            _changed(Current);
        }

        private void OnReleased()
        {
            _released();
        }

        private void Refresh()
        {
            if (!Mathf.Approximately(_areaHue, _hue))
            {
                _areaHue = _hue;
                for (int y = 0; y < AREA_TEXTURE_SIZE; y++)
                {
                    float value = y / (AREA_TEXTURE_SIZE - 1.0f);
                    for (int x = 0; x < AREA_TEXTURE_SIZE; x++)
                    {
                        _areaPixels[(y * AREA_TEXTURE_SIZE) + x] = Color.HSVToRGB(_hue, x / (AREA_TEXTURE_SIZE - 1.0f), value);
                    }
                }

                _areaTexture.SetPixels32(_areaPixels);
                _areaTexture.Apply(false);
            }

            Color32 current = Current;
            Color opaque = current;
            opaque.a = 1.0f;
            Rect area = _area.rectTransform.rect;
            _areaCursor.anchoredPosition = new Vector2(Mathf.Round(_saturation * area.width), Mathf.Round(_value * area.height));
            _hueCursor.anchoredPosition = new Vector2(0.0f, Mathf.Round(_hue * _hueBar.rectTransform.rect.height));
            _alphaCursor.anchoredPosition = new Vector2(0.0f, Mathf.Round(_alpha * _alphaBar.rectTransform.rect.height));
            _alphaBar.color = opaque;
            _previewAfter.color = current;
            _hex.text = ColorValueType.FormatColor(current);
        }

        private static Texture2D CreateTexture(string name, int width, int height)
        {
            return new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        private static void SetChecker(ZRayCanvas canvas, RawImage image)
        {
            image.texture = canvas.CheckerTexture;
            image.uvRect = canvas.GetCheckerUV(image.rectTransform.rect.size);
        }
    }
}
