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
        private float _period = 1f;
        private Text _clock;
        private string _shown;

        /// <summary>깜박임 (period초마다 반은 켜지고 반은 꺼짐). 이미 붙어 있으면 주기만 바꾼다</summary>
        public static void Blink(Graphic graphic, float period = 1f)
        {
            if (!graphic.TryGetComponent(out UiAnimator a)) a = graphic.gameObject.AddComponent<UiAnimator>();
            a._blink = graphic;
            a._period = Mathf.Max(0.1f, period);
        }

        public static void Clock(Text text) => text.gameObject.AddComponent<UiAnimator>()._clock = text;

        private void Update()
        {
            if (_blink != null) _blink.enabled = Time.unscaledTime % _period < _period / 2f;
            if (_clock != null)
            {
                string now = DateTime.Now.ToString("HH:mm");
                if (now != _shown) _clock.text = _shown = now;
            }
        }
    }
}
