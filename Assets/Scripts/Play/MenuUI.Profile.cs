using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static Executor.Play.Theme;

namespace Executor.Play
{
    /// <summary>
    /// 프로필 창 (시안 Executor/profile_ui, profile.exe — 프로필).
    /// 왼쪽 열: 사진 160 + [사진 바꾸기][기본으로] + 닉네임·이메일 + 코인·클리어·별.
    /// 오른쪽: 닉네임(중복 확인·변경) → 자기소개(0/255, 저장) → 꾸미기 4줄(스킨·테마·커서·델타, 가진 것 칩 + 상점 →).
    /// 사진 바꾸기 창: 원본 위 정사각형 자르기 틀(끌어서 옮기고 모서리로 크기) + 96px·32px 미리보기.
    /// </summary>
    public partial class MenuUI
    {
        /// <summary>프로필 화면에서 쓰는 동작. 문자열 결과는 그 칸 아래에 보여 줄 안내 ("✓"로 시작하면 성공)</summary>
        public class ProfileActions
        {
            public Func<string, Task<string>> CheckNickname;
            public Func<string, Task<string>> SaveNickname;
            public Func<string, Task<string>> SaveComment;
            /// <summary>(스킨, 테마, 커서, 델타 꾸미기) 아이템 ID로 장착. null이면 기본</summary>
            public Action<long?, long?, long?, long?> Equip;
            /// <summary>[사진 바꾸기]: 파일을 고르고 자르기 창을 연다</summary>
            public Action ChangePhoto;
            /// <summary>[기본으로]: 사진을 지운다</summary>
            public Action ResetPhoto;
            /// <summary>"상점 →" (아이템 종류)</summary>
            public Action<string> OpenShop;
        }

        private static readonly Color PhotoBack = Hex(0x2d3142);

        /// <summary>작업 표시줄의 사진 칸 (메인·스테이지 선택 화면에 하나씩)</summary>
        private readonly List<RawImage> _taskAvatars = new List<RawImage>();

        /// <summary>작업 표시줄 사진을 바꾼다 (null이면 기본 실루엣)</summary>
        public void SetAvatar(Texture2D avatar)
        {
            foreach (RawImage r in _taskAvatars)
                if (r != null) r.texture = avatar != null ? avatar : AvatarStore.Default;
        }

