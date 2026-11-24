# 챕터 맵 시트(1536x1024 한 장) -> 게임 맵 세트 Resources/Pixel/map/{theme}/. docs/art-guide.md "챕터별 맵 세트"
"""사용: python install_sheet.py <세트 이름> <시트.png> <Resources/Pixel/map/{theme} 폴더> [미리보기.png]
세트 이름: holo (helloworld, 하늘색 홀로그램) / undercity (언더시티, 보라·네온) / corporation (3챕터 코퍼레이션, 라벤더·분홍 네온) / hologram (튜토리얼, 남색·청록)

1. 배경
   - 시트가 투명(RGBA, 투명한 칸이 20% 넘음)이면 그 알파를 그대로 쓴다.
   - 아니면(배경에 체크 무늬가 그려진 RGB) 테두리에서 이어진 무채색 밝은 칸을 지우고,
     빛 효과 영역(SOFT)은 채도로 알파를 다시 만든다 (무채색 = 투명, 진한 외곽선은 불투명).
2. 타일은 64x64로 꽉 채우고(가로가 긴 타일은 가운데 정사각형), 오브젝트는 가로 64px(작은 물건은 SMALL px)로 줄인다.
   ArtCatalog.CellSprite가 가로 1칸으로 그리고, 키가 큰 그림은 칸 위로 솟는다.
위치는 SETS의 표(시트 좌표 x0, y0, x1, y1)에서 고친다. 범위 안의 그림 영역으로 다시 좁힌다."""
import os
import sys
import numpy as np
from PIL import Image, ImageEnhance
from scipy import ndimage

T = 64

SETS = {
    'holo': {
        'tiles': {
            'wall_front_0': (46, 30, 190, 178),
            'wall_front_1': (206, 31, 353, 177),
            'wall_front_2': (403, 31, 549, 176),
            'wall_front_3': (575, 31, 722, 177),
            'wall_front_4': (743, 31, 864, 177),
            'floor_plain': (121, 330, 193, 407),
            'floor_trace_l': (303, 330, 383, 407),
            'floor_trace_h': (48, 330, 121, 407),
            'floor_vias': (785, 330, 863, 407),
        },
        'objects': {
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
        },
        'small': {'keycard': 28, 'data_shard': 44, 'drive': 54, 'null_entity': 54},
        # 빛 효과 영역: 이름 -> 시트 y가 이 값보다 위인 부분만 (None = 전체). 체크 무늬 시트에서만 쓴다
        'soft': {'null_entity': None, 'checkpoint_on': 890},
        # 벽 윗면 = 벽 패널의 중간 밝기 색 x 이 값 (바닥과 구분되게)
        'wall_top_dim': 0.55,
        # 그림 영역을 좁힐 때 알파 기준, 벽 윗면 색을 고를 밝기 범위(R+G+B 합)
        'tight': 0.05,
        'mid': (250, 500),
    },
    'undercity': {
        'tiles': {
            'wall_front_0': (39, 32, 177, 190),
            'wall_front_1': (189, 32, 377, 190),
            'wall_front_2': (390, 32, 560, 190),
            'wall_front_3': (572, 32, 720, 190),
            'wall_front_4': (732, 32, 857, 190),
            'floor_plain': (100, 340, 176, 409),
            'floor_trace_l': (780, 340, 860, 409),
            'floor_trace_h': (682, 340, 772, 409),
            'floor_vias': (391, 260, 476, 331),
        },
        'objects': {
            'exit_gate_64x96': (1054, 28, 1199, 242),
            'shutter_locked': (892, 28, 1032, 242),
            'computer_infected_64x72': (29, 443, 195, 606),
            'computer_fixed_64x72': (220, 444, 393, 605),
            'firewall': (1139, 444, 1322, 601),
            'firewall_damaged': (1345, 444, 1503, 601),
            'drive': (38, 636, 173, 749),
            'keycard': (731, 634, 801, 742),
            'data_shard': (1152, 633, 1262, 751),
            'checkpoint_on': (1155, 774, 1277, 962),
            'null_entity': (39, 780, 176, 962),
        },
        'small': {'keycard': 28, 'data_shard': 44, 'drive': 54, 'null_entity': 54},
        'soft': {},
        'wall_top_dim': 0.5,
        'tight': 0.2,  # 빛 번짐(옅은 알파)까지 넣지 않게
        'mid': (200, 500),
    },
    'corporation': {
        'tiles': {
            'wall_front_0': (36, 57, 165, 186),
            'wall_front_1': (177, 57, 332, 186),
            'wall_front_2': (350, 57, 510, 186),
            'wall_front_3': (525, 57, 682, 186),
            'wall_front_4': (700, 57, 840, 186),
            'floor_plain': (110, 330, 193, 399),
            'floor_trace_l': (675, 330, 756, 399),
            'floor_trace_h': (286, 330, 358, 399),
            'floor_vias': (36, 330, 101, 399),
        },
        'objects': {
            'exit_gate_64x96': (1052, 53, 1200, 256),
            'shutter_locked': (889, 54, 1030, 256),
            'computer_infected_64x72': (31, 432, 204, 590),
            'computer_fixed_64x72': (231, 431, 417, 590),
            'firewall': (1129, 424, 1328, 577),
            'firewall_damaged': (1345, 424, 1505, 577),
            'drive': (54, 608, 165, 707),
            'keycard': (740, 605, 811, 708),
            'data_shard': (1172, 603, 1280, 705),
            'checkpoint_on': (1167, 730, 1273, 885),
            'null_entity': (32, 737, 213, 882),
        },
        'small': {'keycard': 28, 'data_shard': 44, 'drive': 54, 'null_entity': 60},
        'soft': {},
        # 타일의 투명한 곳(벽 패널의 빈 창)을 이 색으로 채운다 (없으면 검정)
        'tile_back': (34, 26, 62),
        'wall_top_dim': 0.5,
        'tight': 0.2,
        'mid': (200, 500),
    },
    'hologram': {
        'tiles': {
            'wall_front_0': (24, 27, 182, 168),
            'wall_front_1': (202, 27, 385, 168),
            'wall_front_2': (405, 27, 557, 168),
            'wall_front_3': (577, 27, 730, 168),
            'wall_front_4': (749, 27, 867, 168),
            'floor_plain': (102, 320, 181, 401),
            'floor_trace_l': (302, 320, 386, 401),
            'floor_trace_h': (405, 320, 486, 401),
            'floor_vias': (26, 320, 98, 401),
        },
        'objects': {
            'exit_gate_64x96': (1064, 23, 1214, 240),
            'shutter_locked': (895, 22, 1044, 239),
            'computer_infected_64x72': (30, 428, 206, 595),
            'computer_fixed_64x72': (234, 428, 408, 594),
            'firewall': (1162, 437, 1337, 592),
            'firewall_damaged': (1358, 437, 1520, 592),
            'drive': (46, 621, 180, 742),
            'keycard': (737, 620, 816, 739),
            'data_shard': (1150, 623, 1272, 743),
            'checkpoint_on': (1146, 762, 1281, 969),
            'null_entity': (23, 770, 209, 965),
        },
        'small': {'keycard': 28, 'data_shard': 44, 'drive': 54, 'null_entity': 58},
        'soft': {},
        'tile_back': (16, 20, 34),
        'wall_top_dim': 0.55,
        'tight': 0.3,  # 배경의 옅은 색 찌꺼기가 들어가지 않게
        'mid': (150, 450),
    },
}


