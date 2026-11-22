using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Lumera.DebuggerKit
{
    // 키 입력을 TMP보다 먼저 볼 수 있는 입력칸. 콘솔과 메뉴가 Tab, ↑/↓, Enter를 자기 뜻으로 쓰려고 만든다.
    // TMP_InputField는 Enter와 Esc에서 스스로 포커스를 놓고, 한 줄 입력에서 ↑/↓로 커서를 줄 끝으로 옮긴다.
    // 그 처리를 하는 KeyPressed는 재정의할 수 없어서, 이벤트를 꺼내는 OnUpdateSelected를 재정의해 먼저 거른다.
    public sealed class KeyFilteredInputField : TMP_InputField
    {
        private readonly Event _event = new Event();

        // true를 돌려주면 그 키는 TMP에 넘기지 않는다. null이면 모두 TMP가 처리한다.
        public Func<Event, bool> KeyFilterOrNull { get; set; }

        public override void OnUpdateSelected(BaseEventData eventData)
        {
            if (!isFocused)
            {
                return;
            }

            bool bConsumed = false;
            while (Event.PopEvent(_event))
            {
                if (_event.rawType == EventType.KeyDown)
                {
                    bConsumed = true;
                    if (KeyFilterOrNull == null || !KeyFilterOrNull(_event))
                    {
                        ProcessEvent(_event);
                    }

                    continue;
                }

                if ((_event.rawType == EventType.ValidateCommand || _event.rawType == EventType.ExecuteCommand) && _event.commandName == "SelectAll")
                {
                    SelectAll();
                    bConsumed = true;
                }
            }

            if (bConsumed)
            {
                ForceLabelUpdate();
                eventData.Use();
            }
        }

        // 글자와 커서를 한 번에 바꾼다. onValueChanged는 부르지 않는다(부른 쪽이 화면을 직접 갱신한다).
        public void SetTextAndCaret(string value, int caret)
        {
            SetTextWithoutNotify(value);
            caretPosition = Mathf.Clamp(caret, 0, value.Length);
            ForceLabelUpdate();
        }
    }
}
