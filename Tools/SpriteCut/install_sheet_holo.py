# 하늘색 홀로그램 시트(1536x1024, 배경에 체크 무늬가 그려진 RGB) -> 1챕터 맵 세트 Resources/Pixel/map/helloworld/
"""사용: python install_sheet_holo.py <시트.png> <Resources/Pixel/map/helloworld 폴더> [미리보기.png]

1. 배경: 테두리에서 이어진 무채색 밝은 칸(체크 무늬)을 지운다. 진한 외곽선이 있는 그림은 그 안이 그대로 남는다.
2. 빛 효과(홀로그램 사람, 체크포인트 기둥의 유리)는 체크 무늬가 그림 안에 비치므로, 채도로 알파를 다시 만든다
   (무채색 = 투명, 파랑이 진할수록 불투명, 진한 외곽선은 불투명).
3. 칸 크기: 가로 64px (ArtCatalog.CellSprite가 가로 1칸으로 그린다. 키가 큰 그림은 칸 위로 솟는다).
위치는 아래 표(시트 좌표)에서 고친다. docs/art-guide.md "챕터별 맵 세트"."""
import os
import sys
import numpy as np
from PIL import Image, ImageEnhance
from scipy import ndimage

SRC, DST = sys.argv[1], sys.argv[2]
PREVIEW = sys.argv[3] if len(sys.argv) > 3 else None
T = 64

# 게임 파일 이름 -> (x0, y0, x1, y1) 시트 좌표. 범위 안의 그림 영역으로 다시 좁힌다
TILES = {  # 칸을 꽉 채우는 타일 (64x64로 맞춘다)
    'wall_front_0': (46, 30, 190, 178),
    'wall_front_1': (206, 31, 353, 177),
    'wall_front_2': (403, 31, 549, 176),
    'wall_front_3': (575, 31, 722, 177),
    'wall_front_4': (743, 31, 864, 177),
    'floor_plain': (121, 330, 193, 407),
    'floor_trace_l': (303, 330, 383, 407),
    'floor_trace_h': (48, 330, 121, 407),
    'floor_vias': (785, 330, 863, 407),
}
OBJECTS = {  # 가로 64px로 줄이고 비율 유지
    'exit_gate_64x96': (1058, 29, 1199, 250),
    'shutter_locked': (892, 29, 1035, 237),
    'computer_infected_64x72': (46, 443, 198, 592),
    'computer_fixed_64x72': (235, 443, 393, 592),
    'firewall': (1168, 443, 1326, 586),
    'firewall_damaged': (1350, 445, 1492, 586),
    'drive': (67, 628, 180, 730),
    'keycard': (729, 626, 791, 728),
    'data_shard': (1152, 618, 1254, 732),
    'checkpoint_on': (1162, 765, 1269, 932),
    'null_entity': (53, 756, 196, 946),
}
# 칸보다 작게 그릴 물건의 가로 px (나머지는 64)
SMALL = {'keycard': 28, 'data_shard': 44, 'drive': 54, 'null_entity': 54}
# 빛 효과 영역 (이 안은 채도로 알파를 만든다). 이름 -> 시트 좌표 y가 이 값보다 위인 부분만 (None이면 전체)
SOFT = {'null_entity': None, 'checkpoint_on': 890}

a = np.array(Image.open(SRC).convert('RGB')).astype(np.float64)
H, W, _ = a.shape
mx, mn = a.max(2), a.min(2)
chroma = mx - mn
bgish = (chroma <= 14) & (mn >= 200)
lab, _ = ndimage.label(bgish)
edge = set(np.unique(np.concatenate([lab[0], lab[-1], lab[:, 0], lab[:, -1]]))) - {0}
alpha = np.where(np.isin(lab, list(edge)), 0.0, 1.0)
rgb = a.copy()


