# 챕터 맵 시트 -> 게임 맵 세트 (Resources/Pixel/map/{theme}/). docs/art-guide.md "챕터별 맵 세트". 필요: pip install pillow numpy scipy
"""시트(1536x1024, 배경이 칠해진 그림)에서 타일·오브젝트를 잘라 게임 크기로 만든다.
- 타일(벽·바닥): 사각형 그대로 잘라 64x64
- 오브젝트: 자른 상자의 테두리 색(배경)과 비슷하고 테두리에서 이어진 픽셀을 지우고, 가로 64px로 줄인다 (세로는 비율대로)
- 줄일 때는 평균(BOX) + 알파 0/255
사용: python cut_tiles.py <시트 폴더(1챕터.png, 언더시티.png가 있는 곳)> <결과 폴더>"""
import os
import sys
import numpy as np
from PIL import Image, ImageEnhance
from scipy import ndimage

SRC, OUT = sys.argv[1], sys.argv[2]
T = 64

# 좌표: (x0, y0, x1, y1) 시트 픽셀
SETS = {
    'helloworld': {
        'sheet': '1챕터.png',
        'cut': {'tol': 70, 'outline_gap': 16},
        'wall_front': [(22, 22, 186, 182), (437, 22, 602, 182), (614, 22, 765, 182), (775, 22, 917, 182),
                       (198, 22, 309, 182), (309, 22, 420, 182)],
        'wall_top': (36, 278, 86, 328),  # 이음선 없는 안쪽만 (벽 덩어리는 평평한 면)
        'floor_plain': (113, 354, 191, 432),
        'floor_trace_l': (437, 262, 521, 346),
        'floor_trace_h': (437, 354, 521, 438),
        'floor_vias': (20, 354, 104, 438),
        'objects': {
            'exit_gate_64x96': (1100, 22, 1240, 245),
            'shutter_locked': (940, 22, 1080, 245),
            'computer_infected_64x72': (572, 470, 668, 636),
            'keycard': (1050, 468, 1102, 537),
            'firewall': (1402, 455, 1518, 642),
            'checkpoint_off': (768, 870, 952, 985),
            'checkpoint_on': (768, 640, 952, 985),
            'data_shard': (693, 815, 762, 883),
            'drive': (573, 762, 665, 842),
        },
    },
    'undercity': {
        'sheet': '언더시티.png',
        'cut': {'tol': 32, 'outline_gap': 6},
        'wall_front': [(22, 18, 157, 165), (398, 18, 543, 165), (553, 18, 700, 165), (710, 18, 836, 165),
                       (172, 18, 277, 165), (277, 18, 382, 165)],
        'wall_top': (38, 341, 80, 393),  # 이음선 없는 안쪽만
        'floor_plain': (107, 325, 190, 412),
        'floor_trace_l': (297, 325, 380, 412),
        'floor_trace_h': (690, 325, 770, 412),
        'floor_vias': (778, 325, 862, 412),
        'objects': {
            'exit_gate_64x96': (1046, 40, 1190, 235),
            'shutter_locked': (870, 20, 1018, 235),
            'computer_infected_64x72': (570, 445, 665, 610),
            'keycard': (1015, 458, 1072, 520),
            'firewall': (1420, 425, 1515, 612),
            'checkpoint_off': (728, 875, 940, 990),
            'checkpoint_on': (728, 640, 940, 990),
            'data_shard': (668, 812, 722, 872),
            'drive': (545, 742, 632, 832),
            'null_entity': (1050, 650, 1210, 790),
        },
    },
}


def shrink(img, w, h):
    a = np.array(img.convert('RGBA')).astype(np.float64)
    al = a[:, :, 3:4] / 255.0
    pre = np.concatenate([a[:, :, :3] * al, a[:, :, 3:4]], axis=2).astype(np.uint8)
    small = np.array(Image.fromarray(pre, 'RGBA').resize((w, h), Image.Resampling.BOX)).astype(np.float64)
    alpha = small[:, :, 3:4]
    rgb = np.where(alpha > 0, small[:, :, :3] * 255.0 / np.maximum(alpha, 1), 0)
    out = np.zeros((h, w, 4), np.uint8)
    out[:, :, :3] = np.clip(rgb, 0, 255).astype(np.uint8)
    out[:, :, 3] = np.where(alpha[:, :, 0] >= 110, 255, 0)
    return Image.fromarray(out, 'RGBA')


def tile(sheet, box, darken=1.0):
    im = sheet.crop(box)
    if darken != 1.0:
        im = ImageEnhance.Brightness(im).enhance(darken)
    return shrink(im, T, T)


