using UnityEngine;
using UnityEngine.UI;

namespace Lumera.ZRay.Samples.OrbitSalvage
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(OrbitSalvageGame))]
    public sealed class OrbitSalvageView : MonoBehaviour
    {
        private static readonly Color32 INK = new Color32(10, 17, 28, 255);
        private static readonly Color32 PAPER = new Color32(228, 243, 239, 255);
        private static readonly Color32 MUTED = new Color32(144, 169, 183, 255);
        private static readonly Color32 MINT = new Color32(118, 247, 205, 255);
        private static readonly Color32 AMBER = new Color32(255, 186, 88, 255);

        private OrbitSalvageGame _game;
        private Font _font;
        private RectTransform _canvas;
        private RectTransform _field;
        private RectTransform _ship;
        private RectTransform _core;
        private RectTransform _result;
        private Text _title;
        private Text _objective;
        private Text _cargo;
        private Text _timer;
        private Text _controls;
        private Text _tools;
        private Text _resultTitle;
        private Text _resultDetail;
        private Vector2 _lastSize;
        private int _lastCount = -1;
        private int _lastSecond = -1;

        private void Awake()
        {
            _game = GetComponent<OrbitSalvageGame>();
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _canvas = CreateRect("OrbitSalvageHUD", transform);
            Canvas canvas = _canvas.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -100;
            CanvasScaler scaler = _canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1000.0f, 640.0f);
            scaler.matchWidthOrHeight = 0.5f;
            AddBackground(_canvas, INK);

            _field = CreateFigure("Playfield", _canvas, ESalvageFigure.Field);
            _ship = CreateFigure("Ship", _field, ESalvageFigure.Ship);
            _core = CreateFigure("Core", _field, ESalvageFigure.Core);
            _title = CreateLabel("Title", _canvas, "ORBIT SALVAGE", 34, PAPER, TextAnchor.MiddleLeft, FontStyle.Bold);
            _objective = CreateLabel("Objective", _canvas, "Fly into the amber cores. Collect five in 45 seconds.", 15, MUTED, TextAnchor.MiddleLeft);
            _cargo = CreateLabel("Cargo", _canvas, "", 20, AMBER, TextAnchor.MiddleRight, FontStyle.Bold);
            _timer = CreateLabel("Timer", _canvas, "", 24, MINT, TextAnchor.MiddleRight, FontStyle.Bold);
            _controls = CreateLabel("FlightControls", _canvas, "WASD / Arrows   Move      R   Restart", 15, PAPER, TextAnchor.MiddleLeft);
#if UNITY_EDITOR || LUMERA_ZRAY
            _tools = CreateLabel("ZRayControls", _canvas, "F1   Cheat menu      `   Console", 15, MUTED, TextAnchor.MiddleRight);
#endif
            _result = CreateRect("RoundResult", _field);
            AddBackground(_result, new Color32(9, 20, 34, 248));
            _resultTitle = CreateLabel("Outcome", _result, "", 26, PAPER, TextAnchor.MiddleCenter, FontStyle.Bold);
            _resultDetail = CreateLabel("Restart", _result, "Press R to fly again", 16, MUTED, TextAnchor.MiddleCenter);
            _result.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            _canvas.gameObject.SetActive(true);
        }

        private void OnDisable()
        {
            if (_canvas != null)
            {
                _canvas.gameObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (_canvas != null)
            {
                Destroy(_canvas.gameObject);
            }
        }

        private void LateUpdate()
        {
            Vector2 size = _canvas.rect.size;
            if (size != _lastSize)
            {
                _lastSize = size;
                Layout(size);
            }

            PlaceInField(_ship, _game.ShipPosition);
            PlaceInField(_core, _game.CorePosition);
            _ship.localRotation = Quaternion.Euler(0.0f, 0.0f, Vector2.SignedAngle(Vector2.up, _game.Heading));
            bool bFinished = _game.IsWon || _game.IsLost;
            if (_game.Collected != _lastCount)
            {
                _lastCount = _game.Collected;
                _cargo.text = $"CORES   {_lastCount} / {OrbitSalvageGame.TARGET_COUNT}";
            }

            int seconds = Mathf.CeilToInt(_game.RemainingSeconds);
            if (seconds != _lastSecond)
            {
                _lastSecond = seconds;
                _timer.text = $"{seconds / 60:00}:{seconds % 60:00}";
                _timer.color = seconds <= 10 ? AMBER : MINT;
            }

            if (bFinished != _result.gameObject.activeSelf)
            {
                _result.gameObject.SetActive(bFinished);
                _core.gameObject.SetActive(!bFinished);
            }

            if (bFinished)
            {
                _resultTitle.text = _game.IsWon ? "CARGO SECURED" : "TIME EXPIRED";
                _resultTitle.color = _game.IsWon ? MINT : AMBER;
            }
        }

        private void Layout(Vector2 size)
        {
            bool bNarrow = size.x < 650.0f;
            float margin = Mathf.Clamp(size.x * 0.04f, 12.0f, 40.0f);
            float header = bNarrow ? 108.0f : 82.0f;
            float footer = bNarrow ? 64.0f : 44.0f;
            float width = Mathf.Min(size.x - margin * 2.0f, Mathf.Max(1.0f, size.y - header - footer) * 16.0f / 9.0f);
            float height = width * 9.0f / 16.0f;
            float left = (size.x - width) * 0.5f;
            float top = (size.y - height + header - footer) * 0.5f;
            SetBox(_field, left, top, width, height);

            float titleY = top - header + 6.0f;
            _title.fontSize = bNarrow ? 27 : 34;
            SetBox(_title.rectTransform, left, titleY, width, 40.0f);
            _objective.fontSize = bNarrow ? 13 : 15;
            SetBox(_objective.rectTransform, left, titleY + 37.0f, width, 23.0f);
            float statsY = bNarrow ? top - 34.0f : titleY + 4.0f;
            _cargo.alignment = bNarrow ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;
            SetBox(_cargo.rectTransform, left, statsY, width - 100.0f, 30.0f);
            SetBox(_timer.rectTransform, left + width - 96.0f, statsY, 96.0f, 30.0f);

            SetBox(_controls.rectTransform, left, top + height + 4.0f, width, 28.0f);
            if (_tools != null)
            {
                _tools.alignment = bNarrow ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;
                SetBox(_tools.rectTransform, left, top + height + (bNarrow ? 28.0f : 4.0f), width, 28.0f);
            }

            float shipSize = Mathf.Clamp(width * 0.049f, 21.0f, 52.0f);
            _ship.sizeDelta = new Vector2(shipSize, shipSize);
            _core.sizeDelta = _ship.sizeDelta;
            float card = Mathf.Min(width * 0.85f, 360.0f);
            SetBox(_result, (width - card) * 0.5f, (height - 92.0f) * 0.5f, card, 92.0f);
            SetBox(_resultTitle.rectTransform, 0.0f, 10.0f, card, 43.0f);
            SetBox(_resultDetail.rectTransform, 0.0f, 51.0f, card, 28.0f);
        }

        private static void PlaceInField(RectTransform marker, Vector2 position)
        {
            Vector2 anchor = new Vector2(position.x / 16.0f + 0.5f, position.y / 9.0f + 0.5f);
            marker.anchorMin = anchor;
            marker.anchorMax = anchor;
            marker.anchoredPosition = Vector2.zero;
        }

        private static void SetBox(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = Vector2.up;
            rect.anchorMax = Vector2.up;
            rect.pivot = Vector2.up;
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static RectTransform CreateRect(string label, Transform parent)
        {
            GameObject item = new GameObject(label, typeof(RectTransform));
            RectTransform rect = (RectTransform)item.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static RectTransform CreateFigure(string label, Transform parent, ESalvageFigure figure)
        {
            RectTransform rect = CreateRect(label, parent);
            OrbitSalvageGraphic graphic = rect.gameObject.AddComponent<OrbitSalvageGraphic>();
            graphic.Figure = figure;
            graphic.raycastTarget = false;
            return rect;
        }

        private static void AddBackground(RectTransform rect, Color32 color)
        {
            UnityEngine.UI.Image image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        private Text CreateLabel(string label, Transform parent, string content, int fontSize, Color32 color, TextAnchor anchor, FontStyle style = FontStyle.Normal)
        {
            Text text = CreateRect(label, parent).gameObject.AddComponent<Text>();
            text.font = _font;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = anchor;
            text.raycastTarget = false;
            text.supportRichText = false;
            text.text = content;
            return text;
        }
    }
}
