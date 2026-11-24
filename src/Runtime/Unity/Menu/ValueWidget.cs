using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Lumera.ZRay
{
    // 위젯에서 일어난 일을 받는 쪽(치트 행).
    public interface IValueWidgetOwner
    {
        // 값이 바뀌었다(글자 입력 포함).
        void OnWidgetChanged(ValueWidget widget);
        // 값을 확정했다(스위치·선택지·± 누름).
        void OnWidgetCommitted(ValueWidget widget);
        // 입력칸의 키. true를 돌려주면 입력칸이 처리하지 않는다.
        bool OnWidgetKey(ValueWidget widget, Event keyEvent);
    }

    // 치트 인자 하나를 입력하는 위젯. 타입에 따라 스위치(bool), 선택지 버튼(열거형), 숫자 입력(±), 글자 입력 중 하나를 보인다.
    // 값은 언제나 글자(Text)로 주고받는다. 글자를 값으로 바꾸고 검증하는 일은 실행기가 콘솔과 같은 방법으로 한다.
    public sealed class ValueWidget : MonoBehaviour
    {
        // 선택지가 이 수를 넘으면 버튼 대신 글자 입력과 후보 목록을 쓴다.
        public const int MAX_SEGMENTS = 6;

        private const float SEGMENT_PADDING = 12.0f;
        private const float SEGMENT_GAP = 2.0f;
        private const float KNOB_INSET = 2.0f;

        [SerializeField] private Button _switch = null;
        [SerializeField] private RectTransform _knob = null;
        [SerializeField] private RectTransform _segments = null;
        [SerializeField] private Button _segmentPrefab = null;
        [SerializeField] private RectTransform _fieldGroup = null;
        [SerializeField] private KeyFilteredInputField _field = null;
        [SerializeField] private Button _minus = null;
        [SerializeField] private Button _plus = null;
        [SerializeField] private GameObject _readOnlyBadge = null;
        [SerializeField] private TextMeshProUGUI _readOnly = null;

        private readonly List<Button> _segmentButtons = new List<Button>();
        private readonly List<string> _choices = new List<string>();
        private IValueWidgetOwner _owner;
        private CheatParameter _parameter;
        private string _text = "";
        private bool _bListening;

        public CheatParameter Parameter => _parameter;
        // 이 위젯이 몇 번째 인자인가(행이 정한다).
        public int ParameterIndex { get; private set; }
        public string Text => _text;
        public bool IsEditing => _fieldGroup.gameObject.activeSelf && _field.isFocused;
        public RectTransform FieldRect => (RectTransform)_field.transform;
        // 글자를 입력하는 칸이 보이는가(Tab으로 옮겨 갈 수 있는가)
        public bool HasField => _fieldGroup.gameObject.activeSelf;

        public void Bind(ZRayCanvas canvas, IValueWidgetOwner owner, CheatParameter parameter, int parameterIndex, bool bWritable, string text)
        {
            _owner = owner;
            _parameter = parameter;
            ParameterIndex = parameterIndex;
            if (!_bListening)
            {
                _bListening = true;
                _switch.onClick.AddListener(OnSwitchClicked);
                _minus.onClick.AddListener(() => Step(-1));
                _plus.onClick.AddListener(() => Step(1));
                _field.onValueChanged.AddListener(OnFieldChanged);
                _field.KeyFilterOrNull = OnFieldKey;
            }

            _choices.Clear();
            parameter.ValueType.AddCandidates(_choices);
            bool bBool = parameter.Type == typeof(bool);
            bool bSegments = !bBool && _choices.Count > 0 && _choices.Count <= MAX_SEGMENTS;
            if (_readOnlyBadge != null)
            {
                _readOnlyBadge.SetActive(!bWritable);
            }
            else
            {
                _readOnly.gameObject.SetActive(!bWritable);
            }
            _switch.gameObject.SetActive(bWritable && bBool);
            _segments.gameObject.SetActive(bWritable && bSegments);
            _fieldGroup.gameObject.SetActive(bWritable && !bBool && !bSegments);
            _minus.gameObject.SetActive(parameter.IsNumber);
            _plus.gameObject.SetActive(parameter.IsNumber);
            if (bWritable && bSegments)
            {
                BuildSegments(canvas);
            }

            SetText(text);
        }

        // 값을 바꾸되 주인에게 알리지 않는다(주기적으로 다시 읽은 값, 보관해 둔 초안).
        public void SetText(string text)
        {
            _text = text ?? "";
            _readOnly.text = _text;
            if (_switch.gameObject.activeSelf)
            {
                _text = _parameter.FormatChoice(_text);
                bool bOn = _text == "true";
                _switch.image.color = bOn ? ZRayTheme.SWITCH_ON : ZRayTheme.SWITCH_OFF;
                RectTransform track = (RectTransform)_switch.transform;
                float travel = track.sizeDelta.x - _knob.sizeDelta.x - (KNOB_INSET * 2.0f);
                _knob.anchoredPosition = new Vector2(KNOB_INSET + (bOn ? travel : 0.0f), 0.0f);
            }

            if (_segments.gameObject.activeSelf)
            {
                for (int index = 0; index < _segmentButtons.Count; index++)
                {
                    bool bChosen = index < _choices.Count && string.Equals(_choices[index], _text, StringComparison.OrdinalIgnoreCase);
                    _segmentButtons[index].image.color = bChosen ? ZRayTheme.EDIT : ZRayTheme.BUTTON;
                    _segmentButtons[index].GetComponentInChildren<TextMeshProUGUI>(true).color = bChosen ? ZRayTheme.EDIT_TEXT : ZRayTheme.MUTED;
                }
            }

            if (_fieldGroup.gameObject.activeSelf && _field.text != _text)
            {
                _field.SetTextWithoutNotify(_text);
            }
        }

        // 스위치(bool)면 뒤집고 true를 돌려준다. 행을 눌렀을 때 쓴다.
        public bool TryToggle()
        {
            if (!_switch.gameObject.activeSelf)
            {
                return false;
            }

            OnSwitchClicked();
            return true;
        }

        public void Focus()
        {
            if (_fieldGroup.gameObject.activeSelf)
            {
                _field.ActivateInputField();
                _field.caretPosition = _field.text.Length;
            }
        }

        private void BuildSegments(ZRayCanvas canvas)
        {
            float x = 0.0f;
            for (int index = 0; index < _choices.Count; index++)
            {
                if (index >= _segmentButtons.Count)
                {
                    Button created = canvas.Create(_segmentPrefab, _segments);
                    int captured = index;
                    created.onClick.AddListener(() => OnSegmentClicked(captured));
                    _segmentButtons.Add(created);
                }

                Button button = _segmentButtons[index];
                TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
                label.text = _choices[index];
                float width = Mathf.Ceil(label.GetPreferredValues(_choices[index]).x + SEGMENT_PADDING);
                RectTransform rect = (RectTransform)button.transform;
                rect.sizeDelta = new Vector2(width, rect.sizeDelta.y);
                rect.anchoredPosition = new Vector2(x, 0.0f);
                button.gameObject.SetActive(true);
                x += width + SEGMENT_GAP;
            }

            for (int index = _choices.Count; index < _segmentButtons.Count; index++)
            {
                _segmentButtons[index].gameObject.SetActive(false);
            }

            _segments.sizeDelta = new Vector2(x, _segments.sizeDelta.y);
        }

        private void OnSwitchClicked()
        {
            SetText(_text == "true" ? "false" : "true");
            _owner.OnWidgetChanged(this);
            _owner.OnWidgetCommitted(this);
        }

        private void OnSegmentClicked(int index)
        {
            if (index < _choices.Count)
            {
                SetText(_choices[index]);
                _owner.OnWidgetChanged(this);
                _owner.OnWidgetCommitted(this);
            }
        }

        private void OnFieldChanged(string text)
        {
            _text = text;
            _owner.OnWidgetChanged(this);
        }

        private bool OnFieldKey(Event keyEvent)
        {
            return _owner.OnWidgetKey(this, keyEvent);
        }

        private void Step(int direction)
        {
            SetText(_parameter.StepText(_text, direction));
            _owner.OnWidgetChanged(this);
            _owner.OnWidgetCommitted(this);
        }
    }
}
