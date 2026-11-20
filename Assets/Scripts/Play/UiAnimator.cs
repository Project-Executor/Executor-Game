using System;
using UnityEngine;
using UnityEngine.UI;

namespace Executor.Play
{
    /// <summary>
    /// UI 부품의 작은 움직임: 깜박이는 커서(1초 주기, 반은 꺼짐), 시계(분 단위). 붙인 오브젝트가 꺼져 있으면 멈춘다
    /// </summary>
    public class UiAnimator : MonoBehaviour
    {
        private Graphic _blink;
        private Text _clock;
        private string _shown;

        public static void Blink(Graphic graphic) => graphic.gameObject.AddComponent<UiAnimator>()._blink = graphic;

        public static void Clock(Text text) => text.gameObject.AddComponent<UiAnimator>()._clock = text;

        private void Update()
        {
            if (_blink != null) _blink.enabled = Time.unscaledTime % 1f < 0.5f;
            if (_clock != null)
            {
                string now = DateTime.Now.ToString("HH:mm");
                if (now != _shown) _clock.text = _shown = now;
            }
        }
    }
}