def soften(x0, y0, x1, y1):
    """채도 -> 알파, 체크 무늬 회색(약 240)을 빼서 원래 빛 색을 되살린다"""
    c = a[y0:y1, x0:x1]
    ch = chroma[y0:y1, x0:x1]
    al = np.clip((ch - 10) / 70.0, 0, 1)
    al = np.maximum(al, (mx[y0:y1, x0:x1] < 120).astype(float))  # 진한 외곽선
    g = 240.0
    safe = np.maximum(al, 1e-3)[..., None]
    f = np.clip((c - (1 - al[..., None]) * g) / safe, 0, 255)
    rgb[y0:y1, x0:x1] = np.where(al[..., None] > 0.02, f, c)
    alpha[y0:y1, x0:x1] = al


for name, cut in SOFT.items():
    x0, y0, x1, y1 = OBJECTS[name]
    soften(x0, y0, x1, y1 if cut is None else cut)

rgba = np.dstack([np.clip(rgb, 0, 255), alpha * 255]).astype(np.uint8)
sheet = Image.fromarray(rgba, 'RGBA')


def tight(box):
    x0, y0, x1, y1 = box
    m = alpha[y0:y1, x0:x1] > 0.05
    ys, xs = np.nonzero(m)
    return (x0 + xs.min(), y0 + ys.min(), x0 + xs.max() + 1, y0 + ys.max() + 1)


def shrink(img, w, h):
    return img.resize((w, h), Image.LANCZOS)


os.makedirs(DST, exist_ok=True)
for f in os.listdir(DST):
    if f.endswith('.png'):
        os.remove(os.path.join(DST, f))
        meta = os.path.join(DST, f + '.meta')
        name = f[:-4]
        if name not in TILES and name not in OBJECTS and name not in ('wall_top', 'board_edge', 'checkpoint_off') \
                and os.path.exists(meta):
            os.remove(meta)  # 이번 세트에 없는 그림 (예: wall_front_5)

out = {}
for name, box in TILES.items():
    img = sheet.crop(tight(box))
    img = shrink(img, T, T)
    arr = np.array(img)
    arr[..., 3] = 255  # 타일은 칸을 꽉 채운다
    out[name] = Image.fromarray(arr, 'RGBA')

for name, box in OBJECTS.items():
    img = sheet.crop(tight(box))
    w = SMALL.get(name, T)
    h = max(1, round(img.height * w / img.width))
    img = shrink(img, w, h)
    if w < T:  # 작은 물건은 가운데 두고 양옆을 비운다 (칸 너비 64는 그대로)
        pad = Image.new('RGBA', (T, h), (0, 0, 0, 0))
        pad.alpha_composite(img, ((T - w) // 2, 0))
        img = pad
    out[name] = img

# 도착 전 체크포인트: 어둡고 채도 낮게
out['checkpoint_off'] = ImageEnhance.Brightness(ImageEnhance.Color(out['checkpoint_on']).enhance(0.2)).enhance(0.55)

# 벽 덩어리 윗면: 바닥(밝은 흰색)과 구분되게 벽 패널의 진한 청회색 단색
panel = np.array(out['wall_front_2'].convert('RGB')).reshape(-1, 3).astype(int)
dark = panel[(panel.sum(1) > 250) & (panel.sum(1) < 500)]
top = np.zeros((T, T, 4), np.uint8)
top[..., :3] = (np.median(dark, axis=0) if len(dark) else np.array([120, 135, 185])) * 0.55  # 그림자처럼 어둡게
top[..., 3] = 255
out['wall_top'] = Image.fromarray(top, 'RGBA')

# 기판 두께: 바닥을 어둡게, 위 18px만
e = np.array(ImageEnhance.Brightness(out['floor_plain']).enhance(0.45))
e[18:, :, 3] = 0
out['board_edge'] = Image.fromarray(e, 'RGBA')

for name, img in out.items():
    img.save(os.path.join(DST, name + '.png'))
print('saved', len(out), 'files to', DST)

if PREVIEW:
    cols = 8
    rows = (len(out) + cols - 1) // cols
    pv = Image.new('RGBA', (cols * 80, rows * 120), (20, 16, 40, 255))
    for i, (name, img) in enumerate(sorted(out.items())):
        pv.alpha_composite(img, ((i % cols) * 80 + 8, (i // cols) * 120 + 110 - img.height))
    pv.resize((pv.width * 2, pv.height * 2), Image.NEAREST).save(PREVIEW)
