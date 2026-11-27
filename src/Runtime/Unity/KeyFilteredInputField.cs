using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Lumera.ZRay
{
    public sealed class KeyFilteredInputField : TMP_InputField
    {
        private readonly Event _event = new Event();

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

        public void SetTextAndCaret(string value, int caret)
        {
            SetTextWithoutNotify(value);
            caretPosition = Mathf.Clamp(caret, 0, value.Length);
            ForceLabelUpdate();
        }
    }
}