def load(path, soft, objects):
    """시트 -> (rgb, alpha 0~1)"""
    img = Image.open(path)
    if img.mode == 'RGBA':
        rgba = np.array(img).astype(np.float64)
        alpha = rgba[..., 3] / 255.0
        # 거의 투명한 칸(알파 10 이하, 색 찌꺼기가 섞인 배경 포함)이 20% 넘으면 투명 시트로 본다
        if (alpha <= 10 / 255).mean() > 0.2:
            alpha[alpha <= 10 / 255] = 0
            return rgba[..., :3], alpha
    a = np.array(img.convert('RGB')).astype(np.float64)
    mx, mn = a.max(2), a.min(2)
    chroma = mx - mn
    bgish = (chroma <= 14) & (mn >= 200)
    lab, _ = ndimage.label(bgish)
    edge = set(np.unique(np.concatenate([lab[0], lab[-1], lab[:, 0], lab[:, -1]]))) - {0}
    alpha = np.where(np.isin(lab, list(edge)), 0.0, 1.0)
    rgb = a.copy()
    for name, cut in soft.items():
        x0, y0, x1, y1 = objects[name]
        if cut is not None:
            y1 = cut
        # 채도 -> 알파, 체크 무늬 회색(약 240)을 빼서 원래 빛 색을 되살린다
        c = a[y0:y1, x0:x1]
        al = np.clip((chroma[y0:y1, x0:x1] - 10) / 70.0, 0, 1)
        al = np.maximum(al, (mx[y0:y1, x0:x1] < 120).astype(float))
        safe = np.maximum(al, 1e-3)[..., None]
        f = np.clip((c - (1 - al[..., None]) * 240.0) / safe, 0, 255)
        rgb[y0:y1, x0:x1] = np.where(al[..., None] > 0.02, f, c)
        alpha[y0:y1, x0:x1] = al
    return rgb, alpha


