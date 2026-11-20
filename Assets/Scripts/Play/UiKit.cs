using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Executor.Play
{
    /// <summary>
    /// 와이어프레임 UI 부품. 기준 해상도 1920x1080. 좌표는 RectTransform anchoredPosition(px).
    /// 디자인이 나오면 같은 역할의 프리팹으로 바꾼다.
    /// </summary>
    public static class UiKit
    {
        public static readonly Color Background = new Color32(0x0b, 0x12, 0x1c, 0xff);
        public static readonly Color PanelColor = new Color32(0x10, 0x18, 0x24, 0xee);
        public static readonly Color ButtonColor = new Color32(0x1c, 0x2c, 0x40, 0xff);
        public static readonly Color Line = new Color32(0x4f, 0xd1, 0xff, 0xff);
        public static readonly Color TextColor = new Color32(0xe6, 0xee, 0xf6, 0xff);
        public static readonly Color Muted = new Color32(0x8c, 0x9b, 0xad, 0xff);
        public static readonly Color Good = new Color32(0x3c, 0xcf, 0x7a, 0xff);
        public static readonly Color Warn = new Color32(0xff, 0xb4, 0x3c, 0xff);
        public static readonly Color Bad = new Color32(0xff, 0x5c, 0x6c, 0xff);

        public static Font Regular { get; private set; }
        public static Font Bold { get; private set; }
        /// <summary>고정폭 (터미널 글자, 숫자)</summary>
        public static Font Mono { get; private set; }

        /// <summary>Galmuri 도트 글꼴 (Resources/Fonts, SIL OFL). 없으면 OS 한글 글꼴</summary>
        public static void LoadFonts()
        {
            if (Regular != null) return;
            Font os = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" }, 32);
            Regular = Resources.Load<Font>("Fonts/Galmuri11") ?? os;
            Bold = Resources.Load<Font>("Fonts/Galmuri11-Bold") ?? Regular;
            Mono = Resources.Load<Font>("Fonts/GalmuriMono11") ?? Regular;
        }

        /// <summary>화면 전체 캔버스. sortOrder가 클수록 위에 그려진다</summary>
        public static RectTransform Canvas(Transform parent, string name, int sortOrder)
        {
            LoadFonts();
            if (Object.FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                Object.DontDestroyOnLoad(es);
            }
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            return (RectTransform)go.transform;
        }

        public static RectTransform Panel(Transform parent, string name, Color color, Color? border = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            if (border != null)
            {
                var outline = go.AddComponent<Outline>();
                outline.effectColor = border.Value;
                outline.effectDistance = new Vector2(3, -3);
            }
            return (RectTransform)go.transform;
        }

        public static Text Label(Transform parent, string text, int size, TextAnchor align, Color? color = null, bool bold = false)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = bold ? Bold : Regular;
            t.fontSize = size;
            t.alignment = align;
            t.color = color ?? TextColor;
            t.text = text;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static Button Button(Transform parent, string label, Action onClick, int size = 28, Color? color = null)
        {
            var rt = Panel(parent, "Button:" + label, color ?? ButtonColor, Line);
            var button = rt.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(0.75f, 0.9f, 1f);
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.8f);
            button.colors = colors;
            if (onClick != null) button.onClick.AddListener(() => onClick());
            var t = Label(rt, label, size, TextAnchor.MiddleCenter, bold: true);
            Stretch(t.rectTransform, 8);
            return button;
        }

        public static void SetButtonText(Button button, string text) => button.GetComponentInChildren<Text>().text = text;

        /// <summary>
        /// 입력칸에서 Enter로 입력을 마쳤을 때 action (다른 곳을 눌러 포커스가 빠진 경우는 제외).
        /// InputField.onSubmit은 Enter로 끝냈을 때만 불린다. 예전에는 onEndEdit + "이번 프레임에 Enter를 눌렀나"로 판단했는데,
        /// 입력칸이 키를 처리하는 시점과 Input System의 프레임이 어긋나 Enter를 놓치는 일이 있었다
        /// </summary>
        public static void OnEnter(InputField field, Action action)
        {
            field.onSubmit.AddListener(_ => action());
        }

        /// <summary>
        /// 다른 입력칸으로 포커스를 옮긴다. Enter를 누르면 UI가 그 Enter를 "제출"로 한 번 더 보내는데,
        /// 그때 방금 떠난 입력칸이 아직 선택돼 있으면 그 칸이 포커스를 다시 가져간다.
        /// 그래서 선택을 바로 다음 칸으로 바꿔 두고(그 제출은 다음 칸이 받아 자기를 켠다), 켜기도 바로 요청한다
        /// </summary>
        public static void FocusField(InputField field)
        {
            if (field == null || !field.gameObject.activeInHierarchy) return;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(field.gameObject);
            field.ActivateInputField();
        }

        public static InputField Input(Transform parent, string placeholder, bool password = false, int limit = 0, int size = 30)
        {
            var bg = Panel(parent, "Input", new Color32(0x1c, 0x26, 0x34, 0xff), Muted);
            var text = Label(bg, "", size, TextAnchor.MiddleLeft);
            text.supportRichText = false;
            Stretch(text.rectTransform, 16);
            var ph = Label(bg, placeholder, size, TextAnchor.MiddleLeft, Muted);
            Stretch(ph.rectTransform, 16);
            var input = bg.gameObject.AddComponent<InputField>();
            input.textComponent = text;
            input.placeholder = ph;
            input.characterLimit = limit;
            if (password) input.contentType = InputField.ContentType.Password;
            return input;
        }

        /// <summary>
        /// 가운데 팝업 (뒤를 어둡게 막는다). body에 내용을 넣는다. 닫기는 Object.Destroy(반환값)
        /// </summary>
        public static GameObject Popup(Transform root, string title, Vector2 size, out RectTransform body, Action onClose = null)
        {
            var dim = Panel(root, "Popup:" + title, new Color(0, 0, 0, 0.6f));
            Stretch(dim);
            var box = Panel(dim, "Box", PanelColor, Line);
            Place(box, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            var t = Label(box, title, 34, TextAnchor.MiddleLeft, bold: true);
            Place(t, new Vector2(0, 1), new Vector2(40, -24), new Vector2(size.x - 200, 56), new Vector2(0, 1));
            var close = Button(box, "닫기", null, 24);
            Place(close, new Vector2(1, 1), new Vector2(-24, -24), new Vector2(120, 52), new Vector2(1, 1));
            GameObject popup = dim.gameObject;
            close.onClick.AddListener(() => { Object.Destroy(popup); onClose?.Invoke(); });
            body = new GameObject("Body", typeof(RectTransform)).GetComponent<RectTransform>();
            body.SetParent(box, false);
            Stretch(body);
            body.offsetMin = new Vector2(40, 32);
            body.offsetMax = new Vector2(-40, -100);
            return popup;
        }

        /// <summary>
        /// 앵커 기준 배치. anchor (0,0)=왼쪽 아래, (1,1)=오른쪽 위. pivot을 안 주면 (0.5, anchor.y)
        /// </summary>
        public static void Place(Component c, Vector2 anchor, Vector2 position, Vector2 size, Vector2? pivot = null)
        {
            var rt = (RectTransform)c.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot ?? new Vector2(0.5f, anchor.y);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        public static void Stretch(RectTransform rt, float inset = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        public static void Clear(Transform t)
        {
            foreach (Transform child in t) Object.Destroy(child.gameObject);
        }
    }
}
