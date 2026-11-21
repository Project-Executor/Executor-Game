using UnityEngine;
using UnityEngine.UI;

namespace Executor.Play
{
    /// <summary>
    /// 시안(Login/Signup)의 입력칸 모양: 짙은 바탕 + 테두리. 테두리 색이 상태를 보여 준다 —
    /// 틀림 빨강 > 맞음 초록 > 입력 중(포커스) 하늘색 + 바깥 옅은 빛 > 평소 짙은 남색. 글자는 고정폭(GalmuriMono)
    /// </summary>
    public class FieldFrame : MonoBehaviour
    {
        public enum Mark { None, Good, Bad }

        public Mark State = Mark.None;

        private InputField _field;
        private Image[] _edges;
        private GameObject _glow;

        /// <summary>UiKit.Input으로 만든 칸에 시안 모양을 입힌다</summary>
        public static FieldFrame Apply(InputField field)
        {
            var image = field.GetComponent<Image>();
            if (image != null) image.color = Theme.Night;
            var outline = field.GetComponent<Outline>();
            if (outline != null) Destroy(outline);
            if (field.textComponent != null)
            {
                field.textComponent.font = UiKit.Mono;
                field.textComponent.color = Theme.Fg;
            }
            if (field.placeholder is Text ph)
            {
                ph.font = UiKit.Mono;
                ph.color = Theme.Dim;
            }
            field.caretColor = Theme.Cyan;
            field.customCaretColor = true;
            field.selectionColor = Theme.Hex(0x22d3ee, 0.35f);

            var frame = field.gameObject.AddComponent<FieldFrame>();
            frame._field = field;
            RectTransform glow = Theme.Node(field.transform, "Glow");
            Theme.Inset(glow, -2 * Theme.S, -2 * Theme.S, -2 * Theme.S, -2 * Theme.S);
            Theme.Border(glow, Theme.Hex(0x22d3ee, 0.35f), 2 * Theme.S);
            frame._glow = glow.gameObject;
            RectTransform edges = Theme.Node(field.transform, "Frame");
            Theme.Inset(edges, 0, 0, 0, 0);
            Theme.Border(edges, Theme.TrackLine, 2 * Theme.S);
            frame._edges = edges.GetComponentsInChildren<Image>();
            frame.Refresh();
            return frame;
        }

        private void Update() => Refresh();

        private void Refresh()
        {
            if (_field == null || _edges == null) return;
            bool focused = _field.isFocused;
            Color c = State == Mark.Bad ? Theme.Red : State == Mark.Good ? Theme.Green : focused ? Theme.Cyan : Theme.TrackLine;
            foreach (Image e in _edges)
            {
                if (e != null && e.color != c) e.color = c;
            }
            bool glow = focused && State == Mark.None;
            if (_glow != null && _glow.activeSelf != glow) _glow.SetActive(glow);
        }
    }
}
