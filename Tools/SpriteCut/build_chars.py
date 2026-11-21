# 캐릭터 시트 -> 게임 프레임 (docs/art-guide.md "큰 캐릭터 시트에서 만들기"). 필요: pip install pillow numpy scipy
"""배경을 지운 캐릭터 시트(segment.py 결과)에서 프레임을 잘라 게임 크기(키 92px, 캔버스 높이 96)로 줄인다.
- 같은 캐릭터는 한 배율로 줄인다 (프레임마다 크기가 달라지지 않게)
- 칸(셀) 창 가운데를 캔버스 가운데에, 그 줄의 발끝(가장 아래)을 캔버스 바닥에 맞춘다 (걸을 때 흔들리지 않게)
- 줄일 때는 BOX(평균) + 알파를 0/255로 잘라 도트처럼 또렷하게"""
import os
import sys
import numpy as np
from PIL import Image
from scipy import ndimage

S = sys.argv[1]  # 작업 폴더 (player_cut.png, cassia_cut.png가 있는 곳)
OUT = sys.argv[2]  # 결과 폴더
H, BODY = 96, 92


def clean(img):
    """작은 조각(점·찌꺼기) 지우기"""
    a = np.array(img)
    lab, n = ndimage.label(a[:, :, 3] > 0)
    sizes = ndimage.sum(np.ones_like(lab), lab, range(1, n + 1))
    small = np.isin(lab, [i + 1 for i, s in enumerate(sizes) if s < 300])
    a[small, 3] = 0
    # 가장자리 흰 테두리: 배경과 맞닿은 4px 띠 안의 밝은 무채색 픽셀은 배경이 섞인 것 -> 지운다
    for _ in range(1):
        fg = a[:, :, 3] > 0
        band = fg & ndimage.binary_dilation(~fg, iterations=2)
        rgb = a[:, :, :3].astype(int)
        light = (rgb.min(axis=2) > 190) & (rgb.max(axis=2) - rgb.min(axis=2) < 40)
        a[band & light, 3] = 0
    return Image.fromarray(a, 'RGBA')


def alpha_box(a):
    ys, xs = np.where(a[:, :, 3] > 0)
    return xs.min(), ys.min(), xs.max() + 1, ys.max() + 1


def shrink(crop, scale):
    """미리 곱한 알파로 평균 축소 후 알파를 0/255로"""
    a = np.array(crop).astype(np.float64)
    al = a[:, :, 3:4] / 255.0
    pre = np.concatenate([a[:, :, :3] * al, a[:, :, 3:4]], axis=2).astype(np.uint8)
    w, h = max(1, round(crop.width * scale)), max(1, round(crop.height * scale))
    small = np.array(Image.fromarray(pre, 'RGBA').resize((w, h), Image.Resampling.BOX)).astype(np.float64)
    alpha = small[:, :, 3:4]
    rgb = np.where(alpha > 0, small[:, :, :3] * 255.0 / np.maximum(alpha, 1), 0)
    keep = alpha[:, :, 0] >= 110
    out = np.zeros((h, w, 4), np.uint8)
    out[:, :, :3] = np.clip(rgb, 0, 255).astype(np.uint8)
    out[:, :, 3] = np.where(keep, 255, 0)
    return Image.fromarray(out, 'RGBA')


def frames(sheet, cells, rows, canvas_w, scale):
    """cells: 칸 창 x 범위들, rows: {이름: (y0, y1)}. 줄마다 [프레임 이미지]"""
    result = {}
    for name, (y0, y1) in rows.items():
        crops = [sheet.crop((x0, y0, x1, y1)) for x0, x1 in cells[name]]
        bottoms = [alpha_box(np.array(c))[3] for c in crops]
        base = max(bottoms)  # 그 줄의 발끝
        out = []
        for c, (x0, x1) in zip(crops, cells[name]):
            small = shrink(c, scale)
            canvas = Image.new('RGBA', (canvas_w, H), (0, 0, 0, 0))
            # 칸 창 가운데 -> 캔버스 가운데, 발끝 -> 바닥
            left = round(canvas_w / 2 - small.width / 2)
            top = round(H - base * scale)
            canvas.alpha_composite(small, (left, top)) if 0 <= left and left + small.width <= canvas_w and top >= 0 else canvas.paste(small, (left, top), small)
            out.append(canvas)
        result[name] = out
    return result


os.makedirs(OUT, exist_ok=True)

# ---- 플레이어 (1536x1024 시트: 정면 / 오른쪽 / 뒤 / 왼쪽, 5프레임씩) ----
player = clean(Image.open(os.path.join(S, 'player_cut.png')))
cols = [(20, 186), (186, 355), (355, 525), (525, 687), (687, 860)]
rows = {'front': (8, 222), 'right': (226, 424), 'back': (426, 623), 'left': (623, 816)}
tallest = max(alpha_box(np.array(player.crop((x0, y0, x1, y1))))[3] - alpha_box(np.array(player.crop((x0, y0, x1, y1))))[1]
              for (y0, y1) in rows.values() for (x0, x1) in cols)
pscale = BODY / tallest
pf = frames(player, {k: cols for k in rows}, rows, 64, pscale)
for d, imgs in pf.items():
    for i, im in enumerate(imgs):
        im.save(os.path.join(OUT, f'player_{d}_{i}.png'))
print('player scale', round(pscale, 3), 'tallest', tallest)

# ---- 카시아 (1774x887: 서 있기 / 손짓 / 기쁨 / 쓰러짐) ----
cassia = clean(Image.open(os.path.join(S, 'cassia_cut.png')))
a = np.array(cassia)[:, :, 3] > 0
proj = a[170:760, 800:900].sum(axis=0)
split = 800 + int(np.argmin(proj))  # 손짓과 기쁨 사이
ccells = [(16, 434), (439, split)]
cbox = [alpha_box(np.array(cassia.crop((x0, 150, x1, 770)))) for x0, x1 in ccells]
ctall = max(b[3] - b[1] for b in cbox)
cscale = BODY / ctall
cf = frames(cassia, {'idle': ccells}, {'idle': (150, 770)}, 80, cscale)
stand, talk = cf['idle']
stand.save(os.path.join(OUT, 'cassia_idle_0.png'))
# 숨쉬기: 윗몸(위 70%)을 1px 내린 프레임
b = np.array(stand)
ys = np.where(b[:, :, 3].any(axis=1))[0]
cut = ys[0] + int((ys[-1] - ys[0]) * 0.7)
breathe = b.copy()
breathe[ys[0]:cut + 1] = 0
breathe[ys[0] + 1:cut + 1] = b[ys[0]:cut]
Image.fromarray(breathe, 'RGBA').save(os.path.join(OUT, 'cassia_idle_1.png'))
talk.save(os.path.join(OUT, 'cassia_talk.png'))
print('cassia scale', round(cscale, 3), 'tallest', ctall, 'split', split)