        /// <summary>사진 칸: 바탕 + 사진(없으면 기본) + 네 귀퉁이 하늘색 꺾쇠 (corner = 꺾쇠 길이, 0이면 없음)</summary>
        private static RawImage Photo(Transform parent, float x, float y, float size, Texture2D texture, float corner, float thick = 3 * S)
        {
            RectTransform box = Node(parent, "Photo");
            At(box, x, y, size, size);
            Image back = Fill(box, "Back", PhotoBack);
            Inset(back, 0, 0, 0, 0);
            var raw = new GameObject("Image", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            raw.transform.SetParent(box, false);
            raw.raycastTarget = false;
            raw.texture = texture != null ? texture : AvatarStore.Default;
            Inset(raw, 0, 0, 0, 0);
            RectTransform line = Node(box, "Edge");
            Inset(line, 0, 0, 0, 0);
            Border(line, Ink, 2 * S);
            if (corner > 0) Corners(box, size, size, corner, thick, Cyan);
            return raw;
        }

        /// <summary>네 귀퉁이 꺾쇠 (바깥으로 corner*0.6 만큼 나간다)</summary>
        private static void Corners(RectTransform box, float w, float h, float corner, float thick, Color color)
        {
            float o = corner * 0.6f;
            foreach (var (cx, cy, sx, sy) in new[] { (0f, 0f, 1, 1), (1f, 0f, -1, 1), (0f, 1f, 1, -1), (1f, 1f, -1, -1) })
            {
                float left = cx * w - (sx > 0 ? o : corner - o);
                float top = cy * h - (sy > 0 ? o : corner - o);
                Image hLine = Fill(box, "CornerH", color);
                At(hLine, left, sy > 0 ? top : top + corner - thick, corner, thick);
                Image vLine = Fill(box, "CornerV", color);
                At(vLine, sx > 0 ? left : left + corner - thick, top, thick, corner);
            }
        }

        private static float TextW(Transform parent, string text, float size, bool bold)
        {
            Text probe = Label(parent, text, size, Fg, TextAnchor.MiddleLeft, bold: bold);
            float w = probe.preferredWidth;
            Destroy(probe.gameObject);
            return w;
        }

        /// <summary>결과 문구를 안내 줄에 (✓ = 초록, 그 밖 = 빨강). 실행 중에는 버튼을 막는다</summary>
        private static async void RunStatus(Button button, Text status, string neutral, Func<Task<string>> action)
        {
            if (button != null) button.interactable = false;
            status.text = "처리 중...";
            status.color = Muted;
            try
            {
                string message = await action();
                if (status == null) return;
                bool ok = message.StartsWith("✓");
                status.text = string.IsNullOrEmpty(message) ? neutral : message;
                status.color = ok ? Green : Red;
            }
            finally
            {
                if (button != null) button.interactable = true;
            }
        }

        public void ShowProfileEditor(Data.MyProfileResponse p, Data.ProfileDetailResponse stats,
            IReadOnlyList<Data.MyItem> items, Texture2D avatar, ProfileActions actions)
        {
            ClosePopups();
            const float width = 1104, height = 620;
            DesktopWindow("profile.exe — 프로필", null, "profile.exe", 152, 24, width, height, false, out RectTransform body);
            float W = width * S - WinBorder * 2, H = height * S - WinBorder * 2 - WinTitleH;

            // ---------- 왼쪽 열 ----------
            const float asideW = 300 * S;
            Image aside = Fill(body, "Aside", Night);
            At(aside, 0, 0, asideW, H);
            Image split = Fill(body, "Split", TrackLine);
            At(split, asideW - 2 * S, 0, 2 * S, H);
            Transform a = aside.transform;
            float y = 24 * S;
            Photo(a, (asideW - 160 * S) / 2, y, 160 * S, avatar, 10 * S);
            y += 160 * S + 14 * S;

            float changeW = TextW(a, "사진 바꾸기", 12 * S, true) + 32 * S, resetW = TextW(a, "기본으로", 12 * S, true) + 28 * S;
            float bx = (asideW - changeW - 8 * S - resetW) / 2;
            PrimaryButton(a, "사진 바꾸기", 12 * S, () => actions.ChangePhoto?.Invoke(), out RectTransform change);
            At(change, bx, y, changeW, 36 * S);
            GhostButton(a, "기본으로", 12 * S, () => actions.ResetPhoto?.Invoke(), out RectTransform reset);
            At(reset, bx + changeW + 8 * S, y, resetW, 36 * S);
            if (p.AvatarVersion == null) reset.gameObject.SetActive(false);  // 이미 기본 사진
            y += 36 * S + 14 * S;
            Text tip = Label(a, FilePicker.Supported ? "PNG · JPG, 정사각형 이미지를 추천해요" : "이 PC에서는 사진을 고를 수 없어요", 12 * S, Dim, TextAnchor.MiddleCenter);
            At(tip, 0, y, asideW, 16 * S);
            y += 16 * S + 18 * S;
            string name = string.IsNullOrEmpty(p.Nickname) ? "닉네임 미정" : p.Nickname;
            Text title = Label(a, $"{name} <size={Mathf.RoundToInt(12 * S)}><color=#8b98b8>집행자</color></size>", 24 * S, Fg,
                TextAnchor.MiddleCenter, bold: true);
            At(title, 0, y, asideW, 28 * S);
            y += 28 * S + 4 * S;
            Text email = Label(a, p.Email ?? "", 12 * S, Muted, TextAnchor.MiddleCenter, UiKit.Mono);
            At(email, 0, y, asideW, 16 * S);

            var cells = new[]
            {
                ("코인", p.Coin.ToString(), Yellow),
                ("클리어", stats?.ClearedStageCount.ToString() ?? "-", Green),
                ("별", stats == null ? "-" : $"★{stats.TotalStars}", Yellow),
            };
            float cellGap = 6 * S, cellW = (asideW - 44 * S - cellGap * 2) / 3, cellH = 60 * S, cellY = H - 24 * S - cellH;
            for (int i = 0; i < cells.Length; i++)
            {
                var (label, value, color) = cells[i];
                float cx = 22 * S + i * (cellW + cellGap);
                Text l = Label(a, label, 12 * S, Muted, TextAnchor.MiddleLeft);
                At(l, cx + 10 * S, cellY + 8 * S, cellW - 20 * S, 16 * S);
                Text v = Label(a, value, 24 * S, color, TextAnchor.MiddleLeft, UiKit.Mono);
                At(v, cx + 10 * S, cellY + 8 * S + 16 * S, cellW - 20 * S, 28 * S);
            }

            // ---------- 오른쪽 ----------
            float x0 = asideW + 24 * S, rw = W - asideW - 48 * S;
            y = 20 * S;

            // 닉네임
            Text nickLabel = Label(body, "닉네임", 12 * S, Muted, TextAnchor.MiddleLeft);
            At(nickLabel, x0, y, rw, 16 * S);
            Text nickStatus = Label(body, "2~20자", 12 * S, Dim, TextAnchor.MiddleLeft);
            At(nickStatus, x0, y + 22 * S, rw, 16 * S);
            y += 44 * S;
            float saveW = 56 * S, checkW = 84 * S;
            InputField nick = UiKit.Input(body, "닉네임", limit: 20, size: Mathf.RoundToInt(16 * S));
            At(nick, x0, y, rw - saveW - checkW - 16 * S, 36 * S);
            FieldFrame.Apply(nick);
            nick.text = p.Nickname ?? "";
            Button check = GhostButton(body, "중복 확인", 12 * S, null, out RectTransform checkRt);
            At(checkRt, x0 + rw - saveW - 8 * S - checkW, y, checkW, 36 * S);
            Button save = PrimaryButton(body, "변경", 12 * S, null, out RectTransform saveRt);
            At(saveRt, x0 + rw - saveW, y, saveW, 36 * S);
            check.onClick.AddListener(() => RunStatus(check, nickStatus, "2~20자", () => actions.CheckNickname(nick.text.Trim())));
            save.onClick.AddListener(() => RunStatus(save, nickStatus, "2~20자", () => actions.SaveNickname(nick.text.Trim())));
            y += 36 * S + 16 * S;

            // 자기소개
            Text bioLabel = Label(body, "자기소개", 12 * S, Muted, TextAnchor.MiddleLeft);
            At(bioLabel, x0, y, rw, 16 * S);
            Text bioStatus = Label(body, "255자 이하 · 비우면 지워져요", 12 * S, Dim, TextAnchor.MiddleLeft);
            At(bioStatus, x0, y + 22 * S, rw, 16 * S);
            y += 44 * S;
            InputField bio = UiKit.Input(body, "나를 한 줄로 소개해 보세요", limit: 255, size: Mathf.RoundToInt(12 * S));
            At(bio, x0, y, rw, 60 * S);
            FieldFrame.Apply(bio);
            bio.lineType = InputField.LineType.MultiLineNewline;
            foreach (Text t in new[] { bio.textComponent, bio.placeholder as Text })
            {
                if (t == null) continue;
                t.alignment = TextAnchor.UpperLeft;
                t.horizontalOverflow = HorizontalWrapMode.Wrap;
                t.font = UiKit.Regular;
                t.rectTransform.offsetMin = new Vector2(10 * S, 8 * S);
                t.rectTransform.offsetMax = new Vector2(-10 * S, -8 * S);
            }
            bio.text = p.Comment ?? "";
            y += 60 * S + 6 * S;
            Text count = Label(body, $"{bio.text.Length} / 255", 12 * S, Dim, TextAnchor.MiddleLeft, UiKit.Mono);
            At(count, x0, y + 8 * S, 200 * S, 16 * S);
            bio.onValueChanged.AddListener(v => count.text = $"{v.Length} / 255");
            Button saveBio = PrimaryButton(body, "저장", 12 * S, null, out RectTransform saveBioRt);
            At(saveBioRt, x0 + rw - saveW, y, saveW, 32 * S);
            saveBio.onClick.AddListener(() => RunStatus(saveBio, bioStatus, "255자 이하 · 비우면 지워져요", () => actions.SaveComment(bio.text)));
            y += 32 * S + 16 * S;

            // 꾸미기 (PUT이라 네 값을 모두 보낸다)
            Text deco = Label(body, "꾸미기", 12 * S, Muted, TextAnchor.MiddleLeft, bold: true);
            At(deco, x0, y, rw, 16 * S);
            y += 20 * S;
            long? skin = p.EquippedSkinId, theme = p.EquippedThemeId, cursor = p.EquippedCursorId, delta = p.EquippedDeltaId;
            var rows = new (string label, string type, long? equipped, Action<long?> pick)[]
            {
                ("스킨", Data.ItemTypes.Skin, skin, id => actions.Equip(id, theme, cursor, delta)),
                ("테마", Data.ItemTypes.Theme, theme, id => actions.Equip(skin, id, cursor, delta)),
                ("커서", Data.ItemTypes.Cursor, cursor, id => actions.Equip(skin, theme, id, delta)),
                ("델타 꾸미기", Data.ItemTypes.Delta, delta, id => actions.Equip(skin, theme, cursor, id)),
            };
            foreach (var (label, type, equipped, pick) in rows)
            {
                DecoRow(body, x0, y, rw, label, type, equipped, items, pick, () => actions.OpenShop?.Invoke(type));
                y += 48 * S;
            }
        }

        /// <summary>꾸미기 한 줄: 이름+지금 값(96) | "기본" + 가진 것 칩 (장착한 것 청록 채움) | 상점 →</summary>
        private static void DecoRow(RectTransform body, float x, float y, float w, string label, string type, long? equipped,
            IReadOnlyList<Data.MyItem> items, Action<long?> onPick, Action openShop)
        {
            var owned = items.Where(i => i.ItemType == type).ToList();
            string current = equipped == null ? "기본" : owned.FirstOrDefault(i => i.ItemId == equipped)?.Name ?? $"#{equipped}";
            Text head = Label(body, $"<b>{label}</b>\n<color=#8b98b8>지금: {current}</color>", 12 * S, Fg, TextAnchor.MiddleLeft);
            head.lineSpacing = 1.1f;
            At(head, x, y + 8 * S, 96 * S, 32 * S);
            Image line = Fill(body, "RowLine", TrackLine);
            At(line, x, y + 48 * S - 1 * S, w, 1 * S);

            float shopW = 60 * S;
            RectTransform shop = Node(body, "Shop:" + type);
            At(shop, x + w - shopW, y + 8 * S, shopW, 32 * S);
            HoverArea(shop, "Hover", Hex(0x22d3ee, 0.10f), openShop, out Image area);
            Inset(area, 0, 0, 0, 0);
            Text shopText = Label(shop, "상점 →", 12 * S, Cyan, TextAnchor.MiddleRight);
            Inset(shopText, 0, 0, 4 * S, 0);

            float cx = x + 106 * S, limit = x + w - shopW - 10 * S;
            var chips = new List<(long? id, string name)> { (null, "기본") };
            chips.AddRange(owned.Select(i => ((long?)i.ItemId, i.Name)));
            int shown = 0;
            foreach (var (id, name) in chips)
            {
                float cw = TextW(body, name, 12 * S, true) + 24 * S + 14 * S;
                if (cx + cw > limit - (shown < chips.Count - 1 ? 50 * S : 0)) break;
                bool on = id == equipped;
                long? pickId = id;
                RectTransform chip = Node(body, "Chip:" + name);
                At(chip, cx, y + 8 * S, cw, 32 * S);
                Image face = Fill(chip, "Face", on ? Cyan : Night, raycast: !on);
                Inset(face, 0, 0, 0, 0);
                if (!on) Clickable(face, () => onPick(pickId), Night, Hex(0x22d3ee, 0.20f));
                Image dot = Fill(chip, "Dot", on ? Ink : id == null ? Dim : Cyan);
                At(dot, 12 * S, 12 * S, 8 * S, 8 * S);
                Text t = Label(chip, name, 12 * S, on ? Ink : Fg, TextAnchor.MiddleLeft, bold: true);
                Inset(t, 26 * S, 0, 0, 0);
                cx += cw + 6 * S;
                shown++;
            }
            if (owned.Count == 0)
            {
                Text none = Label(body, "아직 가진 게 없어요", 12 * S, Dim, TextAnchor.MiddleLeft);
                At(none, cx + 4 * S, y + 8 * S, 200 * S, 32 * S);
            }
            else if (shown < chips.Count)
            {
                Text more = Label(body, $"외 {chips.Count - shown}개", 12 * S, Muted, TextAnchor.MiddleLeft);
                At(more, cx + 4 * S, y + 8 * S, 60 * S, 32 * S);
            }
        }

        // ================= 사진 바꾸기 =================

        /// <summary>
        /// 자르기 창. 원본을 240x240 칸에 맞춰 보여 주고, 정사각형 틀을 끌어서 옮기거나 오른쪽 아래 손잡이로 크기를 바꾼다.
        /// [적용]하면 원본 픽셀 사각형(왼쪽 아래 기준)을 넘긴다. 이 창만 닫고 프로필 창은 남긴다.
        /// </summary>
        public void ShowAvatarCrop(Texture2D source, string fileLabel, Action<RectInt> onApply, Action onPickAnother)
        {
            const float width = 484, height = 424;
            GameObject dialog = null;
            Action close = () => { if (dialog != null) Destroy(dialog); };
            WindowFrame(_popupRoot, "사진 바꾸기", null, (1280 - width) / 2, 110, width, height, true, close, out RectTransform body, out dialog);
            Transform bar = dialog.transform.Find("Window/TitleBar");
            for (int i = 0; i < 2 && bar != null; i++) bar.Find("WindowButton" + i)?.gameObject.SetActive(false);

            // 원본 칸
            const float area = 240 * S, ax = 22 * S, ay = 20 * S;
            Image areaBack = Fill(body, "Source", Ink);
            At(areaBack, ax, ay, area, area);
            float fit = Mathf.Min(area / source.width, area / source.height);
            float iw = source.width * fit, ih = source.height * fit, ix = ax + (area - iw) / 2, iy = ay + (area - ih) / 2;
            var img = new GameObject("Image", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            img.transform.SetParent(body, false);
            img.texture = source;
            img.raycastTarget = false;
            At(img, ix, iy, iw, ih);

            // 미리보기
            float px = ax + area + 16 * S, pw = width * S - WinBorder * 2 - 22 * S - px;
            Text previewLabel = Label(body, "미리보기", 12 * S, Fg, TextAnchor.MiddleCenter);
            At(previewLabel, px, ay + 24 * S, pw, 16 * S);
            RawImage big = Photo(body, px + (pw - 96 * S) / 2, ay + 48 * S, 96 * S, source, 10 * S);
            RawImage small = Photo(body, px + (pw - 32 * S) / 2, ay + 48 * S + 96 * S + 16 * S, 32 * S, source, 8 * S, 2 * S);
            Text taskLabel = Label(body, "작업 표시줄", 12 * S, Muted, TextAnchor.MiddleCenter);
            At(taskLabel, px, ay + 48 * S + 96 * S + 16 * S + 32 * S + 8 * S, pw, 16 * S);

            // 자르기 틀 (원본 표시 좌표, 왼쪽 위 기준)
            float size = Mathf.Min(iw, ih), minSize = Mathf.Min(24 * S, size);
            float cx = ix + (iw - size) / 2, cy = iy + (ih - size) / 2;
            RectTransform crop = Node(body, "Crop");
            Image cropHit = Fill(crop, "Hit", new Color(0, 0, 0, 0), raycast: true);
            Inset(cropHit, 0, 0, 0, 0);
            RectTransform cropLine = Node(crop, "Line");
            Inset(cropLine, 0, 0, 0, 0);
            Border(cropLine, Cyan, 2 * S);
            Image handle = Fill(crop, "Handle", Cyan, raycast: true);
            handle.rectTransform.anchorMin = handle.rectTransform.anchorMax = new Vector2(1, 0);
            handle.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            handle.rectTransform.sizeDelta = new Vector2(12 * S, 12 * S);
            handle.rectTransform.anchoredPosition = Vector2.zero;

            void Redraw()
            {
                At(crop, cx, cy, size, size);
                var uv = new Rect((cx - ix) / iw, 1 - (cy - iy + size) / ih, size / iw, size / ih);
                big.uvRect = uv;
                small.uvRect = uv;
            }
            Redraw();

            float scale = GetComponentInParent<Canvas>()?.rootCanvas.scaleFactor ?? 1f;
            OnDrag(cropHit.gameObject, d =>
            {
                cx = Mathf.Clamp(cx + d.x / scale, ix, ix + iw - size);
                cy = Mathf.Clamp(cy - d.y / scale, iy, iy + ih - size);  // 화면 y는 위로, 배치 y는 아래로
                Redraw();
            });
            OnDrag(handle.gameObject, d =>
            {
                float grow = (d.x - d.y) / 2 / scale;  // 오른쪽·아래로 끌면 커진다
                size = Mathf.Clamp(size + grow, minSize, Mathf.Min(ix + iw - cx, iy + ih - cy));
                Redraw();
            });

            // 안내 + 버튼
            float y = ay + area + 18 * S, inner = width * S - WinBorder * 2 - 44 * S;
            Text help = Label(body, "네모를 끌어서 보일 부분을 고르고, 모서리를 끌어 크기를 바꿔요.", 12 * S, Fg, TextAnchor.MiddleLeft);
            At(help, ax, y, inner, 16 * S);
            Text info = Label(body, $"{fileLabel} · {source.width} × {source.height}", 12 * S, Dim, TextAnchor.MiddleLeft, UiKit.Mono);
            At(info, ax, y + 22 * S, inner, 16 * S);
            y += 52 * S;
            GhostButton(body, "다른 파일 고르기", 12 * S, () => { close(); onPickAnother?.Invoke(); }, out RectTransform another);
            At(another, ax, y, 126 * S, 36 * S);
            PrimaryButton(body, "적용", 12 * S, () =>
            {
                var uv = new Rect((cx - ix) / iw, 1 - (cy - iy + size) / ih, size / iw, size / ih);
                int side = Mathf.Max(1, Mathf.RoundToInt(uv.width * source.width));
                int sx = Mathf.Clamp(Mathf.RoundToInt(uv.x * source.width), 0, source.width - side);
                int sy = Mathf.Clamp(Mathf.RoundToInt(uv.y * source.height), 0, source.height - side);
                close();
                onApply?.Invoke(new RectInt(sx, sy, side, Mathf.Min(side, source.height)));
            }, out RectTransform apply);
            At(apply, ax + inner - 120 * S, y, 120 * S, 36 * S);
            GhostButton(body, "취소", 12 * S, close, out RectTransform cancel);
            At(cancel, ax + inner - 120 * S - 10 * S - 56 * S, y, 56 * S, 36 * S);
        }

        /// <summary>끌기 이벤트 (화면 픽셀 이동량)</summary>
        private static void OnDrag(GameObject target, Action<Vector2> onDelta)
        {
            var trigger = target.AddComponent<EventTrigger>();
            var entry = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
            entry.callback.AddListener(e => onDelta(((PointerEventData)e).delta));
            trigger.triggers.Add(entry);
        }
    }
}
