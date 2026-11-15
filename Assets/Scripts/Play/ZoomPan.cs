using UnityEngine;
using UnityEngine.EventSystems;

namespace Executor.Play
{
    /// <summary>
    /// 화면 확대·이동. 이 컴포넌트를 붙인 RectTransform이 보이는 영역(뷰포트, RectMask2D)이고 Content를 키운다.
    /// <list type="bullet">
    ///   <item>마우스 휠: 커서 위치를 기준으로 확대·축소</item>
    ///   <item>드래그: 확대한 상태에서 이동 (드래그하면 버튼 클릭은 취소된다)</item>
    ///   <item>ZoomBy / ResetView: 버튼·키보드용</item>
    /// </list>
    /// Content는 뷰포트와 같은 크기로 가득 채우고 pivot은 가운데여야 한다.
    /// </summary>
    public class ZoomPan : MonoBehaviour, IScrollHandler, IBeginDragHandler, IDragHandler
    {
        public RectTransform Content;
        public float MinZoom = 1f;
        public float MaxZoom = 2.5f;
        public float Step = 1.15f;

        public float Zoom { get; private set; } = 1f;

        private RectTransform Viewport => (RectTransform)transform;

        public void OnScroll(PointerEventData e)
        {
            if (Mathf.Abs(e.scrollDelta.y) < 0.01f) return;
            ZoomAt(e.position, e.scrollDelta.y > 0 ? Step : 1f / Step);
        }

        public void OnBeginDrag(PointerEventData e) { }

        public void OnDrag(PointerEventData e)
        {
            if (Content == null || Zoom <= MinZoom + 0.001f) return;
            float scale = GetComponentInParent<Canvas>()?.scaleFactor ?? 1f;
            Content.anchoredPosition += e.delta / scale;
            Clamp();
        }

        /// <summary>뷰포트 가운데를 기준으로 확대(factor &gt; 1)·축소</summary>
        public void ZoomBy(float factor)
        {
            Vector3 center = Viewport.TransformPoint(Viewport.rect.center);
            ZoomAt(RectTransformUtility.WorldToScreenPoint(null, center), factor);
        }

        public void ResetView()
        {
            Zoom = 1f;
            if (Content == null) return;
            Content.localScale = Vector3.one;
            Content.anchoredPosition = Vector2.zero;
        }

        /// <summary>화면 좌표 point 아래의 내용이 제자리에 있도록 확대한다</summary>
        private void ZoomAt(Vector2 point, float factor)
        {
            if (Content == null) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(Content, point, null, out Vector2 before);
            Zoom = Mathf.Clamp(Zoom * factor, MinZoom, MaxZoom);
            Content.localScale = new Vector3(Zoom, Zoom, 1f);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(Content, point, null, out Vector2 after);
            Content.anchoredPosition += (after - before) * Zoom;
            Clamp();
        }

        /// <summary>내용이 뷰포트 밖으로 빠져나가지 않게 이동 범위를 제한한다</summary>
        private void Clamp()
        {
            Vector2 size = Viewport.rect.size;
            Vector2 limit = size * (Zoom - 1f) / 2f;
            Vector2 p = Content.anchoredPosition;
            Content.anchoredPosition = new Vector2(Mathf.Clamp(p.x, -limit.x, limit.x), Mathf.Clamp(p.y, -limit.y, limit.y));
        }
    }
}
