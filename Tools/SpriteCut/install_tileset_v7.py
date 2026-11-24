# 타일셋 v7 (Executor/Executor_tileset_v7) -> 게임 테마 세트 (Resources/Pixel/map/{theme}/). docs/art-guide.md "챕터별 맵 세트"
"""사용: python install_v7.py <Executor_tileset_v7 폴더> <Resources/Pixel/map 폴더> [wall_top 타일 이름]
타일(tiles/)은 두 테마가 같이 쓰고, 오브젝트는 helloworld = variants_A(시트 A), undercity = objects(시트 B)."""
import os
import shutil
import sys
import numpy as np
from PIL import Image, ImageEnhance

SRC, DST = sys.argv[1], sys.argv[2]
WALL_TOP = sys.argv[3] if len(sys.argv) > 3 else 'flat'  # flat = 벽 패널 윗부분 색(팔레트)으로 칠한 이음선 없는 면

TILES = {
    'floor_plain': 'floor_plain_a',
    'floor_trace_l': 'floor_quad',
    'floor_trace_h': 'floor_vent_a',
    'floor_vias': 'floor_plain_c',
    'wall_front_0': 'wall_panel_01',
    'wall_front_1': 'wall_panel_02',
    'wall_front_2': 'wall_panel_03',
    'wall_front_3': 'wall_panel_04',
    'wall_front_4': 'wall_panel_05',
    'wall_front_5': 'wall_pipes',
}

# 게임 파일 이름 -> 세트 파일 (README의 map_data 대응 추천을 따르되, 출구는 빛나는 문, 열린 문 그림은 없음 = 열면 사라짐)
OBJECTS = {
    'undercity': ('objects', {
        'exit_gate_64x96': 'door_holo_open',
        'shutter_locked': 'gate_bars_locked',
        'computer_infected_64x72': 'terminal_infected',
        'computer_fixed_64x72': 'terminal_small',
        'keycard': 'keycard_blue',
        'firewall': 'firewall_gate_narrow',
        'checkpoint_on': 'holo_pedestal',
        'data_shard': 'crystal_pink',
        'drive': 'safe_box',
        'null_entity': 'screen_broken',
    }),
    'helloworld': ('variants_A', {
        'exit_gate_64x96': 'a_02',
        'shutter_locked': 'a_04',
        'computer_infected_64x72': 'a_10',
        'computer_fixed_64x72': 'a_09',
        'keycard': 'a_14',
        'firewall': 'a_16',
        'checkpoint_on': 'a_36',
        'data_shard': 'a_35',
        'drive': 'a_30',
        'null_entity': '../objects/screen_broken',  # 시트 A에는 이상 개체 그림이 없다
    }),
}


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


def unlit(img):
    """도착 전 체크포인트: 어둡고 채도 낮게"""
    return ImageEnhance.Brightness(ImageEnhance.Color(img).enhance(0.35)).enhance(0.6)


for theme, (folder, objs) in OBJECTS.items():
    out = os.path.join(DST, theme)
    # 예전 자동 자르기 결과(그림)를 지우고 새로 넣는다 (.meta는 같은 이름이면 그대로 쓰인다)
    if os.path.isdir(out):
        for f in os.listdir(out):
            if f.endswith('.png'):
                os.remove(os.path.join(out, f))
    os.makedirs(out, exist_ok=True)
    for name, src in TILES.items():
        shutil.copyfile(os.path.join(SRC, 'tiles', src + '.png'), os.path.join(out, name + '.png'))
    # 벽 덩어리 윗면: 칸마다 이음선이 보이면 바닥처럼 읽혀서, 이음선 없는 단색 면으로 만든다
    if WALL_TOP == 'flat':
        panel = np.array(Image.open(os.path.join(SRC, 'tiles', 'wall_panel_03.png')).convert('RGB')).astype(int)
        color = np.median(panel[3:10, 6:58].reshape(-1, 3), axis=0)
        pal = np.unique(np.array(Image.open(os.path.join(SRC, 'preview', 'palette_41.png')).convert('RGB')).reshape(-1, 3), axis=0)
        near = pal[np.argmin(((pal - color) ** 2).sum(1))]
        top = np.zeros((64, 64, 4), np.uint8)
        top[:, :, :3] = near
        top[:, :, 3] = 255
        Image.fromarray(top, 'RGBA').save(os.path.join(out, 'wall_top.png'))
    else:
        shutil.copyfile(os.path.join(SRC, 'tiles', WALL_TOP + '.png'), os.path.join(out, 'wall_top.png'))
    floor = Image.open(os.path.join(SRC, 'tiles', TILES['floor_plain'] + '.png')).convert('RGBA')
    e = np.array(ImageEnhance.Brightness(floor).enhance(0.45))
    e[18:, :, 3] = 0
    Image.fromarray(e, 'RGBA').save(os.path.join(out, 'board_edge.png'))
    for name, src in objs.items():
        path = os.path.normpath(os.path.join(SRC, folder, src + '.png'))
        img = Image.open(path).convert('RGBA')
        img.save(os.path.join(out, name + '.png'))
        if name == 'firewall':
            damaged(img).save(os.path.join(out, 'firewall_damaged.png'))
        if name == 'checkpoint_on':
            unlit(img).save(os.path.join(out, 'checkpoint_off.png'))
    print(theme, len(os.listdir(out)))
