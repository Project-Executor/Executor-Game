using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Executor.Play
{
    /// <summary>막대(볼륨 등)를 누르거나 끌면 그 위치 값(0~1)을 알린다</summary>
    public class SliderDrag : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        public Action<float> OnValue;

        public void OnPointerDown(PointerEventData e) => Report(e);
        public void OnDrag(PointerEventData e) => Report(e);

        private void Report(PointerEventData e)
        {
            var rt = (RectTransform)transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, e.position, e.pressEventCamera, out Vector2 local)) return;
            float x = local.x - rt.rect.xMin;
            OnValue?.Invoke(rt.rect.width <= 0 ? 0 : x / rt.rect.width);
        }
    }
}