def cut_object(sheet, box, tol=70, outline_gap=16, pad=8):
    """배경 = 테두리에서 이어지고, 테두리 색과 비슷하며, 진한 외곽선이 아닌 픽셀.
    도트 그림은 진한 외곽선이 둘러싸므로 배경 채우기가 외곽선에서 멈춘다 (몸체 색이 배경과 비슷해도 남는다)"""
    x0, y0, x1, y1 = box
    im = sheet.crop((x0 - pad, y0 - pad, x1 + pad, y1 + pad)).convert('RGB')
    a = np.array(im).astype(int)
    border = np.concatenate([a[0], a[-1], a[:, 0], a[:, -1]])
    ref = np.median(border, axis=0)
    lum = a.mean(axis=2)
    dist = np.sqrt(((a - ref) ** 2).sum(axis=2))
    outline = lum < ref.mean() - outline_gap
    bgish = (dist < tol) & ~outline
    lab, n = ndimage.label(bgish)
    edge = set(np.unique(np.concatenate([lab[0], lab[-1], lab[:, 0], lab[:, -1]]))) - {0}
    bg = np.isin(lab, list(edge))
    fg = ndimage.binary_opening(~bg, iterations=1)
    lab2, n2 = ndimage.label(ndimage.binary_dilation(fg, iterations=4))
    if n2 > 1:
        sizes = ndimage.sum(fg, lab2, range(1, n2 + 1))
        keep = 1 + int(np.argmax(sizes))
        fg &= lab2 == keep
    rgba = np.dstack([a.astype(np.uint8), (fg * 255).astype(np.uint8)])
    img = Image.fromarray(rgba, 'RGBA')
    bb = img.getbbox()
    return img.crop(bb) if bb else img


def to_width(img, w=T):
    h = max(1, round(img.height * w / img.width))
    return shrink(img, w, h)


# 칸보다 작은 물건: 이 너비로 줄여 64x64 칸 가운데(아래쪽)에 둔다
SMALL = {'keycard': 36, 'data_shard': 34, 'drive': 52, 'null_entity': 56}


def small_in_cell(img, w):
    s = to_width(img, w)
    if s.height > T:
        s = shrink(img, max(1, round(img.width * T / img.height)), T)
    cell = Image.new('RGBA', (T, T), (0, 0, 0, 0))
    cell.alpha_composite(s, ((T - s.width) // 2, T - s.height - 6))
    return cell


def recolor_fixed(img):
    """감염(빨강) -> 복구(하늘색): 빨강이 강한 픽셀의 색을 하늘색 계열로"""
    a = np.array(img).astype(int)
    r, g, b = a[:, :, 0], a[:, :, 1], a[:, :, 2]
    red = (r > g + 40) & (r > b + 30)
    nr = np.where(red, (g + b) // 4, r)
    ng = np.where(red, np.minimum(255, r * 0.85).astype(int), g)
    nb = np.where(red, np.minimum(255, r * 0.95).astype(int), b)
    a[:, :, 0], a[:, :, 1], a[:, :, 2] = nr, ng, nb
    return Image.fromarray(a.astype(np.uint8), 'RGBA')


def damaged(img):
    """금 간 방화벽: 어둡게 + 대각선 금"""
    out = ImageEnhance.Brightness(img).enhance(0.6)
    a = np.array(out)
    h, w = a.shape[:2]
    for i in range(h):
        x = int(w * 0.3 + (i % 9) * 0.6 + i * 0.25) % w
        if a[i, x, 3] > 0:
            a[i, x, :3] = (20, 14, 30)
    return Image.fromarray(a, 'RGBA')


for theme, spec in SETS.items():
    sheet = Image.open(os.path.join(SRC, spec['sheet'])).convert('RGB')
    out = os.path.join(OUT, theme)
    os.makedirs(out, exist_ok=True)
    for i, box in enumerate(spec['wall_front']):
        tile(sheet, box).save(os.path.join(out, f'wall_front_{i}.png'))
    tile(sheet, spec['wall_top'], 0.72).save(os.path.join(out, 'wall_top.png'))
    for name in ('floor_plain', 'floor_trace_l', 'floor_trace_h', 'floor_vias'):
        tile(sheet, spec[name], 0.55).save(os.path.join(out, f'{name}.png'))
    # 기판 두께: 바닥을 아주 어둡게 한 띠 (위쪽 18px)
    edge = ImageEnhance.Brightness(tile(sheet, spec['floor_plain'], 0.55)).enhance(0.45)
    e = np.array(edge)
    e[18:, :, 3] = 0
    Image.fromarray(e, 'RGBA').save(os.path.join(out, 'board_edge.png'))
    for name, box in spec['objects'].items():
        cut = cut_object(sheet, box, **spec['cut'])
        obj = small_in_cell(cut, SMALL[name]) if name in SMALL else to_width(cut)
        obj.save(os.path.join(out, f'{name}.png'))
        if name == 'computer_infected_64x72':
            recolor_fixed(obj).save(os.path.join(out, 'computer_fixed_64x72.png'))
        if name == 'firewall':
            damaged(obj).save(os.path.join(out, 'firewall_damaged.png'))
    print(theme, sorted(os.listdir(out)))
