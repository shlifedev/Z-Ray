using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Lumera.ZRay
{
    public sealed class DragPad : MonoBehaviour, IPointerDownHandler, IInitializePotentialDragHandler, IDragHandler, IPointerUpHandler
    {
        public event Action<Vector2> Moved;
        public event Action Released;

        public void OnPointerDown(PointerEventData eventData)
        {
            Report(eventData);
        }

        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            eventData.useDragThreshold = false;
        }

        public void OnDrag(PointerEventData eventData)
        {
            Report(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                Released?.Invoke();
            }
        }

        private void Report(PointerEventData eventData)
        {
            RectTransform rect = (RectTransform)transform;
            if (eventData.button != PointerEventData.InputButton.Left
                || !RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position, eventData.pressEventCamera, out Vector2 local))
            {
                return;
            }

            Rect area = rect.rect;
            Moved?.Invoke(new Vector2(Mathf.Clamp01((local.x - area.xMin) / area.width), Mathf.Clamp01((local.y - area.yMin) / area.height)));
        }
    }
}