def main():
    name, src, dst = sys.argv[1], sys.argv[2], sys.argv[3]
    preview = sys.argv[4] if len(sys.argv) > 4 else None
    cfg = SETS[name]
    rgb, alpha = load(src, cfg['soft'], cfg['objects'])
    sheet = Image.fromarray(np.dstack([np.clip(rgb, 0, 255), alpha * 255]).astype(np.uint8), 'RGBA')

    def tight(box, threshold=cfg['tight']):
        x0, y0, x1, y1 = box
        ys, xs = np.nonzero(alpha[y0:y1, x0:x1] > threshold)
        return (x0 + xs.min(), y0 + ys.min(), x0 + xs.max() + 1, y0 + ys.max() + 1)

    def square(box):
        """가로가 긴 타일은 가운데 정사각형만"""
        x0, y0, x1, y1 = box
        w, h = x1 - x0, y1 - y0
        if w > h * 1.15:
            c = (x0 + x1) // 2
            return (c - h // 2, y0, c - h // 2 + h, y1)
        return box

    keep = set(cfg['tiles']) | set(cfg['objects']) | {'wall_top', 'board_edge', 'checkpoint_off'}
    os.makedirs(dst, exist_ok=True)
    for f in os.listdir(dst):
        if f.endswith('.png'):
            os.remove(os.path.join(dst, f))
            meta = os.path.join(dst, f + '.meta')
            if f[:-4] not in keep and os.path.exists(meta):
                os.remove(meta)  # 이번 세트에 없는 그림 (예: wall_front_5)

    out = {}
    for n, box in cfg['tiles'].items():
        img = sheet.crop(square(tight(box))).resize((T, T), Image.LANCZOS)
        if 'tile_back' in cfg:
            back = Image.new('RGBA', img.size, tuple(cfg['tile_back']) + (255,))
            back.alpha_composite(img)
            img = back
        arr = np.array(img)
        arr[..., 3] = 255  # 타일은 칸을 꽉 채운다
        out[n] = Image.fromarray(arr, 'RGBA')

    for n, box in cfg['objects'].items():
        img = sheet.crop(tight(box))
        w = cfg['small'].get(n, T)
        h = max(1, round(img.height * w / img.width))
        img = img.resize((w, h), Image.LANCZOS)
        if w < T:  # 작은 물건은 가운데 두고 양옆을 비운다 (칸 너비 64는 그대로)
            pad = Image.new('RGBA', (T, h), (0, 0, 0, 0))
            pad.alpha_composite(img, ((T - w) // 2, 0))
            img = pad
        out[n] = img

    # 도착 전 체크포인트: 어둡고 채도 낮게
    out['checkpoint_off'] = ImageEnhance.Brightness(ImageEnhance.Color(out['checkpoint_on']).enhance(0.2)).enhance(0.55)

    # 벽 덩어리 윗면: 벽 패널의 중간 밝기 색을 어둡게 한 단색 (이음선 없는 면)
    panel = np.array(out['wall_front_2'].convert('RGB')).reshape(-1, 3).astype(int)
    lo, hi = cfg['mid']
    mid = panel[(panel.sum(1) > lo) & (panel.sum(1) < hi)]
    top = np.zeros((T, T, 4), np.uint8)
    top[..., :3] = (np.median(mid, axis=0) if len(mid) else np.array([120, 135, 185])) * cfg['wall_top_dim']
    top[..., 3] = 255
    out['wall_top'] = Image.fromarray(top, 'RGBA')

    # 기판 두께: 바닥을 어둡게, 위 18px만
    e = np.array(ImageEnhance.Brightness(out['floor_plain']).enhance(0.45))
    e[18:, :, 3] = 0
    out['board_edge'] = Image.fromarray(e, 'RGBA')

    for n, img in out.items():
        img.save(os.path.join(dst, n + '.png'))
    print('saved', len(out), 'files to', dst)

    if preview:
        cols = 8
        rows = (len(out) + cols - 1) // cols
        pv = Image.new('RGBA', (cols * 80, rows * 120), (20, 16, 40, 255))
        for i, (n, img) in enumerate(sorted(out.items())):
            pv.alpha_composite(img, ((i % cols) * 80 + 8, (i // cols) * 120 + 110 - img.height))
        pv.resize((pv.width * 2, pv.height * 2), Image.NEAREST).save(preview)


if __name__ == '__main__':
    main()
