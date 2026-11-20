using System;
using UnityEngine;
using UnityEngine.UI;
using static Executor.Play.UiKit;

namespace Executor.Play
{
    /// <summary>
    /// 디자인 시안(Executor_Export/02_ui_screens)의 색과 부품. 시안은 1280x720이고 화면은 1920x1080이라
    /// 시안의 px 값에 <see cref="S"/>(1.5)를 곱해 쓴다 (예: 글자 12px -> 18).
    /// 모양: 둥근 모서리 없이 사각형, 테두리 + 오른쪽 아래로 밀린 그림자(번짐 없음)
    /// </summary>
    public static class Theme
    {
        public const float S = 1.5f;

        public static readonly Color Ink = Hex(0x0c0a18);
        public static readonly Color Night = Hex(0x15112e);
        public static readonly Color Window = Hex(0x0a0716, 0.94f);
        public static readonly Color WindowLine = Hex(0x4c4aa8);
        public static readonly Color TitleBar = Hex(0x36307a);
        public static readonly Color Track = Hex(0x1e1942);
        public static readonly Color TrackLine = Hex(0x2a2358);
        public static readonly Color Cyan = Hex(0x22d3ee);
        public static readonly Color CyanLight = Hex(0xb6f4ff);
        /// <summary>본문 글자 (거의 흰색)</summary>
        public static readonly Color Fg = Hex(0xeaf2ff);
        public static readonly Color Muted = Hex(0x8b98b8);
        public static readonly Color Dim = Hex(0x4b5675);
        public static readonly Color Green = Hex(0x5cf08a);
        public static readonly Color Red = Hex(0xff4d5e);
        public static readonly Color Yellow = Hex(0xffd84a);
        public static readonly Color YellowDark = Hex(0xc08a14);
        public static readonly Color Orange = Hex(0xff9a3c);
        public static readonly Color Magenta = Hex(0xd61ad0);

        public static Color Hex(int rgb, float alpha = 1f) =>
            new Color(((rgb >> 16) & 0xff) / 255f, ((rgb >> 8) & 0xff) / 255f, (rgb & 0xff) / 255f, alpha);

        /// <summary>그림 없는 빈 RectTransform</summary>
        public static RectTransform Node(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>단색 사각형</summary>
        public static Image Fill(Transform parent, string name, Color color, bool raycast = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        /// <summary>왼쪽 위 기준 배치 (시안처럼 left, top으로). 값은 이미 1.5배한 화면 px</summary>
        public static void At(Component c, float left, float top, float width, float height)
        {
            var rt = (RectTransform)c.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(left, -top);
            rt.sizeDelta = new Vector2(width, height);
        }

        /// <summary>오른쪽 위 기준 배치 (right, top)</summary>
        public static void AtRight(Component c, float right, float top, float width, float height)
        {
            var rt = (RectTransform)c.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(1, 1);
            rt.anchoredPosition = new Vector2(-right, -top);
            rt.sizeDelta = new Vector2(width, height);
        }

        /// <summary>부모를 꽉 채우되 안쪽으로 left, top, right, bottom만큼 들인다</summary>
        public static void Inset(Component c, float left, float top, float right, float bottom)
        {
            var rt = (RectTransform)c.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        /// <summary>안쪽 테두리 (사방 width 두께). 바탕이 반투명이어도 겹치지 않게 네 변으로 그린다</summary>
        public static void Border(Transform parent, Color color, float width)
        {
            Image top = Fill(parent, "BorderTop", color);
            Edge(top, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -width), Vector2.zero);
            Image bottom = Fill(parent, "BorderBottom", color);
            Edge(bottom, new Vector2(0, 0), new Vector2(1, 0), Vector2.zero, new Vector2(0, width));
            Image left = Fill(parent, "BorderLeft", color);
            Edge(left, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, width), new Vector2(width, -width));
            Image right = Fill(parent, "BorderRight", color);
            Edge(right, new Vector2(1, 0), new Vector2(1, 1), new Vector2(-width, width), new Vector2(0, -width));
        }

        private static void Edge(Image image, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var rt = image.rectTransform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        /// <summary>
        /// 시안의 창·상자: 바탕 + 테두리 + 오른쪽 아래 그림자(box-shadow: Npx Npx 0 #0c0a18).
        /// 돌려주는 것은 바깥 상자(배치용). 내용은 그 안에 넣는다 (테두리는 맨 위에 그려지도록 마지막에 붙인다)
        /// </summary>
        public static RectTransform Frame(Transform parent, string name, Color fill, Color border, float borderWidth, float shadow)
        {
            RectTransform root = Node(parent, name);
            if (shadow > 0)
            {
                // 그림자는 상자 밖 오른쪽·아래 띠 두 개 (반투명 바탕 밑으로 비치지 않게)
                Image right = Fill(root, "ShadowRight", Ink);
                Edge(right, new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, -shadow), new Vector2(shadow, -shadow));
                Image bottom = Fill(root, "ShadowBottom", Ink);
                Edge(bottom, new Vector2(0, 0), new Vector2(1, 0), new Vector2(shadow, -shadow), new Vector2(0, 0));
            }
            Image bg = Fill(root, "Fill", fill);
            Inset(bg, 0, 0, 0, 0);
            if (borderWidth > 0)
            {
                RectTransform b = Node(root, "Border");
                Inset(b, 0, 0, 0, 0);
                Border(b, border, borderWidth);
            }
            return root;
        }

        /// <summary>테두리를 맨 위로 (Frame에 내용을 다 넣은 뒤 부른다)</summary>
        public static void BorderOnTop(RectTransform frame)
        {
            Transform border = frame.Find("Border");
            if (border != null) border.SetAsLastSibling();
        }

        public static Text Label(Transform parent, string text, float size, Color color, TextAnchor align = TextAnchor.MiddleLeft,
            Font font = null, bool bold = false)
        {
            Text t = UiKit.Label(parent, text, Mathf.RoundToInt(size), align, color, bold);
            if (font != null) t.font = font;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.supportRichText = true;
            return t;
        }

        /// <summary>글자 테두리 (text-shadow 1px 사방 #0c0a18 흉내)</summary>
        public static void Outlined(Text text, float width = 1.5f)
        {
            var o = text.gameObject.AddComponent<Outline>();
            o.effectColor = Ink;
            o.effectDistance = new Vector2(width, -width);
        }

        /// <summary>
        /// 버튼 동작을 붙인다. target의 색(흰색 권장)에 normal / hover 색을 곱해 보인다.
        /// 평소에 안 보이는 hover 배경이면 normal을 투명으로
        /// </summary>
        public static Button Clickable(Graphic target, Action onClick, Color normal, Color hover, Color? pressed = null)
        {
            target.raycastTarget = true;
            var button = target.gameObject.AddComponent<Button>();
            button.targetGraphic = target;
            var colors = button.colors;
            colors.normalColor = normal;
            colors.highlightedColor = hover;
            colors.selectedColor = normal;
            colors.pressedColor = pressed ?? hover * 0.85f;
            colors.disabledColor = new Color(normal.r * 0.5f, normal.g * 0.5f, normal.b * 0.5f, normal.a);
            colors.fadeDuration = 0.05f;
            button.colors = colors;
            if (onClick != null) button.onClick.AddListener(() => onClick());
            return button;
        }

        /// <summary>도트 아이콘 (Resources/UI/{path}). 그림이 없으면 null</summary>
        public static Image Icon(Transform parent, string path, float size)
        {
            Sprite sprite = ArtCatalog.Ui(path);
            if (sprite == null) return null;
            Image image = Fill(parent, "Icon", Color.white);
            image.sprite = sprite;
            image.preserveAspect = true;
            image.rectTransform.sizeDelta = new Vector2(size, size);
            return image;
        }

        /// <summary>채운 버튼 (주 행동, 하늘색): 그림자 + 글자. 마우스를 올리면 밝은 하늘색</summary>
        public static Button PrimaryButton(Transform parent, string text, float fontSize, Action onClick, out RectTransform root)
        {
            root = Node(parent, "Primary:" + text);
            Image shadow = Fill(root, "Shadow", Ink);
            Inset(shadow, 4 * S, 4 * S, -4 * S, -4 * S);
            Image face = Fill(root, "Face", Color.white);
            Inset(face, 0, 0, 0, 0);
            Button button = Clickable(face, onClick, Cyan, CyanLight);
            Text label = Label(face.transform, text, fontSize, Ink, TextAnchor.MiddleCenter, bold: true);
            Inset(label, 0, 0, 0, 0);
            return button;
        }

        /// <summary>테두리 버튼 (보조 행동): 투명 바탕 + 하늘색 테두리·글자. 마우스를 올리면 바탕이 옅은 하늘색</summary>
        public static Button GhostButton(Transform parent, string text, float fontSize, Action onClick, out RectTransform root)
        {
            root = Node(parent, "Ghost:" + text);
            Image face = Fill(root, "Face", new Color(Cyan.r, Cyan.g, Cyan.b, 0.15f));
            Inset(face, 0, 0, 0, 0);
            Button button = Clickable(face, onClick, new Color(1, 1, 1, 0), Color.white, new Color(1, 1, 1, 0.7f));
            RectTransform b = Node(root, "Border");
            Inset(b, 0, 0, 0, 0);
            Border(b, Cyan, 2 * S);
            Text label = Label(root, text, fontSize, Cyan, TextAnchor.MiddleCenter, bold: true);
            Inset(label, 0, 0, 0, 0);
            return button;
        }

        /// <summary>마우스를 올리면 옅게 칠해지는 투명 버튼 바탕</summary>
        public static Button HoverArea(Transform parent, string name, Color hover, Action onClick, out Image area)
        {
            area = Fill(parent, name, hover);
            return Clickable(area, onClick, new Color(1, 1, 1, 0), Color.white, new Color(1, 1, 1, 0.7f));
        }
    }
}
